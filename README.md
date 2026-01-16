# QuickMarkup (ALPHA)

QuickMarkup is a declarative markup language that makes it easier to declare UI in C# without using XAML. Our system relies on reactivity, rather than MVVM style. Some patterns are Vue-inspired.

Currently, our support is only WinUI 3. In the future, it is possible to decouple from WinUI 3 specific logic and can be used in general.

Disclaimer: this project is still in Alpha. Not recommended for production use yet.

## Introduction

QuickMarkup is divided into 3 main sections: Usings, setup, and UI.

```cs
using static QuickMarkup.Infra.QuickRefs;
<setup>
var counter = Ref(0);
</setup>
<root>
    <StackPanel>
        <Button Text="Click Me" />
        <TextBlock
            Text=/-$"You clicked {counter.Value} time(s)"-/
            Visibility=/-counter.Value > 0 ? Visibility.Visible : Visbility.Collapsed-/
        />
    </StackPanel>
</root>
```

Usings and setup part are optional. Root declares the place where your UI goes.

- Usings - declare all the imported namespace for variables and class you will use in `<setup>` and `<root>`
- Setup - the code that will be run before UI creation. The UI will have access to all those variables you declared in `<setup>` tag
- UI (`<root>`) - the part where your markup goes.

### Usage

Real usage will be put as an attribute on C# partial class

```csharp
[QuickMarkup("""
    using static QuickMarkup.Infra.QuickRefs;
    <setup>
    var counter = Ref(0);
    </setup>
    <root>
        <StackPanel HorizontalAlignment=Center VerticalAlignment=Center>
            <Button Text="Click Me" Click+=/-(o, e) => counter.Value++-/ />
            <TextBlock
                Text=/-$"You clicked {counter.Value} times"-/
                Visibility=/-counter.Value > 0 ? Visibility.Visible : Visbility.Collapsed-/
            />
        </StackPanel>
    </root>
    """)]
public partial class CounterPage : Page;
```

And that's it.

## General Syntax

Inside setup tag, the code must be valid C# statements.

### UI Syntax

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

### Comments

Oh yeah before anything else. Unlike XML and HTML that uses `<!-- -->` as comments, we'll use `//` and `/* */` for comments.

because we make our own parser and because we can :)

### Property Syntax

Unlike XAML/XML, we do not need to enclose everything in "quotes."

#### Supported Value Types

##### String

```xml
<UIClassName Property1="String" />
```

##### Number

```xml
<UIClassName PropertyInt=123 PropertyDouble=123.45 />
```

##### Boolean

```xml
<UIClassName
    // Explicit
    PropertyTrue1=true PropertyFalse1=false
    // A property name alone will be treated as true
    PropertyTrue2
    // A property name with excliamation in front will be treated as false
    !PropertyFalse2
/>
```

##### Enum

We do support enum.

```csharp
enum Animals {
    Cat, Dog, Tiger
}
class AnimalUI : UserControl {
    public Animals Animal { get => /* ... */; set => /* ... */; }
    /* ... */
}
```

You can type enum members name directly:

```xml
<StackPanel>
    <AnimalUI Animal=Cat />
    <AnimalUI Animal=Dog />
    <AnimalUI Animal=Tiger />
</StackPanel>
```

##### C# Expression

You can insert any C# expression with `/- csharp_expression -/` syntax. Note that your C# code cannot contain exactly the text `-/`, otherwise our parser will not work correctly.

```xml
<TextBlock
    Text=/-$"You clicked {counter.Value} times"-/
    Visibility=/-counter.Value > 0 ? Visibility.Visible : Visbility.Collapsed-/
/>
```

To declare events, use `+=`

```xml
<Button Text="Click Me" Click+=/-(o, e) => counter.Value++-/ />
```

##### Markup

Yes, you can use markup on property value if you really want to. Also, something special is, we don't care if your markup is not derived from UIElement.

```cs
<NumberBox
    NumberFormatter=<DecimalFormatter
        IntegerDigits=1
        FractionDigits=/- -(int)Math.Floor(Math.Log10(Step)) -/
        NumberRounder=<IncrementNumberRounder
            Increment=/-Step-/
            RoundingAlgorithm=RoundHalfUp
        />
    />
/>
```

### Special callback

Special callback allows for customizing functionality of the component. It is determined by `/- code block -/` being alone without any property name in front

```cs
<Grid HorizontalAlignment=Center /-x => {
    x.RowDefinitions.Add(new() { Height = GridLength.Auto });
    x.RowDefinitions.Add(new());
}-/>
    <Child HorizontalAlignment=Center /- x => Grid.SetRow(x, 0) -/ />
    <Child HorizontalAlignment=Center /- x => Grid.SetRow(x, 1) -/ />
</Grid>
```

It can also be an identifier being alone. Combining with extension method for best result.

```cs
static class Extension {
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
using static QuickMarkup.Infra.QuickRefs;
<setup>
var counter = Ref(0);
</setup>
<root>
    <StackPanel CenterH CenterV>
        <Button CenterH Text="Click Me" />
        <TextBlock CenterH
            Text=/-$"You clicked {counter.Value} time(s)"-/
            Visibility=/-counter.Value > 0 ? Visibility.Visible : Visbility.Collapsed-/
        />
    </StackPanel>
</root>
```

The catch is that the name must not be a property of that class, otherwise it will be treated as a boolean property.

```cs
// CenterH is treated as special callback variable.
// IsEnabled is treated as implicit true boolen because Button defines IsEnabled property.
<Button CenterH IsEnabled />
```

## Reactivity

Reactive variables will be the variables that QuickMarkup will track. Whenever the value changes, it will update the UI that uses that variable.

Simpliest units are `Ref`s. Let's go back to the first example,

```cs
using static QuickMarkup.Infra.QuickRefs;
<setup>
var counter = Ref(0);
</setup>
<root>
    <StackPanel HorizontalAlignment=Center VerticalAlignment=Center>
        <Button Text="Click Me" Click+=/-(o, e) => counter.Value++-/ />
        <TextBlock
            Text=/-$"You clicked {counter.Value} time(s)"-/
            Visibility=/-counter.Value > 0 ? Visibility.Visible : Visbility.Collapsed-/
        />
    </StackPanel>
</root>
```

When the button is first clicked, `counter.Value` will be changed from `0` to `1`. Then, `TextBlock` will get its `Text` updated to `You clicked 1 time(s)` and its `Visbility` to `true` automatically.

### Bind Back

There is also a way to "bind back" the value. Using the `=>` instead of `=`, we change the direction the value is assigned.

```cs
using static QuickMarkup.Infra.QuickRefs;
<setup>
var value = Ref(0.0);
</setup>
<root>
    <StackPanel HorizontalAlignment=Center VerticalAlignment=Center>
        // sets value.Value to Slider's Value and update whenever Slider's Value is updated.
        <Slider Value=>/-value.Value-/ />
        <TextBlock Text=/-$"Value: {value.Value}"-/ />
    </StackPanel>
</root>
```

Note that the initial value is ignored. If we were to set `var value = Ref(10.0);`, then it will get reset to 0 immedietely whenever slider is created and the value is set. Slider's default value will still be 0. Fortunately, we do have a workaround for setting default value.

```cs
using static QuickMarkup.Infra.QuickRefs;
<setup>
var value = Ref(0.0);
</setup>
<root>
    <Slider
        // sets Slider's Value to 5
        Value=5
        // then sets value.Value to Slider's Value and update whenever Slider's Value is updated.
        Value=>/-value.Value-/
    />
</root>
```

Without user interaction, slider's initial value (and `value.Value`) will be `5` instead.


Bind back is supported on DependencyProperty and Ref-powered property.

## Repeated section

To assist in development, we do have simple loops.

### Loop over number

```cs
<root>
    <StackPanel>
        // Row 1, Row 2, Row 3
        for (var row in ..3) {
            <TextBlock Text=/-$"Row {row + 1}"-/ />
        }
        // Also allows for starting and ending value
        // Row 4, Row 5, Row 6
        for (var row in 4..7) {
            <TextBlock Text=/-$"Row {row}"-/ />
        }
    </StackPanel>
</root>
```

### Loop over list

```cs
<setup>
string[] animals = ["Dog", "Cat", "Tiger"];
</setup>
<root>
    <StackPanel>
        for (var animal in animals) {
            <TextBlock Text=/-animal-/ />
        }
    </StackPanel>
</root>
```

Please note that if your list do change, the UI does not change. Support for changing list may be in the future, but our code is not that advanced yet. Maybe if someone want to contribute to the project.

## Get the most out of QuickMarkup

To get most out of QuickMarkup, here's the pattern that works for me.

### Define Extension properties

Latest C# is amazing at this. I know you cannot use these in XAML, but you can use them in QuickMarkup.

```cs
static class GridExtension {
    extension<T>(T element) where T : FrameworkElement
    {
        public int Grid_Row
        {
            get => Grid.GetRow(element);
            set => Grid.SetRow(element, value);
        }
        public int Grid_Column
        {
            get => Grid.GetColumn(element);
            set => Grid.SetColumn(element, value);
        }
        public int Grid_RowSpan
        {
            get => Grid.GetRowSpan(element);
            set => Grid.SetRowSpan(element, value);
        }
        public int Grid_ColumnSpan
        {
            get => Grid.GetColumnSpan(element);
            set => Grid.SetColumnSpan(element, value);
        }
    }
}
```

```cs
<Grid /- /* ... */ -/>
    // This will save you a lot of typing to /- x => Grid.SetRow(x, 0) -/, etc.
    <Child Grid_Row=0 Grid_Column=0 />
    <Child Grid_Row=0 Grid_Column=1 />
    <Child Grid_Row=1 Grid_Column=0 />
    <Child Grid_Row=1 Grid_Column=1 />
</Grid>
```

Another good one is this one. Seriously, if you want to use visiblity a lot, do this.

```cs
static class Extension {
    extension<T>(T element) where T : UIElement
    {
        public bool IsVisible
        {
            get => element.Visibility is Visibility.Visible;
            set => element.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
```

### Define Extension Methods

This will save you a lot of time for layout work.

```cs
static class Extension {
    extension<T>(T element) where T : FrameworkElement
    {
        public T Center()
        {
            element.HorizontalAlignment = HorizontalAlignment.Center;
            element.VerticalAlignment = VerticalAlignment.Center;
            return element;
        }
        public T CenterH()
        {
            element.HorizontalAlignment = HorizontalAlignment.Center;
            return element;
        }
        public T CenterV()
        {
            element.VerticalAlignment = VerticalAlignment.Center;
            return element;
        }
        public T Bottom()
        {
            element.VerticalAlignment = VerticalAlignment.Bottom;
            return element;
        }
        public T Right()
        {
            element.HorizontalAlignment = HorizontalAlignment.Right;
            return element;
        }
    }
}
```

```cs
using static QuickMarkup.Infra.QuickRefs;
<setup>
var counter = Ref(0);
</setup>
<root>
    <StackPanel CenterH CenterV>
        <Button CenterH Text="Click Me" />
        <TextBlock CenterH
            Text=/-$"You clicked {counter.Value} time(s)"-/
            Visibility=/-counter.Value > 0 ? Visibility.Visible : Visbility.Collapsed-/
        />
    </StackPanel>
</root>
```
