# QuickMarkup (ALPHA)

> [!WARNING]  
> This project is still in Alpha. Not recommended for production use yet.

QuickMarkup is a declarative markup language that makes it easier to declare UI in C# without using XAML. Our system relies on reactivity, rather than MVVM style. Some patterns are Vue-inspired.

Currently, our support is only UWP .NET 10. In the future, it is possible to decouple from WinUI specific logic and can be used in general.

## Introduction

QuickMarkup is divided into 3 main sections: Usings, setup, and UI.

```cs
int Counter = 0;
<root>
    <StackPanel>
        <Button Text="Click Me" @Click+=`Counter++` />
        <TextBlock
            Text=`$"You clicked {Counter} time(s)"`
            Visibility=`Counter > 0 ? Visibility.Visible : Visbility.Collapsed`
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
    int Counter = 0;
    <root>
        <StackPanel>
            <Button Text="Click Me" @Click+=`Counter++` />
            <TextBlock
                Text=`$"You clicked {Counter} time(s)"`
                Visibility=`Counter > 0 ? Visibility.Visible : Visbility.Collapsed`
            />
        </StackPanel>
    </root>
    """)]
public partial class CounterPage : Page;
```

And that's it.

## Documentation

- [QuickMarkup Backend Infrastructure](./docs/infra.md)
- [The QuickMarkup Language](./docs/qm-language.md)
- [Get The most out of QuickMarkup](./docs/get-most-out-of-qm.md)