# QuickMarkup

> Reactive UI for C# without XAML.

> [!IMPORTANT]
> QuickMarkup is currently in Preview.
> APIs, generated output, and syntax may evolve before 1.0.
> Recommended for experimentation, prototypes, and early adopters.

QuickMarkup is a reactive UI DSL for C# that lets you build declarative interfaces entirely in code using compile-time source generation.

It combines:

* Reactive state updates
* HTML/Vue-inspired markup syntax
* Direct C# expressions
* Compile-time code generation
* No MVVM boilerplate
* No `INotifyPropertyChanged`
* No XAML

QuickMarkup currently provides the best experience on WinUI 3 and UWP, with experimental support for additional .NET UI frameworks.

---

## Example

```cs
[QuickMarkup("""
    using static QuickMarkup.Infra.QuickRefs;

    string Input = "";

    <root>
        <StackPanel Spacing=12 Padding=24>

            <TextBlock
                Text="QuickMarkup Todo App"
                FontSize=28
                FontWeight=SemiBold
            />

            <TextBox
                PlaceholderText="Add a todo..."
                Text<=>`Input`
            />

            <Button
                Content="Add Todo"
                @Click+=`
                    if (!string.IsNullOrWhiteSpace(Input))
                    {
                        Todos.Add(Input);
                        Input = "";
                    }
                `
            />

            if (`Todos.Count == 0`) {
                <TextBlock
                    Text="Nothing here yet."
                    Opacity=0.7
                />
            }
            else {
                foreach (index; var todo in `Todos`) {
                    <Border
                        CornerRadius=12
                        Padding=12
                    >
                        <TextBlock
                            Text=`$"{index + 1}. {todo}"`
                        />
                    </Border>
                }
            }

        </StackPanel>
    </root>
    """)]
public partial class TodoPage : Page
{
    ObservableCollection<string> Todos = [
        "Ship QuickMarkup",
        "Write documentation"
    ];
}
```

State updates automatically refresh the UI.

No view models.
No property change boilerplate.
No `INotifyPropertyChanged`.

---

## Installation

QuickMarkup is available as NuGet packages.

```pwsh
# WinUI 3
Install-Package QuickMarkup.WinUI

# UWP
Install-Package QuickMarkup.UWP

# Cross-platform core package
Install-Package QuickMarkup
```

Packages:

* `QuickMarkup.WinUI` — WinUI 3 integration
* `QuickMarkup.UWP` — UWP integration
* `QuickMarkup` — framework-agnostic core runtime

---

## Quick Start

Initialize the reactive scheduler once on the UI thread:

```csharp
public App()
{
    this.InitializeComponent();

    QuickMarkup.WinUI.ReactiveInitializer
        .InitReactiveScheduler();
}
```

Then create a page:

```cs
[QuickMarkup("""
int Counter = 0;

<root>
    <StackPanel Spacing=12>

        <Button
            Content="Increment"
            @Click+=`Counter++`
        />

        <TextBlock
            Text=`$"Count: {Counter}"`
        />

    </StackPanel>
</root>
""")]
public partial class CounterPage : Page;
```

That’s it.

Changing `Counter` automatically updates the UI.

---

## Why QuickMarkup?

QuickMarkup is designed for developers who prefer:

* Writing UI directly in C#
* Reactive programming models
* Co-locating state and UI logic
* Compile-time tooling over runtime reflection
* Less MVVM ceremony
* Vue/React-style ergonomics in .NET
* Native interoperability with existing WinUI/UWP controls and libraries

QuickMarkup may *not* be the right fit if your project depends heavily on:

- Visual designers
- Strict MVVM-only architecture
- Advanced XAML-specific tooling workflows
- Maximum long-term API stability today

## Core Concepts

### Reactive References

Variables declared at the top level become reactive references automatically.

```cs
int Counter = 0;
```

QuickMarkup generates reactive backing infrastructure automatically.

---

### Computed Values

Use `=>` to declare cached computed values.

```cs
double Total => `Price * Quantity`;
```

Computed values automatically track dependencies and re-evaluate when needed.

---

### Direct C# Expressions

Any property can contain inline C# using backticks.

```cs
<TextBlock
    Text=`$"Clicked {Counter} times"`
/>
```

Expressions automatically re-run when reactive dependencies change.

---

### Two-Way Binding

```cs
<TextBox Text<=>`SearchText` />
```

QuickMarkup supports:

* One-way binding
* Bindback
* Two-way binding

without requiring separate view model infrastructure.

---

### Structural UI

Conditional rendering:

```cs
if (`IsLoggedIn`) {
    <ProfilePage />
}
else {
    <LoginPage />
}
```

Reactive collection rendering:

```cs
foreach (var item in `Items`) {
    <TextBlock Text=`item.Name` />
}
```

---

### Ecosystem Compatibility

QuickMarkup works directly with existing WinUI/UWP controls and ecosystem libraries such as CommunityToolkit.

Because QuickMarkup generates native UI elements directly, existing dependency properties, styles, resource dictionaries, and control behaviors continue to work normally.

```cs
[QuickMarkup("""
    using CommunityToolkit.WinUI.Controls;

    bool IsEnabled = true;

    <root>
        <SettingsCard
            Header="Notifications"
            Description=`IsEnabled
                ? "Notifications are enabled"
                : "Notifications are disabled"`
        >
            <ToggleSwitch IsOn<=>`IsEnabled` />
        </SettingsCard>
    </root>
    """)]
public partial class SettingsPage : Page;
```

---

## Included Features Today

| Area | Description | Supported Frameworks | Status |
|---|---|---|---|
| Core DSL & source generation | Declarative UI authoring without XAML using compile-time code generation | Cross-platform | Preview |
| Reactive state system | Automatic dependency tracking and UI updates without `INotifyPropertyChanged` | Cross-platform | Preview |
| Inline C# expressions | Reactive C# expressions directly inside markup | Cross-platform | Preview |
| Events & callbacks | Concise event and inline callback syntax | Cross-platform | Preview |
| Reactive bindings | One-way, bindback, and two-way bindings | WinUI 3 / UWP / Uno* | Preview |
| Structural rendering | Reactive `if` / `else` / `foreach` rendering | Cross-platform | Preview |
| Reusable components | Single-node and fragment-based reusable components | Cross-platform | Preview |
| Native control interoperability | Use existing WinUI/UWP controls and component libraries directly inside QuickMarkup | WinUI 3 / UWP / Uno* | Preview |
| Roslyn analyzers | Real-time diagnostics and compile-time validation | Cross-platform | Preview |
| Theme integration | Reactive theme-aware brushes and resources | WinUI 3 / UWP | Preview |
| Snapshot persistence | Source-generated state persistence | Cross-platform | Draft |

\* Uno Platform support currently relies on WinUI-compatible APIs and patterns. Official support and compatibility guarantees are still evolving.

---

## Current Limitations

* APIs may change before 1.0
* Some native controls do not fully support bindback
* WPF and MAUI support are still experimental
* Visual designer tooling is not available
* Hot reload support is still evolving

---

## Documentation

* [QuickMarkup Language](./docs/qm-language.md)
* [Backend Infrastructure](./docs/infra.md)
* [Get the Most out of QuickMarkup](./docs/get-most-out-of-qm.md)

---

## Example Project

* [PhotoToys Next](https://github.com/GetGet99/PhotoToysNext)

---

## Philosophy

QuickMarkup aims to make UI development feel lightweight, reactive, and direct.

Instead of separating UI into:

* XAML
* ViewModels
* Converters
* Property change infrastructure

QuickMarkup keeps state, logic, and markup close together while still generating efficient compiled code.

The goal is to make modern reactive UI patterns feel natural in C# without requiring a large architectural framework around every screen.
