using QuickMarkup.Infra;
namespace QuickMarkup.WinUI;

[QuickMarkupChildrenProperty("Children")]
[QuickMarkupContentProperty("Content")]
[QuickMarkupContentProperty("Child")]
[QuickMarkupExternalContentProperty(typeof(Microsoft.UI.Xaml.Markup.ContentPropertyAttribute))]
[QuickMarkupDependencyProperty(typeof(DependencyProperty), "Property")]
[QuickMarkupAttachedProperty("Set")]
[QuickMarkupDataTemplateFactory(typeof(DataTemplateFactory))]
public sealed class WinUIFramework
{
}
