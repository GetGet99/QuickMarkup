using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;

namespace QuickMarkup.LanguageServer;

/// <summary>
/// Builds <see cref="QuickMarkupGeneratedMemberTable"/> from .qmui entries.
/// C# [QuickMarkup] attribute types are resolved on-demand via FindProperty.
/// </summary>
internal static class GeneratedMemberTableBuilder
{
    public static QuickMarkupGeneratedMemberTable Build(
        IEnumerable<QuickMarkupTypeEntry> qmuiEntries,
        IFileProvider fileProvider,
        Compilation compilation)
    {
        var members = new List<QuickMarkupGeneratedTypeMembers>();

        foreach (var entry in qmuiEntries)
        {
            try
            {
                if (entry.Kind != QuickMarkupDefinitionKind.QmuiFile || string.IsNullOrEmpty(entry.FilePath))
                    continue;

                var sfc = ParseQmuiFile(entry, fileProvider);
                if (sfc?.ClassDeclaration is null)
                    continue;

                var target = new QuickMarkupTargetContext(
                    Namespace: entry.Namespace,
                    TypeName: entry.ShortName,
                    FullTypeName: entry.FullTypeName,
                    FileName: entry.FilePath,
                    AttributeLocation: default,
                    AttributeLineSpan: default);

                var typeResult = QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(
                    new QuickMarkupParsedAttribute(target, sfc),
                    compilation,
                    CancellationToken.None);

                if (typeResult is { } result)
                    members.Add(result);
            }
            catch (Exception)
            {
                // Skip problematic files
            }
        }

        return new QuickMarkupGeneratedMemberTable(members);
    }

    static QuickMarkupSFC? ParseQmuiFile(QuickMarkupTypeEntry entry, IFileProvider fileProvider)
    {
        var fileContent = fileProvider.ReadAllText(entry.FilePath);
        return QuickMarkupProviderExtension.Parse(fileContent);
    }
}
