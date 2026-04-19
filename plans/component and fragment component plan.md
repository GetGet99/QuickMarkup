# Component And Fragment Component Plan

This plan covers adding QuickMarkup component wrapper support:

- `IQuickMarkupComponent<T>`
- `IQuickMarkupFragmentComponent<T>`

These interfaces allow a QuickMarkup-decorated type to render an output value without deriving from that output value's UI type. This is intended for sealed framework controls and design-system wrappers.

## API Decision

Recommended public interfaces:

```csharp
namespace QuickMarkup.Infra;

public interface IQuickMarkupComponent<out T>
{
    T MarkupNode { get; }
}

public interface IQuickMarkupFragmentComponent<out T>
{
    FragmentBlock<T> MarkupNode { get; }
}
```

`MarkupNode` is the selected output property name.

Rejected or risky names:

- `Content`: conflicts with user-facing content slots, `ContentControl.Content`, and common component-child patterns.
- `Child`: conflicts with single-child containers and user API that accepts a child input.
- `Root`: works for single components but does not describe fragments.
- `View`: UI-specific and less suitable if QuickMarkup later supports non-UI object graphs.
- `Build()` / `Create()`: suggests repeated construction, while the generated output should be stable for a component instance.

Other possible names:

- `Rendered`
- `RenderedNode`
- `MarkupRoot`
- `Output`
- `Value`

`MarkupNode` was selected because it is unlikely to collide with app-level child/content API and describes the generated QuickMarkup output without implying visual inheritance.

## Current State

Relevant files:

- `QuickMarkup.Infra/FragmentBlock.cs`
- `QuickMarkup.Language/Symbols/QuickMarkupSymbols.cs`
- `QuickMarkup.CodeAnalysis/CodeTypeResolver.cs`
- `QuickMarkup.CodeAnalysis/Binders/QuickMarkupBinder.cs`
- `QuickMarkup.CodeAnalysis/Binders/QMBinderError.cs`
- `QuickMarkup.SourceGen/QuickMarkupGenerator.cs`
- `QuickMarkup.SourceGen/CodeGen/CodeGenContext.cs`
- `QuickMarkup.SourceGen.Test/`
- `docs/qm-language.md`
- `docs/get-most-out-of-qm.md`

Existing behavior:

- A QuickMarkup target's `<root>` binds against the target class itself.
- Children are located through `CodeTypeResolver.TryGetContentProperty`.
- Child insertion emits either direct `collection.Add(...)`, assignment to a child property, or block lowering for structural children.
- Extension-method markers are represented as `QMExtensionMember` and currently emit against the tag variable itself.
- Callback markers are represented as `QMCallbackMember<T>` and currently receive the tag variable itself.
- `FragmentBlock<T>` already exists for structural child lowering.

Missing behavior:

- No marker interface for component output.
- No binding model for "this tag creates a wrapper but inserts its output".
- No generated output property.
- No component-mode `<root>` cardinality validation.
- No extension-property or extension-method fallback against single component output.
- No fragment-component expansion at parent insertion sites.

## Goals

Support single-output components:

```csharp
[QuickMarkup("""
string Text = "";
<root>
    <TextBlock Text=`Text` />
</root>
""")]
public partial class StyledText : IQuickMarkupComponent<UIElement>;
```

Parent usage should behave like an element at insertion and extension points:

```csharp
<StackPanel>
    <StyledText Text="Hello" CenterH Grid_Row=1 />
</StackPanel>
```

Conceptual generated parent code:

```csharp
var styledText = new StyledText();
styledText.Text = "Hello";
styledText.MarkupNode.CenterH();
Grid.SetRow(styledText.MarkupNode, 1);
stackPanel.Children.Add(styledText.MarkupNode);
```

Support fragment-output components:

```csharp
[QuickMarkup("""
<root>
    <TextBlock Text="A" />
    <TextBlock Text="B" />
</root>
""")]
public partial class TwoLabels : IQuickMarkupFragmentComponent<UIElement>;
```

Parent usage:

```csharp
<StackPanel>
    <TwoLabels />
</StackPanel>
```

Conceptually expands the fragment block into the parent child collection.

## Non-Goals

- Do not make `IQuickMarkupComponent<T>` derive from `T`.
- Do not support fragment-wide extension methods in the first pass.
- Do not map extension methods to every element in a fragment.
- Do not forward fragment-component properties, extension markers, or callbacks to `MarkupNode`.
- Do not change normal QuickMarkup root semantics for non-component classes.

## Component Metadata

Add component-shape detection to code analysis.

Possible shape:

```csharp
enum QuickMarkupComponentKind
{
    None,
    Single,
    Fragment
}

sealed record QuickMarkupComponentInfo(
    QuickMarkupComponentKind Kind,
    ITypeSymbol? OutputType,
    string OutputPropertyName
);
```

Detection rules:

- `IQuickMarkupComponent<T>` means `Kind = Single`, `OutputType = T`.
- `IQuickMarkupFragmentComponent<T>` means `Kind = Fragment`, `OutputType = T`.
- Reject a type implementing both interfaces.
- Reject multiple closed versions of either interface.
- Reject interfaces with unresolved `T`.
- Reject component mode if the generated output property would collide with a real or synthetic member.

Implementation location:

- Prefer `CodeTypeResolver` for symbol-level detection so binder and sourcegen can share it.
- Keep string comparisons on fully qualified metadata names:
  - `global::QuickMarkup.Infra.IQuickMarkupComponent<T>`
  - `global::QuickMarkup.Infra.IQuickMarkupFragmentComponent<T>`

## Infra Plan

Add interfaces to `QuickMarkup.Infra`.

```csharp
namespace QuickMarkup.Infra;

public interface IQuickMarkupComponent<out T>
{
    T MarkupNode { get; }
}

public interface IQuickMarkupFragmentComponent<out T>
{
    FragmentBlock<T> MarkupNode { get; }
}
```

Notes:

- Use covariance because the interface only returns `T`.
- Keep the output property get-only.
- The generated implementation may use a private setter or field internally.

## Binding Plan

### Root Binding

For non-component targets:

- Keep existing behavior: `<root>` binds against the target type.

For `IQuickMarkupComponent<T>` targets:

- `<root>` binds as a single output value assignable to `T`.
- It must contain exactly one non-property child.
- Property-tag children on `<root>` target `this`, not `MarkupNode`.
- This allows component authors to configure helper properties, events, or C#-declared component state while declaring the output node.
- The bound result should preserve enough information for sourcegen to assign the generated output property.

For `IQuickMarkupFragmentComponent<T>` targets:

- `<root>` binds as a fragment output containing zero or more children assignable to `T`.
- Structural children are allowed and should lower into a `FragmentBlock<T>`.
- Property-tag children on `<root>` target `this`, not the fragment block.

Potential symbol additions:

```csharp
public record class QMComponentRootSymbol(
    ITypeSymbol? OutputType,
    IQMNodeChildSymbol Output
) : IQMMemberSymbol;

public record class QMFragmentComponentRootSymbol(
    ITypeSymbol? ElementType,
    IReadOnlyList<IQMMemberSymbol> Body
) : IQMMemberSymbol;
```

Alternatively, avoid new symbols and let sourcegen receive a different bound root shape. The key requirement is that normal root member writes to `this` should not be used for component mode.

### Child Value Materialization

Introduce a reusable binding/lowering concept for child values:

- Raw tag value: constructed object.
- Single component tag value: constructed component object, materialized as `component.MarkupNode` when inserted into parent content.
- Fragment component tag value: constructed component object, materialized as `component.MarkupNode` when inserted into additive child collections or fragment-producing contexts.

This should be centralized rather than handled only in `CGenAddChildDirect`, because it also affects:

- assignment child positions
- value-list collection positions
- block lowering
- static range loops
- conditional branches

Potential symbol shape:

```csharp
public record class QMComponentNodeSymbol<T>(
    QMNodeSymbol<T> Component,
    QuickMarkupComponentKind Kind,
    T? OutputType,
    string OutputPropertyName
) : IQMNodeChildSymbol, IQMValueSymbol;
```

This keeps the component object available for property assignment while exposing output metadata to parent lowering.

### Property And Extension Resolution

For a tag whose type is a single component:

Resolution order for inline members:

1. Real or synthetic property/event on the component type.
2. Existing normal component assignment or event binding.
3. If a normal property or event is not found on the component, and the property/event can be detected on output type `T`, emit it against `component.MarkupNode`.
4. If no property is found for an identifier-only marker, emit an extension-style call against `component.MarkupNode`.
5. Let C# validate whether the extension-style call is actually valid.

For a fragment component:

- Normal properties and events apply to the component object.
- Do not forward property, event, extension-marker, or callback usage to `MarkupNode`.
- If users add extension methods/properties directly for the fragment component class, normal C# compilation can validate those generated calls.

Important cases:

```csharp
<StyledText Text="Hello" />
```

`Text` should bind to the component.

```csharp
<StyledText CenterH />
```

`CenterH` should bind against `StyledText.MarkupNode` if `CenterH` is an extension method on the output type.

```csharp
<StyledText IsVisible />
```

If `IsVisible` is not a component property but is an extension property on the output type, emit `styledText.MarkupNode.IsVisible = true`.

```csharp
<StyledText Visibility=Collapsed />
```

If `Visibility` is detectable on the output type, emit `styledText.MarkupNode.Visibility = Visibility.Collapsed`.

Detection limits:

- Do not add a new compilation-hack path only to detect extension members.
- If detecting an extension property/method is not straightforward, fall back to generating an extension-style call on `MarkupNode` and let C# report any invalid member.
- This preserves the current QuickMarkup behavior where extension markers assume the user knows what the generated C# call means.

Infra helper option:

```csharp
static void ExtensionResolve<T>(IQuickMarkupComponent<T> component, Action<T> action);
static void ExtensionResolve<T>(IQuickMarkupComponent<T> component, Action<IQuickMarkupComponent<T>> action);
static void ExtensionResolve<T, TResult>(IQuickMarkupComponent<T> component, Func<T, TResult> action);
static void ExtensionResolve<T, TResult>(IQuickMarkupComponent<T> component, Func<IQuickMarkupComponent<T>, TResult> action);
```

Then an identifier marker can emit:

```csharp
global::QuickMarkup.Infra.CompilerHelpers.ExtensionResolve(
    component,
    x => x.CenterH());
```

The return value is ignored for compatibility with current extension-marker behavior. C# overload resolution decides whether the expression applies to the component wrapper or the output node.

Event fallback:

- Events should target the component when found there.
- If not found on the component and an event is detectable on the output type, emit against `component.MarkupNode`.
- Do not invent synthetic event lookup for QuickMarkup-generated members because QuickMarkup cannot declare events.

### Callback Resolution

For callback markers:

```csharp
<StyledText `x => x.Visibility = Visibility.Collapsed` />
```

Always target the component object, not `MarkupNode`.

Rationale:

- It keeps callback behavior flexible for wrapper APIs.
- It avoids advanced delegate-target detection.
- Users can still write `x => x.MarkupNode.Visibility = Visibility.Collapsed` when they want the output node.

## Code Generation Plan

### Generated Component Declaration

In component mode, generated partial class should be sealed:

```csharp
public sealed partial class StyledText
{
    public UIElement MarkupNode { get; private set; } = null!;
}
```

Current generator emits members inside a partial class through `AddSource`. It may need support for adding `sealed` to the generated partial declaration. If `AddSource` currently always emits `partial class`, update the helper or add a component-specific source path.

Diagnostics:

- If user declaration cannot be sealed cleanly, report a QuickMarkup diagnostic.
- If the type is already explicitly sealed, generated sealed partial should be compatible.
- If the type is abstract/static, reject component mode.

### Initialization Shape

Keep eager output creation to match existing QuickMarkup behavior:

- Constructor mode: generate output during the generated constructor.
- Explicit-constructor mode: generate output during `Init()`.

Conceptual shape:

```csharp
public StyledText()
{
    // refs are lazy properties; setup runs here as today
    MarkupNode = CreateMarkupNode();
}
```

or inside current generated constructor body without adding a separate method if that minimizes diffs.

Reactive bindings should still update output when parent-assigned generated refs change after construction.

### Parent Insertion

Update codegen so a component node can be configured as a component object and materialized as output for insertion.

Single component:

- Construct component variable.
- Emit inline member writes against either component variable or `component.MarkupNode`, depending on binding result.
- Return `component.MarkupNode` from child-value codegen when a parent needs a child value.

Fragment component:

- Construct component variable.
- Emit inline member writes against component variable.
- In additive child collection contexts, add or host `component.MarkupNode` as an `IUIBlock<T>` rather than calling `Children.Add(component)`.
- Reject assignment child contexts. Do not forward fragment components as property values, even if the property type could accept `FragmentBlock<T>`, unless a later feature explicitly designs that behavior.

### Block Lowering

Fragment components should force block lowering in additive child collections, because they produce an `IUIBlock<T>` rather than a single item.

Single components should not force block lowering by themselves.

Structural cases:

- `if` branch returning a fragment component in collection context should be allowed.
- `foreach` body containing a fragment component should be allowed in block-lowered context.
- single-child conditional branches should reject fragment component values.

## Diagnostics Plan

Add diagnostics for:

- component target implements both interfaces
- component target implements multiple closed versions of one interface
- component root has zero or multiple children in single-output mode
- component root child is not assignable to output type
- fragment component child is not assignable to fragment element type
- fragment component used in single-child assignment context
- generated output property name collision when QuickMarkup needs to generate `MarkupNode`
- generated sealed partial conflicts with user type shape

Use existing `QMBinderError` patterns where possible.

## Tests

Add behavioral sourcegen tests after or alongside the sourcegen behavioral test plan.

Minimum cases:

- single component generates and exposes output property
- single component is sealed in generated partial
- generated ref property on component can be set from parent markup
- parent insertion unwraps single component output
- extension method marker applies to single component output
- extension property / attached-property helper applies to single component output
- component property wins over output property when names collide
- callback marker receives the component, not `MarkupNode`
- root property-tag children target the component instance
- single component rejects multiple root children
- fragment component root allows multiple children
- parent insertion expands fragment component in additive child collection
- fragment component does not forward properties or extension markers to `MarkupNode`
- fragment component rejects single-child assignment context

## Documentation Plan

Update `docs/qm-language.md`:

- Add component wrapper section.
- Explain that component output is not inheritance.
- Explain generated refs as component inputs.
- Explain eager construction and reactive updates.
- Explain that plain auto-properties read during initialization are constructor-time values.
- Document fragment component limitations.

Update `README.md` with a short example after the existing page example.

Update `docs/get-most-out-of-qm.md`:

- Clarify that extension helpers on UI elements can apply through single-output components.

## Implementation Order

1. Add infra interfaces with the final output property name.
2. Add component metadata detection in `CodeTypeResolver`.
3. Add binder diagnostics for invalid component shapes.
4. Add component-mode root binding.
5. Add child materialization symbols or equivalent metadata.
6. Update codegen for generated output property and sealed partial output.
7. Update child insertion for single component unwrapping.
8. Update child insertion for fragment component expansion.
9. Add extension/property/event output-target fallback for single components.
10. Add tests.
11. Update docs.

## Decisions

- Output property name is `MarkupNode`.
- Ordinary output properties and events can unwrap when they are detectable on the output type.
- Extension markers for single components generate against `MarkupNode` when the marker is not a component property.
- Callback markers target the component object.
- Fragment components never forward properties, events, extension markers, or callbacks to `MarkupNode`.
- Fragment components are not forwarded as property values in v1.
- Manually authored `MarkupNode` is allowed when the type has no QuickMarkup attribute, no `<root>` tag, or a `<root>` tag with no child output.
- If QuickMarkup needs to generate output from `<root>` children and the target already has a user-authored `MarkupNode`, report an error.
