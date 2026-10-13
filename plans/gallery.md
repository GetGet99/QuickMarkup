# QuickMarkup Gallery — Full Plan

## Goal

A reusable gallery library in the spirit of WinUI 3 Gallery, WinUI 2 Gallery, and Community Toolkit Gallery, for QuickMarkup.

Two parts:

1. Runtime (`QuickMarkup.Gallery`): single Uno Platform class library providing the gallery shell — navigation, categories, search, sample renderer, code viewer, prop editors, event log.
2. Source generator (`QuickMarkup.Gallery.SourceGen`): built on the shared `QuickMarkup.CodeAnalysis` project (same pattern as `QuickMarkup.Snapshot.SourceGen`) generating gallery wiring — discovery, metadata, factories, typed props, usage-code formatters.

Two authoring flavors:

1. Split attributes: `[GalleryCategory]` + `[GalleryName]` + `[GalleryStory]`. Simple mode works out of the box for primitives and enums. Complex types fall back to read-only with a warning.
2. Storybook style: explicit story factory methods and custom editors for variants, complex args, and actions.

## Locked Decisions

1. Single Uno project. No `WinUI` / `Uno` split. The old split was a migration artifact.
2. Wide input range, no vendor lock-in (non-urgent). v0/v1 optimize for QuickMarkup types. Non-QuickMarkup types are supported through hand-written story classes. No reflection anywhere.
3. Code tab shows usage-code (how to consume the sample), not the sample's own source. Statically generated formatter plus runtime interpolation. Reflection is banned for AOT reasons.
4. `required` refs are handled through story factories using the existing `Action<T>` initializer constructor. Simple mode errors on unprovidable `required` refs and points at story mode.
5. Attribute shape is split: `[GalleryCategory]` + `[GalleryName]` + `[GalleryStory]`. Hierarchy is `Category - Component - Story`. Missing category means empty category, shown as "Uncategorized". Missing name means class name. Category accepts `const string` so users can share constants later.
6. Shell is written in QuickMarkup. Nav and props use `foreach`; sample boundary is `<Border Child=... />`; a buggy sample cannot break the shell beyond its own tile. SourceGen/runtime provide plain lists; shell builds UI from them.
7. Built-in editor controls: `TextBox` for string, `ToggleSwitch` for bool, `ComboBox` for enum/options, `TextBlock` read-only fallback.
8. Entry point is generated `GalleryApp.RunGallery()`, called from `OnLaunched` when the user wants the gallery. If never called the app runs normally. No `ModuleInitializer`, no auto-open, AOT-safe.

## Repo Shape

```csharp
// C#
QuickMarkup.Gallery/                 // Uno runtime lib, no Roslyn
QuickMarkup.Gallery.SourceGen/       // IIncrementalGenerator, imports QuickMarkup.CodeAnalysis.props
QuickMarkup.Gallery.Demo/            // Uno demo app, refs Gallery + SourceGen
QuickMarkup.Gallery.Test/            // binder mapping + emission unit tests over in-memory Roslyn
QuickMarkup.Gallery.IntegrationTest/ // registry, factory, usage-code behavior
docs/gallery.md                      // user doc, written at v1
```

Follow the Snapshot precedent:

- Runtime targets the Uno-compatible TFM.
- SourceGen is `netstandard2.0` with `IsRoslynComponent` and imports `QuickMarkup.CodeAnalysis.props`.
- Shared attribute definitions are linked into SourceGen via `<Compile Include="..\QuickMarkup.Gallery\Attributes.cs" Link="..." />`.
- Gallery binder derives from `QuickMarkup.CodeAnalysis.Binders.Binder`, mirroring `SnapshotBinder`.

## Runtime Design

### Story abstraction

The shell only sees a non-generic base. All typing lives in generated closures, so the shell never switches on `T`.

```csharp
// C#
abstract class GalleryStory
{
    public abstract string Category { get; }
    public abstract string ComponentName { get; }
    public abstract string StoryTitle { get; }
    public abstract string Id { get; }
    public abstract string? Description { get; }

    public abstract object CreateModel();
    public abstract FrameworkElement CreateElement(object model);
    public abstract IReadOnlyList<GalleryPropDescriptor> CreateProps(object model);
    public abstract string GetUsageCode(object model);
}

abstract class GalleryStory<T> : GalleryStory
{
    public abstract T CreateTypedModel();
    public abstract FrameworkElement CreateTypedElement(T model);
    public abstract IReadOnlyList<GalleryPropDescriptor> CreateTypedProps(T model);
    public abstract string GetTypedUsageCode(T model);

    public override object CreateModel() => CreateTypedModel()!;
    public override FrameworkElement CreateElement(object model) => CreateTypedElement((T)model);
    public override IReadOnlyList<GalleryPropDescriptor> CreateProps(object model) => CreateTypedProps((T)model);
    public override string GetUsageCode(object model) => GetTypedUsageCode((T)model);
}
```

One `CreateModel()` call per navigation; element, props, and code share that instance. Element unwrapping rule: `FrameworkElement` used directly, `IQuickMarkupComponent<T>` via `.MarkupNode`, fragment wrapped in a panel, non-UI via `ToString()` fallback.

Manual registration exists independent of source-gen and is the seam for non-QuickMarkup types:

```csharp
// C#
GalleryRegistry.Register(GalleryStory story);
```

### Catalog grouping

Flat story list is grouped for nav as `Category - Component - Story`, sorted by `(Category, ComponentName, StoryTitle)`. Empty category sorts last and shell shows "Uncategorized".

```csharp
// C#
sealed class GalleryCatalog
{
    public IReadOnlyList<GalleryCategoryGroup> Categories { get; }
}

sealed class GalleryCategoryGroup
{
    public string Name { get; }
    public IReadOnlyList<GalleryComponentGroup> Components { get; }
}

sealed class GalleryComponentGroup
{
    public string DisplayName { get; }
    public IReadOnlyList<GalleryStory> Stories { get; }
}
```

### Shell

QuickMarkup shell. SourceGen/runtime hand over `GalleryCatalog` plus current selection; shell renders with `foreach` and embeds via `<Border Child=... />`:

```csharp
// QuickMarkup
foreach (var category in `Catalog.Categories`) {
    <TextBlock Text=`category.Name` />
    foreach (var component in `category.Components`) {
        foreach (var story in `component.Stories`) {
            <Button Content=`story.StoryTitle` @Click+=`() => Select(story)` />
        }
    }
}
<Border Child=`SelectedElement` />
foreach (var prop in `CurrentProps`) {
    <Border Child=`prop.CreateRendering()` />
}
<TextBox Text=`CurrentUsageCode` />
```

### Entry point

```csharp
// C#
public static class GalleryApp
{
    public static void RunGallery();
}
```

Generated. Inits reactive scheduler, builds catalog, opens `Window` with `GalleryShell`, calls `Activate()`.


The caller (library user) decides when to invoke it:

```csharp
// psudocode
void OnLaunched(...)
{
    if (ShouldShowGallery())
        GalleryApp.RunGallery();
    else
        RunApp();
}
```

Manual shim `GalleryBoot.RunManual(catalog)` mirrors the shape until the generator replaces it.

### Prop descriptors: polymorphic, always a renderer

No central `switch` on prop type in the shell. Each descriptor creates its own rendering and formats its own usage-code fragment.

```csharp
// C#
abstract class GalleryPropDescriptor
{
    public string Name { get; }
    public string? Description { get; }
    public abstract Type ValueType { get; }
    public abstract FrameworkElement CreateRendering();
    public abstract string FormatForUsageCode();
}

sealed class GalleryProp<T> : GalleryPropDescriptor
{
    public Func<T> Getter { get; }
    public Action<T> Setter { get; }
    public Func<GalleryProp<T>, IValueEditor<T>> EditorFactory { get; }
    public T Value { get => Getter(); set => Setter(value); }

    public override FrameworkElement CreateRendering()
    {
        var editor = EditorFactory(this);
        editor.ValueProp.Value = Getter();
        editor.ValueProp.Watch(v => Setter(v));
        return ((IQuickMarkupComponent<UIElement>)editor).MarkupNode;
    }
}

abstract class GalleryOptionsDescriptor : GalleryPropDescriptor
{
    public abstract IReadOnlyList<string> OptionLabels { get; }
    public abstract int SelectedIndex { get; set; }
}

sealed class GalleryOptionsProp<T> : GalleryOptionsDescriptor
{
    public IReadOnlyList<T> Options { get; }
}
```

Descriptors are classes, not records, because they hold delegate fields. The non-generic `GalleryOptionsDescriptor` lets shell code that does not know `T` work with option labels and selection. `FormatForUsageCode()` keeps usage-code formatting polymorphic too.

### Editors

```csharp
// C#
interface IValueEditor<T> : IQuickMarkupComponent<UIElement>
{
    Reference<T> ValueProp { get; }
}
```

Built-ins: string, boolean, integer, double, options/enum, read-only `ToString()` fallback. Each is a QuickMarkup component implementing `IValueEditor<T>`. Data flow is standard reactivity: initialize from `Getter()`, `Watch` pushes back through `Setter(v)`.

Custom editors via assembly attribute, resolved at compile time:

```csharp
// C#
[assembly: GalleryValueEditor(typeof(MyColor), typeof(MyColorEditor))]
```

Generator validates that the editor implements `IValueEditor<T>` and is constructible, then emits `EditorFactory = () => new MyColorEditor()`. Runtime never reflects. Unknown `T` with no mapping emits the read-only fallback plus warning `QMGAL002`.

## SourceGen Design

### Discovery

- `ForAttributeWithMetadataName("QuickMarkup.Gallery.GalleryStoryAttribute")` on classes. Read sibling `[GalleryCategory]` / `[GalleryName]` from the same symbol.
- Combined with `ForAllQuickMarkupSuccessfulParse()` for ref extraction, same combine pattern as the Snapshot generator.
- Later: story factory methods (`static`, parameterless, returning the sample type) discovered from story classes.

### Simple mode

```csharp
// C#
[GalleryCategory("Form and user Inputs")]
[GalleryName("Buttons")]
[GalleryStory("Basic Button", Description = "Default state")]
[QuickMarkup("""
    // QuickMarkup
    string Text = "Click me";
    bool IsEnabled = true;
    <Button Content=`Text` IsEnabled=`IsEnabled` />
    """)]
partial class ButtonSample;
```

Generator tasks:

1. Validate target is a `partial`, concrete class with an accessible parameterless constructor or only `required` refs satisfiable through a story factory. Error otherwise.
2. `GalleryBinder : Binder` walks `markup.AST.Refs`, skipping computed, async-computed, and `provide`/`inject` unless explicitly opted in (same exclusion rationale as `SnapshotBinder`).
3. Map `ITypeSymbol` to descriptor kind: `string`, numeric, `bool` to `GalleryProp<T>`; enum to `GalleryOptionsProp<T>`; anything else to read-only fallback plus `QMGAL002`.
4. Check `[assembly: GalleryValueEditor]` mappings for custom `EditorFactory`.
5. Emit per-story `GalleryStory<T>` subclass: `Category` / `ComponentName` (override or class name) / `StoryTitle`, `CreateTypedModel` factory, `CreateTypedProps` with typed getter/setter closures capturing the model, `CreateTypedElement` unwrapping, `GetTypedUsageCode` formatter with per-type printers (string escaped/quoted, bool lowercase invariant, numeric invariant, enum name, unsupported omitted).
6. Emit per-assembly grouped catalog contributing all stories sorted by `(Category, ComponentName, StoryTitle)`, plus generated `GalleryApp.RunGallery()` opening the shell window.

### Usage-code

The generator emits a formatter; the runtime calls it with live values, so the code tab stays in sync after prop edits. For story factories with arbitrary C# bodies, the generator does not parse the body. Usage-code falls back to reconstructed initializer syntax from current prop values, or an explicit `[GalleryStory(UsageCode = "...")]` override.

### Storybook mode

```csharp
// C#
[GalleryStories(typeof(ButtonSample))]
partial class ButtonStories
{
    [GalleryStory("Disabled")]
    public static ButtonSample Disabled() => new(x => { x.Text = "Disabled"; x.IsEnabled = false; });
}
```

One `GalleryItemDescriptor` equivalent (one `GalleryStory` instance) per story method. Validation: containing class marked with `[GalleryStories]`, method `static` and parameterless, return type is the sample type.

### Diagnostics

- `QMGAL001` error: bad target, duplicate story `Id` (`Category` + `ComponentName` + `StoryTitle`, same duplicate-key checking approach as Snapshot discriminators), unprovidable `required` ref in simple mode, malformed story method.
- `QMGAL002` warning: unsupported type downgraded to read-only.
- `QMGAL003` error: bad `[assembly: GalleryValueEditor]` mapping (editor does not implement `IValueEditor<T>`, not constructible).

## Non-QuickMarkup Support

Hand-written story subclass, no `[QuickMarkup]` required:

```csharp
// C#
sealed class ThirdPartyButtonStory : GalleryStory<ThirdPartyButton>
{
    public override ThirdPartyButton CreateTypedModel() => new();
    // hand-written CreateTypedElement, CreateTypedProps with GalleryProp<T>, GetTypedUsageCode
}
```

Generator ignores non-QM types with an info diagnostic in v0/v1. Roslyn-symbol-based public-property extraction (mirroring Snapshot `IncludesPublic` mode) is a future enhancement and needs no shell change.

## Milestones

### v0: minimal prototype, always runnable

Each slice lands in the same branch as a stacked commit and keeps `QuickMarkup.Gallery.Demo` runnable.

- Slice-0 plan: this file update. No code.
- Slice-1 runtime + manual demo: story/prop/editor bases, `StringEditor` (`TextBox`), `BoolEditor` (`ToggleSwitch`), read-only fallback (`TextBlock`), `GalleryRegistry` + grouped `GalleryCatalog`, QuickMarkup `GalleryShell` with `foreach` nav, `<Border Child=... />` host, props tab, raw code tab, `GalleryBoot.RunManual`. Two hardcoded samples in demo. Done when demo lists by category, renders a sample, edits string/bool live, views code.
- Slice-2 source-gen: `[GalleryStory]` (+`Category`/`Name`) discovery, primitive prop emission, usage-code formatter, generated `GalleryApp.RunGallery`, `QMGAL001`/`QMGAL002`. Demo switches from manual to generated. Done when demo behaves as slice-1 with zero manual registration.
- Slice-3 props + tests: `int` editor, enum `ComboBox` options, "Uncategorized" + `(Category, ComponentName, StoryTitle)` sort, `Gallery.Test` + minimal integration tests. Done when `dotnet test` green on new projects.

Non-goals: search, event log, theming, story factories, custom editor attribute, packaging.

### v1: shippable

1. Navigation polish: categories, ordering, search filter, deep link (`?sample=Id`).
2. Prop hardening: `double`, nullable, default values, `required`-ref error guidance pointing at story factories.
3. Story factories: `[GalleryStories]` / per-method stories, validation, reconstructed usage-code.
4. Events/actions: `event Action` members surfaced into an event log panel.
5. Custom editors: `[assembly: GalleryValueEditor]` end to end with one real custom editor.
6. Docs and packaging: `docs/gallery.md`, skill note, NuGet (`QuickMarkup.Gallery` runtime plus analyzer package), `AnalyzerReleases` entries, NativeAOT pass.

### Future

- Complex-type editors (Color, Brush, Thickness, collections, `ObservableCollection` / `ReactiveList` playgrounds).
- Theme, density, locale switchers.
- Roslyn public-property extraction for non-QM types.
- Doc-comment extraction for prop descriptions.
- Screenshot and visual-regression hooks.
- Accessibility and performance panels.
- Item templates for adding new samples.

## Testing

- `QuickMarkup.Gallery.Test`: binder type-mapping tests, editor-mapping tests, emission snapshot tests over in-memory Roslyn compilations.
- Integration tests: registry count and ordering, factory creates expected element kind, `GetUsageCode` output for primitives/enum, `required`-ref error case. Shared-style cases where behavior must hold under both init modes.
- Run `dotnet test` per change; run `test-nativeaot.sh` before v1 ship to catch trimming or `Activator` regressions.

## Risks

- Usage-code printer coverage: backtick expressions that are not simple refs have no faithful literal form; formatter must degrade to omitting or quoting the expression text.
- Editor two-way sync loops: initialize-then-watch discipline, plus external model changes needing reverse sync.
- `GalleryProp<T>` delegate equality and lifetime: descriptors are per-model-instance, short-lived, never cached across navigations.
- Source embedding size: one formatter per story is small; never embed whole generated `.g.cs` files.
