# Get the most out of QuickMarkup

To get most out of QuickMarkup, here's the pattern that works for me.

## Define Global Usings

Usings that apply globally in C# will also work in the markup.

```cs
global using Windows.UI.Core;
global using Windows.Foundation;
global using Windows.UI.Xaml.Controls;
global using Microsoft.UI.Xaml.Controls;
global using Windows.UI.Xaml;
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Text;
global using System.Threading.Tasks;
global using static QuickMarkup.Infra.QuickRefs;
```

## Define Extension properties

Latest C# is amazing at this. While these are not supported in XAML, they're a great helper in QuickMarkup.

```cs
static class Extension {
    extension<T>(T element) where T : UIElement
    {
        // Extremely valuable helper if you're dealing with visibility a lot.
        public bool IsVisible
        {
            get => element.Visibility is Visibility.Visible;
            set => element.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
```

### Define Attached Properties

Attached Properties are not yet supported in QuickMarkup, but with extension properties, you can get UI declaring to be easier.

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
<Grid>
    // This will save you a lot of typing to `x => Grid.SetRow(x, 0)`, etc.
    <Child Grid_Row=0 Grid_Column=0 />
    <Child Grid_Row=0 Grid_Column=1 />
    <Child Grid_Row=1 Grid_Column=0 />
    <Child Grid_Row=1 Grid_Column=1 />
</Grid>
```

Another good one is this one. Seriously, if you want to use visiblity a lot, do this.


## Define Extension Methods

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
            Text=`$"You clicked {counter.Value} time(s)"`
            Visibility=`counter.Value > 0 ? Visibility.Visible : Visbility.Collapsed`
        />
    </StackPanel>
</root>
```
