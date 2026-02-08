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

Declaring variables in QuickMarkup will create a reference variable. Declaring variables with `=>` syntax will create a computed variable.

```cs
// Inside QUickMarkup

// Declare references
double FirstOperand = 1;
double SecondOperand = 2;

// Declare computed variables
double Output => `FirstOperand + SecondOperand`;
```

Will generate the following fields:

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

Original references will have `Prop` suffix and original computed variables will have `Comp` suffix. If needed, you may gather references to computed variables.

### Evaluation Order

Refs and computed will be lazily initialized. They will NOT be evaluated until they're accessed for the first time.

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

### Comments

Supported comments are in `//` and `/* */` style.

Note:`<!-- -->` is not supported.

```cs
// This is a comment
/* And this is also
   a comment */
```

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
            <TextBlock Text=/-$"Row {row + 1}"-/ />
        }
        // Row 4, Row 5, Row 6
        foreach (var row in 4..7) {
            <TextBlock Text=/-$"Row {row}"-/ />
        }
    </StackPanel>
</root>
```

##### Loop over iterables

> [!WARNING]  
> Loop iterables are evaluated **only once**. If any elements of the iterables are being replaced, added, or removed, the UI will not be changed. This may change in future versions of QuickMarkup. Therefore, it is an undefined behavior to put a changing list into the foreach loop.

```cs
<setup>
string[] animals = ["Dog", "Cat", "Tiger"];
</setup>
<root>
    <StackPanel>
        foreach (var animal in animals) {
            <TextBlock Text=/-animal-/ />
        }
    </StackPanel>
</root>
```

