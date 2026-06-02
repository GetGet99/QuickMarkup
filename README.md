# QuickMarkup (Preview)

> [!WARNING]  
> This project is still in Preview. Not recommended for production use yet.

QuickMarkup is a declarative markup language that makes it easier to declare UI in C# without using XAML. Our system relies on reactivity, rather than MVVM style. Some patterns are Vue-inspired.

Currently, our full support is UWP and WinUI 3 .NET 10. Supports most other UI frameworks in C# but with some functionality becoming limited (bind two way, bind to source, and children/child node auto detection do not have full support against native components)

## Installation

QuickMarkup is now avaliable as Nuget Packages

```pwsh
# WinUI 3 https://www.nuget.org/packages/QuickMarkup.WinUI
Install-Package QuickMarkup.WinUI
# UWP https://www.nuget.org/packages/QuickMarkup.UWP
Install-Package QuickMarkup.UWP
# Cross Platform, no specific platform (Uno Platform, WPF, MAUI, etc.) https://www.nuget.org/packages/QuickMarkup
Install-Package QuickMarkup
```

See compatability table below under [Included Features](#included-features-today)

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

## Included Features Today

| Area | What it does | Supported Frameworks | Maturity |
|---|---|---|---|
| **Core DSL & source gen** | Write UI without XAML — embed markup in `[QuickMarkup]` attributes, compile-time code generation eliminates boilerplate | Cross-platform | Preview |
| **Reactivity system** | Reactive variables that automatically update the UI when values change; no `INotifyPropertyChanged` or manual event wiring | Cross-platform | Preview |
| **Markup syntax** | Familiar tag syntax with direct C# integration — use any type, expression, or enum inline without value converters | Cross-platform | Preview |
| **Events & one-way binding** | Attach event handlers concisely; bind source values to UI properties with auto-update | Cross-platform | Preview |
| **Bindback & two-way binding** | One-way-to-source (bindback) and two-way binding for DependencyProperty-backed controls | UWP/WinUI 3/Uno Platform\* | Preview |
| **Structural directives** | Conditionally show content with `if`/`else`, loop over data with auto-updating `foreach` on collection changes | Cross-platform | Preview |
| **Component model** | Build reusable UI pieces that supports single-element and multi-element outputs | Cross-platform | Preview |
| **Extension callbacks** | Call custom extension methods as markup identifiers (`CenterH`, `CenterV`); inline lambdas for one-off side effects | Cross-platform | Preview |
| **Roslyn analyzers** | Catch errors in real time — syntax mistakes, property typos, type mismatches flagged before you build | Cross-platform | Preview |
| **Theming** | Theme-aware brushes that react to dark/light/high-contrast changes; access resource dictionaries without boilerplate | UWP/WinUI 3 | Preview |
| **Framework packages** | Add `QuickMarkup.WinUI` or `QuickMarkup.UWP` with one initialization call and get full platform integration | UWP/WinUI 3 | Preview |
| **Snapshot persistence** | Save and restore UI state automatically via source-generated serialization — no manual save/load code | Cross-platform | Draft |

\* Uno Platform is indirectly supported by utilizing the same namespace and pattern as WinUI 3. No official support is provided for now.

## Documentation

- [QuickMarkup Backend Infrastructure](./docs/infra.md)
- [The QuickMarkup Language](./docs/qm-language.md)
- [Get The most out of QuickMarkup](./docs/get-most-out-of-qm.md)

## Example Project

- [PhotoToys Next](https://github.com/GetGet99/PhotoToysNext)
