using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer;

/// <summary>
/// Builds <see cref="QuickMarkupGeneratedMemberTable"/> from .qmui entries.
/// C# [QuickMarkup] attribute types are resolved on-demand via FindProperty.
/// Uses cached ASTs from the catalog to avoid re-parsing.
/// </summary>
internal static class GeneratedMemberTableBuilder
{
    public static QuickMarkupGeneratedMemberTable Build(
        QuickMarkupWorkspaceCatalog catalog,
        IQmuiDocumentStore documentStore,
        IFileProvider fileProvider,
        Compilation compilation)
    {
        var members = new List<QuickMarkupGeneratedTypeMembers>();

        foreach (var entry in catalog.Entries)
        {
            try
            {
                if (entry.Kind != QuickMarkupDefinitionKind.QmuiFile || string.IsNullOrEmpty(entry.FilePath))
                    continue;

                // Use cached AST from catalog to avoid re-parsing
                var sfc = GetSfc(entry.FilePath, catalog, documentStore, fileProvider);
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

    static QuickMarkupSFC? GetSfc(
        string filePath,
        QuickMarkupWorkspaceCatalog catalog,
        IQmuiDocumentStore documentStore,
        IFileProvider fileProvider)
    {
        // Prefer cached AST from catalog
        if (catalog.TryGetCachedAst(filePath, out var cached))
            return cached;

        // Fallback: read and parse (shouldn't happen in normal flow)
        var storeTask = documentStore.GetTextAsync(filePath);
        var content = storeTask.IsCompletedSuccessfully && storeTask.Result is { } inMemory
            ? inMemory
            : fileProvider.ReadAllText(filePath);

        return QuickMarkupProviderExtension.Parse(content);
    }
}
