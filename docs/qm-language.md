# QuickMarkup Language

## Using Statements

In the top section of QuickMarkup, it optionally declares the namespaces to be imported (usings) and list of references and computed variables.

```cs
using Windows.UI.Xaml.Controls;
// Supports explicit declaration, in case of ambiguity
using NavigationView = Microsoft.UI.Xaml.Controls.NavigationView;
// Also supports using static
using static QuickMarkup.Infra.QuickRefs;
```

Additionally, QuickMarkup recognizes **global using imports**. This will be very helpful to avoid repeating using statements.

```cs
// GlobalUsings.cs

// These will be taken into account inside QuickMarkup Tag as well.
global using Windows.UI.Xaml.Controls;
global using Microsoft.UI.Xaml.Controls;
global using NavigationView = Microsoft.UI.Xaml.Controls.NavigationView;
global using static QuickMarkup.Infra.QuickRefs;
```

## Reference Declarations

Declaring variables in QuickMarkup before `<setup>` tag creates reactive references. Use `=>` for computed variables — they cache and auto-re-evaluate when dependencies change.

```cs
// Inside QuickMarkup

// Declare references
double FirstOperand = 1;
double SecondOperand = 2;

// Declare computed variables
double Output => `FirstOperand + SecondOperand`;
```

References get a `*Prop` backing field, computed get `*Comp` — accessible directly if needed. Computed variable is lazily initialized and caches its value until dependent references chagne.

Above example will generate the following fields:

```cs
partial class Calc {
    public Reference<double> FirstOperandProp => field ??= new Reference<double>(1, "Calc.FirstOperand");
    public double FirstOperand {
        get {
            return this.FirstOperandProp.Value;
        }
        set {
            this.FirstOperandProp.Value = value;
        }
    }
    public Reference<double> SecondOperandProp => field ??= new Reference<double>(2, "Calc.SecondOperand");
    public double SecondOperand {
        get {
            return this.SecondOperandProp.Value;
        }
        set {
            this.SecondOperandProp.Value = value;
        }
    }
    public Computed<double> OutputComp => field ??= new Computed<double>(() => FirstOperand + SecondOperand, "Calc.Output");
    public double Output {
        get {
            return this.OutputComp.Value;
        }
    }
}
```

This is useful to be used in UI and is a shorthand of declaring references.

```cs
double FirstOperand = 1;
double SecondOperand = 2;
double Output => `FirstOperand + SecondOperand`;
<root>
    <StackPanel Orientation=Horizontal Spacing=16>
        <NumberBox Value<=>`FirstOperand` />
        <TextBlock Text="+" CenterV />
        <NumberBox Value<=>`SecondOperand` />
        <TextBlock Text="=" CenterV />
        <TextBlock Text=`Output.ToString()` CenterV />
    </StackPanel>
</root>
```

## Provide/Inject (Experimental)

Provide/Inject is a context-based dependency injection system for passing reactive references between parent and child components. A parent `provide`s a value, and child components `inject` it by name and type.

### Syntax

```cs
// Parent component: provide a value to children
provide string Theme = "dark";

// Child component: required injection (throws at runtime if no provider found)
inject string Theme;

// Child component: optional injection (returns default if no provider found)
inject? string Theme;
```

### Renaming with `as`

Use `as` to decouple the local reference name from the context key. This is useful when the provider and consumer want different names for the same shared reference.

```cs
// Parent: backing ref is MyRefProp, but exposed to context as MyCtx
provide string MyRef as MyCtx = "dark";

// Child: inject from context key MyCtx into local backing ref MyRefProp
inject string MyCtx as MyRef;
```

Note the **ordering difference**: `provide ref as contextName`, `inject contextName as ref`.

- `provide string MyRef as MyCtx` — the local name is `MyRef` (generates `MyRefProp`), the context key is `MyCtx`.
- `inject string MyCtx as MyRef` — the context key is `MyCtx`, the local name is `MyRef` (generates `MyRefProp`).

Optional injection also supports `as`:

```cs
inject? string MyCtx as MyRef;
```

### How It Works

1. The parent's `provide` stores its `Reference<T>` backing field in a `QuickMarkupContext`.
2. When a child component is created in the parent's markup, it receives a **child context** (cloned from the parent's context).
3. The child's `inject` retrieves the same `Reference<T>` object from the context, enabling **bidirectional reactivity** — changes to the provided value are reflected in the injector.

```cs
// Parent
provide string Label = "hello";

// Child (separate component)
inject string Label;
<TestText Text=`Label` />
```

When the parent sets `Label = "world"`, the child's `Label` property updates automatically because they share the same `Reference<string>` object.

Default accessibility is `private` (unlike regular refs which default to `public`).

### Constraints

- `provide`/`inject` cannot be `static` or `required`.
- `provide`/`inject` cannot use computed syntax (`=>`).
- `inject` does not support default values.
- Must enable new lifecycle for entire project `[assembly: QuickMarkupNewLifecycle]`

### Generated Code

A `provide string Label = "hello";` declaration generates:

```cs
// Backing field (same as regular ref)
private Reference<string> LabelProp => field ??= new Reference<string>(value, "MyComponent.Label");

// Property
public string Label {
    get => this.LabelProp.Value;
    set => this.LabelProp.Value = value;
}

// In generated constructor (runs before user's [QuickMarkupConstructor] method):
Context.Provide<string>("Label", LabelProp);
```

A `provide string MyRef as MyCtx = "hello";` declaration generates:

```cs
// Backing field uses local name (MyRef)
private Reference<string> MyRefProp => field ??= new Reference<string>(value, "MyComponent.MyRef");

// Property uses local name
public string MyRef {
    get => this.MyRefProp.Value;
    set => this.MyRefProp.Value = value;
}

// In generated constructor: context key uses the alias (MyCtx)
Context.Provide<string>("MyCtx", MyRefProp);
```

An `inject string Label;` declaration generates:

```cs
// Backing field initialized to null!
private Reference<string> LabelProp = null!;

// Property
public string Label {
    get => this.LabelProp.Value;
    set => this.LabelProp.Value = value;
}

// In generated constructor (runs before user's [QuickMarkupConstructor] method):
LabelProp = Context.Inject<string>("Label");
```

An `inject? string Label;` declaration generates:

```cs
// Backing field initialized to null (nullable)
private Reference<string>? LabelProp = null;

// Property (returns default if not injected)
public string Label {
    get => LabelProp is not null ? LabelProp.Value : default(string);
    set { if (LabelProp is not null) LabelProp.Value = value; }
}

// In generated constructor:
LabelProp = Context.TryInject<string>("Label");
```

An `inject string MyCtx as MyRef;` declaration generates:

```cs
// Backing field uses local name (MyRef)
private Reference<string> MyRefProp = null!;

// Property uses local name
public string MyRef {
    get => this.MyRefProp.Value;
    set => this.MyRefProp.Value = value;
}

// In generated constructor: context key uses the original name before `as` (MyCtx)
MyRefProp = Context.Inject<string>("MyCtx");
```

### Context Hierarchy

Contexts form a hierarchy: grandparent → parent → child. A child component can find providers from any ancestor. The `QuickMarkupContext` walks up the parent chain when resolving an injection.

## Setup

Setup is a place to define C# code to be run before UI is generated. UI will have access to any variables declared in setup tag, but these variables will not be exported outside this scope.

```cs
<setup>
string[] options = ["Apple", "Orange", "Banana"];
</setup>
<root>
    // UI will have access to these variables
    <ComboBox ItemsSource=`options` />
</root>
```

## UI

### QuickMarkup general syntax

```xml
<UIClassName Property1=Value Property2=Value
    // ...
>
    // Children
    <Child />
    <Child />
    <Child />
</UIClassName>
```

Self-closing tag is also supported

```xml
<UIClassName Property1=Value Property2=Value />
```

You can also give a name, will be set as backing field.

```xml
myUIElement = <UIClassName Property1=Value Property2=Value />
```

Use `ref` when you need the variable to be a reactive reference (starts null, set after element creation):

```cs
ref InputLabel = <TextBlock Text="subtitle" />
```

Useful for cross-referencing elements (e.g., `` AutomationProperties.LabeledBy=`InputLabel` ``) backward. Without `ref`, reading the variable before the element is created returns `null`. Prefer plain capture unless cross-referencing is needed.

Example:
```cs
<TextBox AutomationProperties.LabeledBy=`InputLabel`/>
ref InputLabel = <TextBlock AutomationProperties.AccessibilityView=Raw Text="subtitle" />
```

### Comments

Supported comments are in `//` and `/* */` style.

Note:`<!-- -->` is not supported.

### QuickMarkup Primitive Values

QuickMarkup supports following value kinds.

```cs
// Integer
123456
0xDEADBEEF
0b101101
// Double
123.456
// Boolean
true
false
// String
"Hello World"
// Like C# default, which evaluates to null or default, uninitialized struct
default
// null
null
// In some context, identifier is supported as enum value
Center
// C# literal, can be any valid C# expression
`string.IsNullOrEmpty(x) ? "Empty String" : x`
// Older syntax of C# literal, provided for backward compatability and in case if ` needs to be used inside C#.
/-string.IsNullOrEmpty(x) ? "Empty String" : x-/
```

> [!WARNING]  
> Invalid C# expression inside C# literal may result in an undefined behavior in compiled code. In most cases, it would not compile.

### QuickMarkup Properties

Unlike HTML/XML/XAML, in QuickMarkup, values that are not string are not enclosed in "double quotes" around property syntax.

Just `PropertyName=Value`.

```cs
<setup>
string[] options = ["Apple", "Orange", "Banana"];
</setup>
<root>
    <ComboBox ItemsSource=`options`
        // 0 is not enclosed in quotes.
        SelectedIndex=0
        // For enum values, the enum member name can be used
        HorizontalAlignment=Center
        // Value alone will be treated as true
        // Equivalent to IsEnabled=true
        IsEnabled
        // Equivalent to IsHitTestVisible=false
        !IsHitTestVisible
        // Events can be declared with +=
        SizeChanged+=`(_, _) => Debug.WriteLine("ComboBox was resized.")`
        // With @ symbol, it will automatically wrap in delegates
        @SelectionChanged+=`Debug.WriteLine("User has chanegd item.")`
    />
</root>
```

#### Automatic `new` for one-parameter constructors

For **non-string** literals (`int`, `double`, `bool`, and similar), the binder does not always paste the raw token into the assignment. If the **property type** does not accept the literal directly, but that type has a **constructor with exactly one parameter** whose type **does** accept the literal (according to the generator’s `CanAssign` rules), the emitter wraps the value as `new FullTypeName(literal)`. This is implemented as `Binder.ValueOrAutoNew` plus `CodeTypeResolver.ShouldAutoNew`.

Typical WinUI / UWP examples:

- `CornerRadius=16` on `Border` → emitted like `new global::Windows.UI.Xaml.CornerRadius(16)` (uniform radius from a numeric literal).
- `BorderThickness=1` → emitted like `new global::Windows.UI.Xaml.Thickness(1)` when the single “uniform length” constructor applies.

**Limits:**

- Only **one-parameter** constructors are considered. For `Thickness` padding/margin with **four** components, literals like `"0,12,0,0"` are strings in XAML but are **not** the same in QuickMarkup; use a **backtick C# expression** instead, e.g. `` Padding=`new(0,12,0,0)` `` or `` Padding=`new Thickness(0, 12, 0, 0)` ``.
- `CanAssign` does **not** model every implicit conversion; if something does not compile, use an explicit `` `expression` ``.

When a plain number is enough for the property (as with uniform `CornerRadius`), you can **omit** `` `new CornerRadius(16)` `` and write `CornerRadius=16`.

#### C# literals

Using `` PropertyName=`csharp expression` `` syntax, the expression will be rerun whenever any QuickMarkup reactive dependencies used are updated.

```cs
string SelectedOption;
<setup>
string[] options = ["Apple", "Orange", "Banana"];
</setup>
<root>
    <StackPanel>
        <ComboBox ItemsSource=`options`
            SelectionChanged+=`(sender, _) => SelectedOption = (string)((ComboBox)sender).SelectedValue`
        />
        <TextBlock Text=`$"{SelectedOption} was selected."` />
    </StackPanel>
</root>
```

Any time the user selects a new item in ComboBox, the TextBlock's text will be updated.

#### Bindback

You can bind the variable backward by using `` Property=>`TargetVariable` `` instead.

```cs
string SelectedOption;
<setup>
string[] options = ["Apple", "Orange", "Banana"];
</setup>
<root>
    <StackPanel>
        <ComboBox ItemsSource=`options` SelectedValue=>`SelectedOption` />
        <TextBlock Text=`$"{SelectedOption} was selected."` />
    </StackPanel>
</root>
```

Any time the user selects a new item in ComboBox, the backing ref for `SelectedOption` will be updated.

#### TwoWay binding

You can bind the variable two-way by using `` Property<=>`TargetVariable` `` instead.

```cs
string SelectedOption;
<setup>
string[] options = ["Apple", "Orange", "Banana"];
</setup>
<root>
    <StackPanel>
        // These two comboboxes will select the same object after the user changes any of them
        <ComboBox ItemsSource=`options` SelectedValue<=>`SelectedOption` />
        <ComboBox ItemsSource=`options` SelectedValue<=>`SelectedOption` />
    </StackPanel>
</root>
```

You can also preprocess the value by using `` Property=`preprocessed` `` and `` Property=>`Variable` `` as well.

```cs
double Value;
<root>
    // when set from external source, rounds to two decimal places
    // when user types the number, use any number they type
    <NumberBox Value=`Math.Round(Value, 2)` Value=>`Value` />
</root>
```

#### QuickMarkup Tags inside QuickMarkup tag.

You can use QuickMarkup tags as property value.

```cs
using Windows.Globalization.NumberFormatting;

double Value = 0;
double Minimum = 0;
double Maximum = 0;
double Step = 1;
<root>
    <NumberBox Minimum=`Minimum` Maximum=`Maximum` Value<=>`Value`
        NumberFormatter=<DecimalFormatter
            IntegerDigits=1
            FractionDigits=`-(int)Math.Floor(Math.Log10(Step))`
            NumberRounder=<IncrementNumberRounder
                Increment=`Step`
                RoundingAlgorithm=RoundHalfUp
            />
        />
    />
</root>
```

The `<> ... </>` syntax can be used as a value list for property values, for example:

```cs
<Grid RowDefinitions=<>
    <RowDefinition />
    <RowDefinition />
</> />
```

#### Special Callbacks

Identifier being alone, if it is identified as not a valid property variable, will be called as an extension method.

```cs
// UIExtension.cs
static class UIExtension {
    public static void CenterH(this FrameworkElement element)
    {
        element.HorizontalAlignment = HorizontalAlignment.Center;
        return element;
    }
    public static void CenterV(this FrameworkElement element)
    {
        element.VerticalAlignment = VerticalAlignment.Center;
        return element;
    }
}
```

```cs
int Counter;
<root>
    <StackPanel CenterH CenterV>
        <Button CenterH Text="Click Me" />
        <TextBlock CenterH
            Text=`$"You clicked {Counter} time(s)"`
            Visibility=`Counter > 0 ? Visibility.Visible : Visbility.Collapsed`
        />
    </StackPanel>
</root>
```

Foreign value being alone will be treated as a callback function. Expects the C# expression to be of `Action<T>` where `T` is the type of the element.

It will be evaluated immediately once with input being the object created. This feature is for advanced usage where QuickMarkup does not support yet.

```cs
<Grid CenterH `x => {
    x.RowDefinitions.Add(new() { Height = GridLength.Auto });
    x.RowDefinitions.Add(new());
}`>
    <Child CenterH `x => Grid.SetRow(x, 0)` />
    <Child CenterH `x => Grid.SetRow(x, 1)` />
</Grid>
```

#### Order of evaluations for properties

On object initialization, QuickMarkup properties are evaluated in order they are defined. As references change, specific properties will be reevaluated in no particular order.

#### Root Tag With Properties

`<root>` can carry properties that apply to the class itself (since the class inherits from a UI type):

```cs
<root Background=`bgBrush` CornerRadius=16 Margin=16 Padding=8 />
```

### QuickMarkup Children

As seen in previous examples, QuickMarkup can declare nested statemnts like HTML or XAML does.

```cs
string? SelectedOption = null;
<setup>
string[] options = ["Apple", "Orange", "Banana"];
</setup>
<root>
    <StackPanel>
        <TextBlock Text="Select an item" />
        <ComboBox ItemsSource=`options` SelectedValue=>`SelectedOption` />
        <TextBlock Text=`$"You selected {SelectedOption}."` IsVisible=`SelectedOption is not null` />
    </StackPanel>
</root>
```

#### Fragment children

A `{ ... }` block is a fragment. It can contain any valid QuickMarkup child node, including elements, nested fragments, `if`, and `foreach`.

```cs
<root>
    <StackPanel>
        {
            <TextBlock Text="A" />
            <TextBlock Text="B" />
        }
    </StackPanel>
</root>
```

#### Conditional children

QuickMarkup supports `if` and `if`/`else` as child nodes. The body can be a single child node or a `{ ... }` fragment.

```cs
bool ShowDetails = true;
<root>
    <StackPanel>
        if (`ShowDetails`) {
            <TextBlock Text="Details" />
            <Button Content="Close" />
        }
        else
            <TextBlock Text="Summary" />
    </StackPanel>
</root>
```

For single-child content positions, such as `Content`, conditional content requires an `else` branch and each branch must resolve to exactly one child.

#### Foreach loop

> [!WARNING]  
> This is the beta features. They may be changed in the future without notice.

To assist in development of repeated UI, simple loops are offered in QuickMarkup. The features

##### Loop over ranges

Ranges are declared with `start..end` or `..end` syntax where `start` and `end` represents QuickMarkup integers. Lower bound is inclusive (or 0 if not explicitly stated), and upper bound is exclusive.

> [!INFO]
> C# expression is not supported in range syntax. For example, `` 5..`isLong ? 20 : 10` `` is not supported.

```cs
<root>
    <StackPanel>
        // Row 1, Row 2, Row 3
        foreach (var row in ..3) {
            <TextBlock Text=`$"Row {row + 1}"` />
        }
        // Row 4, Row 5, Row 6
        foreach (var row in 4..7) {
            <TextBlock Text=`$"Row {row}"` />
        }
    </StackPanel>
</root>
```

##### Loop over iterables

Iterable loops are backed by reactive collection blocks when the source collection implements `INotifyCollectionChanged`, such as `ObservableCollection<T>`. When that collection changes, QuickMarkup reconciles the generated children. Plain arrays and other non-notifying enumerables can still be rendered initially, but they will not notify QuickMarkup about later insert, remove, replace, or move operations.

```cs
<setup>
ObservableCollection<string> animals = ["Dog", "Cat", "Tiger"];
</setup>
<root>
    <StackPanel>
        foreach (var animal in `animals`) {
            <TextBlock Text=`animal` />
        }
    </StackPanel>
</root>
```

For stable identity across collection changes, provide a key expression after a semicolon. Key must be unqiue (otherwise there will be a thrown exception).

```cs
// C#
record class Animal(int Id, string Name);
```

```cs
<setup>
ObservableCollection<Animal> animals = [
    new(1, "Dog"),
    new(2, "Dog"),
    new(3, "Cat"),
    new(4, "Tiger")
];
</setup>
<root>
    <StackPanel>
        foreach (var animal in `animals`; `animal.Id`) {
            <TextBlock Text=`animal.Name` />
        }
    </StackPanel>
</root>
```

You can also request an index reference:

```cs
<setup>
ObservableCollection<string> animals = ["Dog", "Cat", "Tiger"];
</setup>
<root>
    <StackPanel>
        // declare a new variable named "index"
        foreach (index; var animal in `animals`) {
            <TextBlock Text=`$"{index + 1}. {animal}"` />
        }
    </StackPanel>
</root>
```

or both provide a key and request references:

```cs
<setup>
<setup>
ObservableCollection<Animal> animals = [
    new(3, "Dog"),
    new(4, "Dog"),
    new(5, "Cat"),
    new(6, "Tiger")
];
</setup>
<root>
    <StackPanel>
        // index variable declaration goes in the front
        // keys goes in the back
        foreach (index; var animal in `animals`; `animal.Id`) {
            <TextBlock Text=`$"{index + 1}. {animal.Name}"` />
        }
    </StackPanel>
</root>
```

The key expression must be a C# literal expression in (backtick expression or legacy syntax).

## QuickMarkup Components

In case that you cannot subclass components, or wish to write component that returns multiple elements. QuickMarkup defines two interfaces for creating reusable UI components: `IQuickMarkupComponent<T>` and `IQuickMarkupFragmentComponent<T>`.

For WinUI/UWP, instead of above recommendation to subclass, we usually prefer using `IQuickMarkupComponent<T>` instead of subclassing, since subclassing without XMAL can trigger multiple bugs, especially on top of styled or templated control.

### Single Child component

Use `IQuickMarkupComponent<T>` when the component produces exactly **one** UI element.

```cs
[QuickMarkup("""
    string Text = "";
    <root>
        <TextBlock Text=`Text` FontSize=16 />
    </root>
    """)]
public partial class Label : IQuickMarkupComponent<TextBlock>;

[QuickMarkup("""
    string Text = "";
    // you may also omit <root> tag on components
    <TextBlock Text=`Text` FontSize=16 />
    """)]
public partial class Label2 : IQuickMarkupComponent<TextBlock>;
```

**Consuming a single component from another QuickMarkup class:**

```cs
[QuickMarkup("""
    <root>
        <Label Text="Hello" HorizontalAlignment=Center />
        <Label Text="World" FontSize=24 />
    </root>
    """)]
public partial class MyPage : StackPanel;
```

Properties set on `<Label>` that don't exist on the `Label` class are **forwarded** to its `MarkupNode` (the `TextBlock`). So `HorizontalAlignment=Center` becomes `MarkupNode.HorizontalAlignment = Center`.

Note: In C# code, remaining properties are not forwarded:

```csharp
Label label = new Label();

label.Text = "Hello"; // this is defined on component, can access
label.MarkupNode.HorizontalAlignment = HorizontalAlignment.Center; // but remaining ones, you should add .MarkupNode

Children.Add(label.MarkupNode); // need to manually specify markup node here
```

### Multiple Child or Fragment Component

Use when the component produces **multiple** UI elements.

```cs
[QuickMarkup("""
    <root>
        <TextBlock Text="Item A" />
        <TextBlock Text="Item B" />
        <TextBlock Text="Item C" />
    </root>
    """)]
public partial class ItemList : IQuickMarkupFragmentComponent<TextBlock>;

[QuickMarkup("""
    // again, <root> tag can be omitted
    <TextBlock Text="Item A" />
    <TextBlock Text="Item B" />
    <TextBlock Text="Item C" />
    """)]
public partial class ItemList2 : IQuickMarkupFragmentComponent<TextBlock>;
```

Note: subclassing regular UI still requires `<root>` tag if you have UI markup. Only QuickMarkup components may omit root tags and have non-root tag directly.

**Consuming a fragment component from another QuickMarkup class:**

```cs
[QuickMarkup("""
    <root>
        <ItemList />
        <TextBlock Text="Footer" />
    </root>
    """)]
public partial class MyPage : StackPanel;
```

The `<ItemList />` expands inline — all three `TextBlock` children from the fragment component appear inside the `StackPanel` alongside the footer.

### Limitations

- A class may implement at most **one** of the two interfaces. Implementing both produces a compile-time error (`QMBinderMultipleComponentInterfacesError`).

## Bootstrapping

For new WinUI/UWP projects, initialize the reactive scheduler once on the UI thread before any QuickMarkup page is created:

```csharp
public MainWindow()
{
    this.InitializeComponent();
    QuickMarkup.WinUI.ReactiveInitializer.InitReactiveScheduler();
}
```

For other frameworks, adapt the pattern — the scheduler needs a periodic tick on the UI thread:

```csharp
ReactiveScheduler.AddTickCallbackForCurrentThread(delegate
{
    _ = Dispatcher.TryRunAsync(CoreDispatcherPriority.High, ReactiveScheduler.Tick);
});
```

## Reactivity Infrastructure (C# Code-Behind)

For advanced use outside markup:

```csharp
var r = Ref(0);                      // Reference<int>
var c = Computed(() => r.Value + 1);  // Computed<int>
r.Watch(val => { ... });             // callback on change
r.Watch(val => { ... }, immediate: true); // also runs immediately
Effect(() => { ... }, ref1, ref2);   // runs when any listed ref changes
```

`ReferenceTracker.NoCapture(() => expr)` reads a reference without tracking dependencies.

## QuickMarkup Packages (WinUI / UWP)

NuGet: `QuickMarkup.WinUI` (WinUI 3), `QuickMarkup.UWP` (UWP)

- **`ReactiveInitializer.InitReactiveScheduler()`** — call once on the UI thread.
- **`ThemeResources.Get<T>(string resourceName)`** — returns a `Reference<T?>` that re-resolves on theme change.
- **`ThemeBrushes`** — static properties for common brushes (`Accent`, `PrimaryText`, `SolidBackground`, `CardBackground`, `DividerStroke`, `SystemSuccess`, etc.).

Usage in markup:

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
partial class MyPage : Page;
```

## Best Practices

- Define **global usings** for common namespaces (`QuickMarkup.Infra`, `static QuickMarkup.Infra.QuickRefs`) to keep markup clean.
- Write **extension methods** for layout shortcuts (`CenterH`, `CenterV`, `StretchH`, etc.).
- The class must be `partial`. Base class should be a UI element (`Page`, `Grid`, `StackPanel`) or implement `IQuickMarkupComponent<T>` / `IQuickMarkupFragmentComponent<T>`.
- Prefer subclassing UI elements directly over component interfaces when possible (avoids property-forwarding overhead).

