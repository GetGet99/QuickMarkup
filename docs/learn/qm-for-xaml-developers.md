# QuickMarkup for XAML Developers

If you already know XAML and MVVM, QuickMarkup should feel familiar — just with a more direct reactive model.

QuickMarkup is a reactive UI language for WinUI/UWP that lets you build UI using declarative C# markup instead of relying heavily on XAML bindings, converters, and `INotifyPropertyChanged`.

You still work with:

* native WinUI/UWP controls
* panels and layouts
* dependency properties
* styles and resources
* existing control libraries

Your existing knowledge of WinUI/UWP still applies directly.

What changes is how UI is authored.

Instead of splitting UI between:

* XAML
* view models
* converters
* `INotifyPropertyChanged`
* binding strings

QuickMarkup keeps UI, state, and logic together in reactive C# markup.

Reactive expressions automatically track the state they use and refresh when that state changes.

QuickMarkup may not be ideal if your workflow depends heavily on:

* visual designers
* strict MVVM separation
* XAML-specific tooling workflows

---

# Quick Start

A minimal QuickMarkup page looks like this:

```csharp
[QuickMarkup("""
    int Count = 0;

    <StackPanel Spacing=12 Padding=24>

        <Button
            Content="Increment"
            @Click+=`Count++`
        />

        <TextBlock
            Text=`$"Count: {Count}"`
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

When `Count` changes, the `TextBlock` automatically updates.

No:

* `INotifyPropertyChanged`
* view model
* binding strings
* converters

The generated code handles the reactive infrastructure automatically.

---

# Before Continuing

If you already know XAML, these are the most important syntax mappings:

| XAML                  | QuickMarkup             |
| --------------------- | ----------------------- |
| `{Binding Name}`      | `` `Name` ``            |
| `Mode=TwoWay`         | `<=>`                   |
| `Mode=OneWayToSource` | `=>`                    |
| `Click="OnClick"`     | `` @Click+=`...` ``     |
| Item templates        | `foreach` blocks        |
| `IValueConverter`     | inline C#               |
| `x:Name`              | `myButton = <Button />` |

Most QuickMarkup code is just:

* normal WinUI controls
* familiar C# properties and expressions
* normal C#
* reactive expressions

---

# Reactive State

Variables declared at the top of a QuickMarkup block automatically become reactive state.

```csharp
string Name = "Alice";

<TextBlock Text=`Name` />
```

Expressions inside `` `backticks` `` automatically track the reactive values they use and refresh when those values change.

Updating the value:

```csharp
// C#
Name = "Bob";
```

automatically refreshes any UI using it.

QuickMarkup generates the reactive backing infrastructure automatically, so there is no need for:

* `INotifyPropertyChanged`
* manual property notification code
* binding strings for simple state updates

---

# Binding Values

In XAML:

```xml
<TextBlock Text="{Binding User.Name}" />
```

In QuickMarkup:

```csharp
<TextBlock Text=`User.Name` />
```

Bindings are normal C# expressions inside `` `backticks` `` syntax. You can use any valid C# expression:

```csharp
<TextBlock
    Text=`$"{Price:C}"`
/>
```

```csharp
<TextBlock
    Visibility=`IsVisible
        ? Visibility.Visible
        : Visibility.Collapsed`
/>
```

Any expression inside backticks automatically tracks the reactive values it reads and re-runs when those values change.
<!-- For many scenarios, this removes the need for converters entirely. -->

---

# Two-Way Binding

QuickMarkup supports one-way, one-way-to-source (bindback), and two-way binding.

```csharp
string SearchText = "";

<TextBox Text<=>`SearchText` />
```

Equivalent XAML:

```xml
<TextBox
    Text="{Binding SearchText, Mode=TwoWay}"
/>
```

The `<=>` operator keeps both values synchronized.

---

# Event Handling

XAML:

```xml
<Button Click="OnIncrementClick" />
```

```csharp
void OnIncrementClick(object sender, RoutedEventArgs e)
{
    Count++;
}
```

QuickMarkup:


```csharp
<Button
    Click+=`(sender, args) => {
        Count++;
    }`
/>
```

The `@` shorthand automatically wraps the expression in a delegate, allowing concise inline expressions without manually writing the lambda.

```csharp
<Button @Click+=`Count++` />
```

---

# Conditional UI

QuickMarkup uses normal control flow syntax.

```csharp
if (`IsLoggedIn`) {
    <TextBlock Text="Welcome back" />
}
else {
    <Button Content="Log in" />
}
```

This often replaces simple visibility converters and conditional template selection patterns.

The UI updates automatically whenever reactive dependencies change.

---

# Rendering Lists

Instead of `ItemsControl` + `DataTemplate`, QuickMarkup uses `foreach`.

XAML:

```xml
<ItemsControl ItemsSource="{Binding Items}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

QuickMarkup:

```csharp
foreach (var item in `Items`) {
    <TextBlock Text=`item.Name` />
}
```

You can also access indices:

```csharp
foreach (index; var item in `Items`) {
    <TextBlock
        Text=`$"{index + 1}. {item.Name}"`
    />
}
```

When using collections like `ObservableCollection<T>`, QuickMarkup automatically updates the generated UI when items are added or removed.

---

# Computed Values

Use `=>` to declare computed values.

```csharp
double Price = 10;
int Quantity = 3;

double Total => `Price * Quantity`;
```

Use them directly in UI:

```csharp
<TextBlock
    Text=`$"{Total:C}"`
/>
```

Computed values:

* cache automatically
* track dependencies automatically
* re-evaluate only when needed

---

# Element References

XAML uses `x:Name`:

```xml
<Button x:Name="myButton" />
```

QuickMarkup captures elements into variables:

```csharp
myButton = <Button />
```

You can access the element later from code-behind:

```csharp
myButton.Content = "Updated";
```

---

# Components

QuickMarkup supports reusable components.

```csharp
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

Component properties declared at the top automatically become reactive.

---

# Mental Model

QuickMarkup is easiest to understand if you think of it as:

* native WinUI/UWP controls
* declarative UI
* reactive state
* direct C# expressions

without the traditional XAML binding infrastructure.

The platform itself is still WinUI/UWP.

Your existing knowledge of:

* layouts
* controls
* styles
* dependency properties
* resources
* platform APIs

still applies directly.

What changes is the authoring model.
