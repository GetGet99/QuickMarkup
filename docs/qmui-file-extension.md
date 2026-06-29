# QuickMarkup UI File Extension

> [!IMPORTANT]
> QuickMarkup File Extension is currently in Alpha. Breaking changes may be done in the future.

The file ending with `.qmui` extension is the QuickMarkup code directly contained in a single file. It use the same syntax as the remaining with a few exceptions.

```cs
// MyPage.qmui
using Microsoft.UI.Xaml.Controls;

namespace MyNamesapce;
class MyPage : Page;

<root>
    <TextBlock Text="Hello from QuickMarkup file extension" />
</root>

```

There are a few changes that you may see.
- Namespace: you must declare a namespace for your component.
- Class: declare the class and the base class of the component.

## QuickMarkup Components

Instead of `class` declaration, you can also declare components with `component`:

```csharp
// Label.qmui
using Microsoft.UI.Xaml.Controls;

namespace MyNamesapce;
component Label : UIElement;

string Text = "";

<TextBlock
    Text=`Text`
    FontSize=16
/>
```

This generates `class Label : IQuickMarkupComponent<UIElement>`. For fragment components, use `fragment component`:

```csharp
// ItemList.qmui
using Microsoft.UI.Xaml.Controls;

namespace MyNamesapce;
fragment component ItemList : TextBlock;

<TextBlock Text="Item A" />
<TextBlock Text="Item B" />
<TextBlock Text="Item C" />
```

This generates `class ItemLsit : IQuickMarkupFragmentComponent<TextBlock>`.