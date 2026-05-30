using Windows.UI.ViewManagement;

namespace QuickMarkup.WinUI;

public partial class ThemeResources
{
    static UISettings UISettings { get; } = new();
    public static Reference<T?> Get<T>(string resourcesName, FrameworkElement? element)
    {
        if (element is null)
            return Get<T>(resourcesName);
        
        var prop = new Reference<T?>(Resolve<T>(resourcesName, element));
        element.ActualThemeChanged += delegate
        {
            prop.Value = Resolve<T>(resourcesName, element);
        };
        return prop;
    }
    public static Reference<T?> Get<T>(string resourcesName)
    {
        bool isDark = UISettings.GetColorValue(UIColorType.Background).R < 255 / 2;
        var prop = new Reference<T?>(Resolve<T>(resourcesName, isDark));
        UISettings.ColorValuesChanged += delegate
        {
            bool isDark = UISettings.GetColorValue(UIColorType.Background).R < 255 / 2;
            prop.Value = Resolve<T>(resourcesName, isDark);
        };
        return prop;
    }
}
