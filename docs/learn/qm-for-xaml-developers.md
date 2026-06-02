# QuickMarkup for XAML Developers

If you already know XAML, QuickMarkup will feel both familiar and strangely freeing.

QuickMarkup is a reactive UI language for .NET native desktop apps (WinUI/UWP) that replaces much of the traditional XAML + MVVM stack with reactive C#-based markup and compile-time code generation.

Instead of writing:

* XAML views
* `INotifyPropertyChanged`
* converters
* view models
* dependency-property glue
* binding-path strings

...you write reactive UI directly in C# markup.

The underlying UI platform is still native WinUI/UWP, so your existing knowledge of controls, layouts, panels, dependency properties, styling, and platform APIs still applies.

What changes is the authoring model.

If you think of it as:

> “XAML reimagined with reactive C# instead of MVVM bindings”

...you'll be very close to the intended mental model.

---

## A complete minimal example

A QuickMarkup component usually looks like this:

```csharp
[QuickMarkup("""
    // reactive state
    int Count = 0;

    <StackPanel Spacing=12>
        <TextBlock Text=`$"Count: {Count}"` />

        <Button
            Content="Increment"
            @Click+=`Count++`
        />
    </StackPanel>
    """)]
public partial class CounterPage : Page
{
    public CounterPage()
    {
        Init();
    }
}
```

Things to notice immediately:

* UI is written directly inside a C# attribute
* Variables declared at the top become reactive automatically
* Backticks `` `...` `` contain reactive C# expressions
* Updating `Count` automatically refreshes dependent UI
* The UI still uses native WinUI/UWP controls
* No `{Binding}`
* No `INotifyPropertyChanged`
* No view model boilerplate

The generated code handles the reactive infrastructure automatically.

QuickMarkup intentionally resembles a mix of:

* XAML-style UI declaration
* reactive bindings
* direct C# expressions
* declarative rendering

However, unlike XAML, the syntax is fundamentally C#-oriented.

For example, comments use normal C-style syntax:

```csharp
// single-line comment

/* multi-line
   comment */
```

Unlike XML/XAML, `<!-- -->` comments are not supported inside QuickMarkup markup.

---

## Important differences from XAML

Although QuickMarkup uses native WinUI/UWP controls, the programming model is very different from traditional XAML + MVVM.

### No binding-path strings

XAML bindings are string-based:

```xml
<TextBlock Text="{Binding User.Name}" />
```

QuickMarkup bindings are direct C# expressions:

```csharp
<TextBlock Text=`User.Name` />
```

This means:

<!-- * full IntelliSense/refactoring - not true for now -->
* compile-time checking
* normal C# semantics
* no runtime binding-path lookup

Bindings are expressions, not strings interpreted by a runtime binding engine.

---

### No `INotifyPropertyChanged`

In XAML MVVM, state usually looks like this:

```csharp
private string _name;

public string Name
{
    get => _name;
    set
    {
        _name = value;
        OnPropertyChanged();
    }
}
```

In QuickMarkup:

```csharp
// QuickMarkup
string Name = "";
<TextBlock Text=`Name` />
```

That generates a property that automatically becomes a reactive reference.

Changing value:

```csharp
Name = "QuickMarkup";
```

automatically updates dependent UI.

No manual setter implementation.
No property-changed boilerplate.
No base view model classes.

---

### No converters for most cases

XAML often requires converter classes for simple transformations:

```xml
<TextBlock
    Text="{Binding Price,
        StringFormat={}{0:C}}"
/>
```

Or:

```xml
<TextBlock
    Visibility="{Binding IsVisible,
        Converter={StaticResource BoolToVis}}"
/>
```

QuickMarkup usually just uses inline C#:

```csharp
<TextBlock Text=`$"{Price:C}"` />
```

```csharp
<TextBlock
    Visibility=`IsVisible
        ? Visibility.Visible
        : Visibility.Collapsed`
/>
```

Because bindings are expressions rather than string paths, most lightweight conversions no longer need dedicated converter types.

---

### No `DataContext` mental overhead

XAML heavily relies on inherited `DataContext` scopes.

QuickMarkup does not.

Bindings directly reference properties in lexical scope:

```csharp
string Name = "Alice";

<TextBlock Text=`Name` />
```

Inside loops:

```csharp
foreach (var item in `Items`) {
    <TextBlock Text=`item.Name` />
}
```

There is no implicit binding-context switching happening behind the scenes.

The scoping rules behave much more like normal C# code.

---

### No visual designer or hot reload

QuickMarkup relies heavily on compile-time source generation.

The tradeoff is:

* less runtime overhead
* fewer reflection-based systems
* direct generated UI code
* tighter integration with C#

...but currently without the mature visual designer workflow traditionally associated with XAML tooling.

---

## Reactive state → References

QuickMarkup variables map conceptually to reactive references.

```xml
<!-- XAML -->
<TextBlock Text="{Binding Count}" />
```

```csharp
// QuickMarkup
int Count = 0;

<TextBlock Text=`$"Count: {Count}"` />
```

| XAML / MVVM                      | QuickMarkup      |
| -------------------------------- | ---------------- |
| property + `OnPropertyChanged()` | `int Count = 0;` |
| `{Binding Name}`                 | `` `Name` ``     |
| `IValueConverter`                | inline C#        |
| `Mode=TwoWay`                    | `<=>`            |
| `Mode=OneWayToSource`            | `=>`             |
| `DataTemplate`                   | `foreach`        |
<!-- | `x:Name`                         | variable capture | -->

Under the hood, QuickMarkup generates reactive backing infrastructure automatically.

A declaration like:

```csharp
int Count = 0;
```

roughly generates a reactive backing reference and property accessors behind the scenes.

This means:

```csharp
Count++;
```

would trigger updates propagation to dependent bindings and computed values.

No `DependencyProperty`.
No `INotifyPropertyChanged`.
No manual notification wiring.

---

## Computed values

QuickMarkup computed values behave similarly to derived properties in MVVM — except dependency tracking is automatic.

```csharp
double Total => `Price * Quantity`;
```

Computed values:

* cache their results
* automatically track dependencies
* reevaluate only when dependencies change

```csharp
<TextBlock Text=`$"{Total:C}"` />
```

Unlike XAML bindings, there is no runtime binding engine parsing property paths and observing dependencies dynamically.

The generated infrastructure tracks dependencies directly through reactive references and computed values.

---

## Binding directions

XAML exposes several binding modes.

QuickMarkup makes them explicit in the syntax.

```xml
<!-- XAML -->
<TextBox Text="{Binding Name}" />

<TextBox Text="{Binding Name, Mode=TwoWay}" />

<TextBox Text="{Binding Name, Mode=OneWayToSource}" />
```

```csharp
// QuickMarkup

// source → UI
<TextBox Text=`Name` />

// two-way binding
<TextBox Text<=>`Name` />

// UI → source
<TextBox Text=>`Name` />
```

Unlike XAML bindings, these are still just reactive C# expressions — not binding-path strings.

You can also preprocess values inline:

```csharp
<NumberBox
    Value=`Math.Round(Value, 2)`
    Value=>`Value`
/>
```

This allows one direction to transform values differently from the reverse direction without requiring converters or custom binding classes.

---

## Conditional rendering

XAML has no direct equivalent to inline structural conditional rendering.

QuickMarkup uses actual control flow syntax:

```csharp
if (`IsLoggedIn`) {
    <TextBlock Text="Welcome back" />
}
else {
    <Button
        Content="Log in"
        @Click+=`Login()`
    />
}
```

This behaves much more like normal C# code than XAML triggers or template switching.

Whenever dependencies change, QuickMarkup updates only the affected generated UI blocks.

---

## List rendering

XAML list rendering usually requires:

* `ItemsControl`
* `ItemsSource`
* `DataTemplate`
* binding contexts

QuickMarkup instead uses `foreach`.

```xml
<!-- XAML -->
<ItemsControl ItemsSource="{Binding Items}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

```csharp
// QuickMarkup
foreach (var item in `Items`) {
    <TextBlock Text=`item.Name` />
}
```

No `DataTemplate`.
No implicit `DataContext`.
No binding-path strings.

Just a loop.

When the collection implements `INotifyCollectionChanged` (such as `ObservableCollection<T>`), QuickMarkup incrementally reconciles the generated UI as items are added, removed, or moved.

You can also provide stable keys:

```csharp
foreach (var item in `Items`; `item.Id`) {
    <TextBlock Text=`item.Name` />
}
```

And optional indices:

```csharp
foreach (index; var item in `Items`) {
    <TextBlock
        Text=`$"{index + 1}. {item.Name}"`
    />
}
```

---

## Event handling

XAML events:

```xml
<Button Click="OnClick" />
```
```csharp
private void OnClick(object? sender, RoutedEventArgs e)
{
    Count++;
}
```

QuickMarkup event syntax:

```csharp
// QuickMarkup
<Button @Click+=`Count++` />
```

Or explicitly:

```csharp
// QuickMarkup
<Button
    Click+=`(sender, args) => Count++`
/>
```

The `@` shorthand automatically wraps the expression in a delegate.

This keeps inline event expressions concise while still compiling to normal .NET events.

Because QuickMarkup targets native WinUI/UWP controls directly, these are regular platform events — not synthetic framework abstractions like many web frameworks.

---

## Element references

XAML uses `x:Name`:

```xml
<Button x:Name="myButton" />
```

QuickMarkup captures the element into a variable:

```csharp
// QuickMarkup
myButton = <Button />
```

The captured variable becomes a field on the generated class and is available from code-behind after initialization.

```csharp
myButton.Content = "Updated";
```

For forward or cross references, use `ref`:

```csharp
// QuickMarkup
<TextBox
    AutomationProperties.LabeledBy=`InputLabel`
/>

ref InputLabel =
    <TextBlock Text="Subtitle" />
```

The variable remains `null` until the element has been created. `ref` creates a backing `Reference<TextBlock>`, ensuring `` AutomationProperties.LabeledBy=`InputLabel` `` updated the value once the elment is created.

Initialization order matters because UI is generated top-to-bottom.

---

## Watchers and effects

Sometimes you want reactive side effects outside the UI itself.

QuickMarkup exposes low-level reactive APIs directly:

```csharp
// C#
CountProp.Watch(v =>
    Console.WriteLine(v));
```

Or multiple dependencies:

```csharp
// C#
Effect(
    () => Console.WriteLine($"{First} {Last}"),
    FirstProp,
    LastProp
);
```

Unlike XAML bindings, these are not part of a separate binding engine.

They are direct reactive subscriptions over generated references and computed values.

---

## Components

QuickMarkup supports reusable declarative components.

```csharp
// QuickMarkup
[QuickMarkup("""
    string Text = "";

    <TextBlock
        Text=`Text`
        FontSize=16
    />
    """)]
public partial class Label
    : IQuickMarkupComponent<TextBlock>;
```

Usage:

```csharp
<Label Text="Hello" />
```

Properties declared at the top automatically become reactive component properties.

Additional properties on the component tag are forwarded to the root markup node:

```csharp
<Label
    Text="Hello"
    HorizontalAlignment=Center
/>
```

This behaves similarly to setting attached or forwarded properties on a root XAML element.

Unlike XAML user controls, this does not have limitation of creating component on top of sealed types.

---

## Mental model comparison

| Concept             | XAML / MVVM                   | QuickMarkup                       |
| ------------------- | ----------------------------- | --------------------------------- |
| UI declaration      | XAML                          | C# markup                         |
| Binding system      | Runtime binding engine        | Generated reactive bindings       |
| State updates       | `INotifyPropertyChanged`      | Reactive references               |
| Binding expressions | String paths                  | Direct C#                         |
| Value conversion    | `IValueConverter`             | Inline expressions                |
| Templates           | `DataTemplate`                | `foreach` / fragments             |
| Conditional UI      | Triggers / template switching | `if` / `else`                     |
| Rendering           | Dependency-property system    | Direct generated property updates |
| Code organization   | XAML + ViewModel              | Unified reactive component        |
| Dependency tracking | Runtime                       | Generated reactive infrastructure |
| UI target           | Native WinUI/UWP              | Native WinUI/UWP                  |

QuickMarkup intentionally borrows many of the ergonomic goals people wanted from MVVM:

* declarative UI
* automatic UI updates
* separation of state and presentation
* reusable components

...while removing much of the ceremony traditionally associated with XAML binding infrastructure.

If you already think in terms of:

* native WinUI/UWP controls
* declarative UI
* reactive state
* dependency-driven updates

...then most of QuickMarkup will feel immediately familiar — just significantly more direct.
