using System.Runtime.CompilerServices;

namespace QuickMarkup.WinUI;

public static class ThemeResourcesExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ThemeBrushes UseThemeBrushes(this FrameworkElement element) => new(element);
}