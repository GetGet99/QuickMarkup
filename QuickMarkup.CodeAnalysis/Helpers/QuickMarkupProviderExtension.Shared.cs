using QuickMarkup.SourceGen;

namespace QuickMarkup.CodeAnalysis.Helpers;


partial class QuickMarkupProviderExtension
{
    static string FullQuickMarkupAttributeName => field ??= typeof(QuickMarkupAttribute).FullName!;
}