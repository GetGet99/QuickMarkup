---
name: quickmarkup
description: Write and edit QuickMarkup declarative UI markup in C# projects. Use when the project uses QuickMarkup, identifiable by `using QuickMarkup.Infra`, `using QuickMarkup.SourceGen`, or `[QuickMarkup(...)]` attribute in .cs files.
---

# QuickMarkup

QuickMarkup is a Vue-inspired declarative markup DSL embedded in C# that replaces XAML for UI declaration. It uses a **reactivity system** (not MVVM).

## How It Works

QuickMarkup code is placed inside a `[QuickMarkup("""...""")]` attribute on a `partial class`.

```csharp
[QuickMarkup("""
    int Counter = 0;
    <root>
        <StackPanel>
            <Button Text="Click Me" @Click+=`Counter++` />
            <TextBlock Text=`$"You clicked {Counter} time(s)"` />
        </StackPanel>
    </root>
    """)]
partial class CounterPage : Page
{
    public CounterPage() { Init(); }
}
```

A source generator processes the attribute. If the class has at least one user-defined constructor (including primary constructors and record syntax), it generates an `Init()` method the class must call (typically at the end of the constructor, after all other setup). If there are no constructors, it generates a public constructor automatically.

## Sections (in order)

1. **Usings** (optional) — namespace imports for the markup scope. Supports aliases (`using NavigationView = Microsoft.UI.Xaml.Controls.NavigationView;`) and `using static`. Global usings from C# files also apply.
2. **Reference/Computed declarations** (optional) — reactive variable declarations.
3. **`<setup>`** (optional) — C# code that runs before UI creation. Variables declared here are accessible in `<root>` but not exported outside.
4. **`<root>`** — the UI tree. This is where the markup goes.

## Reference Declarations

Declaring variables outside `<setup>` creates reactive references. The generated code wraps them in `Reference<T>` with a property getter/setter.

```
double Value = 0;          // creates Reference<double>, property Value, backing field ValueProp
double Output => `A + B`;  // creates Computed<double>, property Output, backing field OutputComp
```

References auto-notify the UI on change. Computed variables cache and re-evaluate when dependencies change. Computed variables are lazily initialized — not evaluated until first accessed.

References get a `*Prop` backing field and computed get a `*Comp` backing field on the partial class, accessible directly if needed.

## Markup Syntax

### Tags

```
<TypeName Property=Value SomeClass.AttachedPropertyName=Value>
    <Child />
</TypeName>
<SelfClosing Property=Value />
```

Comments use `//` or `/* */`. **Not** `<!-- -->`.

### Property Values

Values are **not** quoted (unlike XML/XAML). Use raw values directly.

| Kind | Syntax | Example |
|------|--------|---------|
| Integer | literal (dec/hex/binary) | `Width=100` / `Tag=0xDEADBEEF` / `Flags=0b101101` |
| Double | literal | `FontSize=14.5` |
| Boolean | literal | `IsChecked=true` |
| Boolean true shorthand | property name alone | `IsEnabled` |
| Boolean false shorthand | `!` prefix | `!IsHitTestVisible` |
| String | double quotes | `Text="Hello"` |
| Enum member | name alone | `HorizontalAlignment=Center` |
| null/default | keyword | `Tag=null` / `Target=default` |
| C# expression | backticks | `` Text=`$"Count: {Counter}"` `` |
| Alternate C# literal (backward compatability legacy syntax of above) | `/-...-/` | `Source=/-new Uri("ms-appx:///icon.png")-/` |

### Automatic `new` (single-argument constructors)

When you assign a **numeric or bool** literal to a property, the source generator may emit **`new PropertyType(literal)`** instead of the raw literal. That happens when the property type does not take the literal directly but exposes a **constructor with exactly one parameter** that does (`CodeTypeResolver.ShouldAutoNew`, `Binder.ValueOrAutoNew`).

**Examples:** `CornerRadius=16` → `new CornerRadius(16)`; `BorderThickness=1` → `new Thickness(1)` when the uniform constructor applies.

**Not covered:** multi-value `Thickness` / `CornerRadius` corners — use a backtick C# expression, e.g. `` Margin=`new(0,12,0,0)` ``. If assignment fails, use an explicit `` `new Thickness(...)` ``.

### C# Expressions (backtick syntax)

`` Property=`expression` `` — the expression re-evaluates automatically whenever any referenced reactive variable changes.

### Binding Directions

| Syntax | Direction | Example |
|--------|-----------|---------|
| `` =`expr` `` | One-way (source→UI) | `` Text=`Name` `` |
| `` =>`var` `` | Bindback (UI→source) | `` SelectedValue=>`Choice` `` |
| `` <=>`var` `` | Two-way | `` Value<=>`Amount` `` |

You can combine one-way + bindback for preprocessing:

```
<NumberBox Value=`Math.Round(Val, 2)` Value=>`Val` />
```

### Events

```
Click+=`(sender, args) => DoSomething()`
@Click+=`Counter++`                        // @ auto-wraps in delegate { ... }
```

### Variable Capture

Assign a tag to a variable accessible from C# code-behind:

```
myButton = <Button Content="Click" />
```

The variable `myButton` becomes a field on the partial class, usable in code-behind methods.

Note that referencing this element in `<setup>` tag would return `null`. The element is only defined from its point on in the markup. (ie. you cannot use the element above), see order of operations.

#### Tag Variable Capture as a reference

Use keyword `ref` to generate a reference pair.

```
<TextBox AutomationProperties.LabeledBy=`InputLabel`/>
ref InputLabel = <TextBlock AutomationProperties.AccessibilityView=Raw Text="subtitle" />
```

On the initial run `InputLabel` will be null, but after `InputLabel` is set, `` AutomationProperties.LabeledBy=`InputLabel` `` will be rerun.

`ref` variable capture is a bit more expensive but does not really provide much value other than use case above, so they are only useful in minimal case where you need to reference MyButton before the element is generated. Try to avoid using `ref` tag variable capture everywhere.

### Inline Tag as Property Value

```
<NumberBox NumberFormatter=<DecimalFormatter IntegerDigits=1 /> />
```

### Inline Collection Children

Use `<>...</>` for collection-typed properties:

```
<Grid RowDefinitions=<>
        <RowDefinition Auto />
        <RowDefinition />
    </>
>
```

### Extension Method Callbacks

An identifier that isn't a recognized property is called as an extension method on the element:

```
<StackPanel CenterH CenterV>
```

This calls `element.CenterH()` and `element.CenterV()`. Define these as extension methods in C#.

### Lambda Callbacks

A standalone backtick expression that is `Action<T>` runs once with the created element:

```
<Grid `x => Grid.SetRow(x, 1)` />
```

### Fragment Children

A `{ ... }` block is a fragment. Contains any valid child nodes:

```
<StackPanel>
    {
        <TextBlock Text="A" />
        <TextBlock Text="B" />
    }
</StackPanel>
```

### Conditional Children

```
if (`condition`) { <TextBlock Text="Visible" /> }
else <TextBlock Text="Fallback" />
```

The `else` branch is required for single-child content positions (e.g., `Content`).

### Foreach Loops

```
// Range (lower inclusive, upper exclusive)
foreach (var i in ..3) { <TextBlock Text=/-$"Row {i}"-/ /> }
foreach (var i in 1..4) { <TextBlock Text=/-$"Item {i}"-/ /> }

// Iterable — reactive when source implements INotifyCollectionChanged
foreach (var item in `items`) { <TextBlock Text=/-item-/ /> }

// With key expression (for stable identity across collection changes), source still must implement INotifyCollectionChanged, but will use id as identity in case of collection reset
foreach (var item in `animals`; `item.Id`) { <TextBlock Text=`item.Name` /> }

// With index variable
foreach (index; var item in `items`) { <TextBlock Text=`$"{index + 1}. {item}"` /> }

// With both
foreach (index; var item in `items`; `item.Id`) { <TextBlock Text=`$"{index + 1}. {item}"` /> }
```

### Root Tag With Properties

`<root>` can carry properties that apply to the class itself (since it inherits from a UI type):

```
<root Background=`bgBrush.Value` CornerRadius=16 Margin=16 Padding=8 />
```

## Setup & Bootstrapping

For new project, for WinUI/UWP, when app is initialized, `ReactiveInitializer.InitReactiveScheduler()` must be called to setup reactivity, otherwise reactivity won't work. For other frameworks, you may refer to an example below and adapt for your framework.

```csharp
// UWP Example
ReactiveScheduler.AddTickCallbackForCurrentThread(delegate
{
    _ = Dispatcher.TryRunAsync(CoreDispatcherPriority.High, ReactiveScheduler.Tick);
});
```

## Order of operations

Inside the `Init()` call or implicitly created constructor,

1. `<setup>` tag is run.
2. QuickMarkup goes through each element, one by one, running in order.
For example:
```
<root> // 1.
    <StackPanel> // 2.
        <TextBlock /> // 3.
        <TextBox /> // 4.
    </StackPanel>
    stack = <StackPanel> // 5.
        <TextBlock /> // 6.
        <TextBox /> // 7.
    </StackPanel>
</root>
```

`stack` variable is assigned and is not null from point `5.` onwards only.

3. For each element, properties and extension methods are evaluated from left to right.

```
// 0. StackPanel is initialized, and `sp` is set to a new stack panel
sp = <StackPanel /* 1. */ First=1 /* 2. */ Second=2
    /* 3. */ ThirdExtension
    /* 4. */ `x => Debug.WriteLine($"This is run on the 4th order, and this should say true: '{sp == x}'")`
    /* 5. */ Fifth=5
/>
```

4. Reactivity changes: properties are rerun whenever values change. No explicit order defined.

### Reference Variables Contains Default Values

IMPORTANT: by the default behavior or when `Init()` call is called at the constructor, if you define a reference class, it may be null, because `Init()` is called before any properties from the parents are set.

For example,
```csharp
record class Card(string Name, string Description);
```

```csharp
[QuickMarkup("""
    Card Card;
    <root>
        // This will crash the app with NullReferenceException, because Card was not yet set on the constructor.
        <TextBlock Text=`Card.Name` />
        // This as well.
        <TextBlock Text=`Card.Description` />
    </root>
    """)]
public partial class CardDisplay : StackPanel;

[QuickMarkup("""
    <root>
        // even if all usage sets the property
        <CardDisplay Card=`new Card("MyCard", "My card description")` />
    </root>
    """)]
public partial class MainPage : Page;
```

In most case, you will need to do null guard or nullable reference type.

```csharp
[QuickMarkup("""
    // good practice to declare as nullable
    Card? Card;
    <root>
        // easiest way is to do if statement around components.
        if (`Card is not null`) {
            <TextBlock Text=`Card.Name` />
            <TextBlock Text=`Card.Description` />
        }

        // alternateively, you may handle it a different way so it does not crash depending on your use case
        <TextBlock Text=`Card?.Name` />
        <TextBlock Text=`Card?.Description` />
    </root>
    """)]
public partial class CardDisplay : StackPanel;
```

## Components

Reusable QuickMarkup components implement one of two interfaces (from `QuickMarkup.WinUI` / `QuickMarkup.UWP`):

- **`IQuickMarkupComponent<T>`** — produces exactly one UI element (its `MarkupNode`). Properties set on the tag that don't exist on the component class are **forwarded** to `MarkupNode` on QuickMarkup, but not on C#. The child must be assignable to type `T`.
- **`IQuickMarkupFragmentComponent<T>`** — produces multiple UI elements; expands inline at the usage site. All children must be assignable to type `T`.

```csharp
[QuickMarkup("""
    string Text = "";
    <root>
        <TextBlock Text=`Text` FontSize=16 />
    </root>
    """)]
public partial class Label : IQuickMarkupComponent<UIElement>;

[QuickMarkup("""
    <root>
        <TextBlock Text="Item A" />
        <TextBlock Text="Item B" />
        <TextBlock Text="Item C" />
    </root>
    """)]
public partial class ItemList : IQuickMarkupFragmentComponent<TextBlock>;
```

You may also omit `<root>` tag if desired (recommended pattern), if you don't really need to put anything inside the `<root>` tag.

```csharp
[QuickMarkup("""
    string Text = "";
    
    <TextBlock Text=`Text` FontSize=16 />
    """)]
public partial class Label : IQuickMarkupComponent<UIElement>;

[QuickMarkup("""
    <TextBlock Text="Item A" />
    <TextBlock Text="Item B" />
    <TextBlock Text="Item C" />
    """)]
public partial class ItemList : IQuickMarkupFragmentComponent<TextBlock>;
```

Consuming a component:

```
// in QuickMarkup, HorizontalAlignment=Center is forwarded automatically to `MarkupNode`
<Label Text="Hello" HorizontalAlignment=Center />
```

```csharp
Label label = new Label();

label.Text = "Hello"; // this is defined on component, can access
label.MarkupNode.HorizontalAlignment = HorizontalAlignment.Center; // properties accessed in C# is not automatically forwareded. .MarkupNode is needed

Children.Add(label.MarkupNode); // need to manually specify markup node here, to get the underlying elmeent
```

For WinUI/UWP project with platform specific package installed, Non-generic versions (`IQuickMarkupComponent`, `IQuickMarkupFragmentComponent`) default `T` to `UIElement`.

Depending on your target UI framework, sometimes subclassing elements directly may make it easier to work with, ie. `partial class MyComponent : Grid`, but for case of sealed elements (ie. WinUI `Border`/`TextBlock` are sealed) or multiple children component/fragment, you may need these.

However, for WinUI/UWP, we usually prefer using `IQuickMarkupComponent<T>` instead of subclassing, since subclassing without XMAL can trigger multiple bugs, especially on top of styled or templated control.

A class may implement at most **one** of the two interfaces. Implementing both produces a compile-time error.

Note:
- `if`/`else`/`foreach` directly on top level without `<root>` tag is currently not supported due to a bug. If you need to use them, you can add `<root>` tag.
- subclassing regular UI still requires `<root>` tag if you have UI markup. Only QuickMarkup components may omit root tags and have non-root tag directly.

## Reactivity Infrastructure

For advanced use in C# code-behind (not inside markup):

```csharp
var r = Ref(0);                     // Reference<int>
var c = Computed(() => r.Value + 1); // Computed<int>
r.Watch(val => { ... });            // callback on change
r.Watch(val => { ... }, immediete: true); // also runs immediately
Effect(() => { ... }, ref1, ref2);  // runs when any listed ref changes
```

`ReferenceTracker.NoCapture(() => expr)` reads without tracking dependencies.

## QuickMarkup.WinUI / QuickMarkup.UWP (NuGet Packages)

These packages provide WinUI 3 / UWP helpers that consume QuickMarkup's generated code.

**NuGet packages:** `QuickMarkup.WinUI` (WinUI 3) / `QuickMarkup.UWP` (UWP)

Namespace `QuickMarkup.WinUI`:

- **`ReactiveInitializer.InitReactiveScheduler()`** — call once on the UI thread (e.g., in `MainWindow` constructor) to initialize the reactive scheduler with the current dispatcher.
- **`ThemeResources.Get<T>(string resourceName)`** — returns a `Reference<T?>` that re-resolves on theme change. Also `Get<T>(string, FrameworkElement)` for per-element theme resolution.
- **`ThemeBrushes`** — static properties returning `Reference<Brush?>` for common WinUI theme brushes (`Accent`, `PrimaryText`, `SolidBackground`, `CardBackground`, `DividerStroke`, `SystemSuccess`, etc.).

### Initiative And Bootstrapping

The entry page must initialize the reactive scheduler. The simplest way is via `ReactiveInitializer.InitReactiveScheduler()`:

```csharp
// App.xaml.cs or MainWindow.xaml.cs
public MainWindow()
{
    this.InitializeComponent();
    QuickMarkup.WinUI.ReactiveInitializer.InitReactiveScheduler();
    Init();
}
```

Only relevant if you start a new project from scratch.

### Using ThemeResources / ThemeBrushes in Markup

```csharp
[QuickMarkup("""
    using QuickMarkup.WinUI;
    <setup>
        var theme = UseThemeBrushes(this);
    </setup>
    <root Background=`theme.SolidBackground`>
        <TextBlock Foreground=`theme.PrimaryText` Text="Hello" />
    </root>
    """)]
partial class MyPage : Page
{
    public MyPage() { Init(); }
}
```

## Best Practices

- Define **global usings** for common namespaces (`QuickMarkup.Infra`, `static QuickMarkup.Infra.QuickRefs`, etc.) so markup stays clean.
- Define **C# extension methods** like (`CenterH`, `CenterV`, `Center`, `Right`, `Bottom`, `StretchH`, `StretchV`) for layout shortcuts.
- The class must be `partial` (source generator emits the other part). The base class should be a UI element (`Page`, `Grid`, `StackPanel`, etc.) or QuickMarkup component interface mentioned above.
