using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;

namespace QuickMarkup.LanguageServer;

/// <summary>
/// Builds <see cref="QuickMarkupGeneratedMemberTable"/> from catalog entries.
/// Shared by semantic and diagnostic services for cross-file property resolution.
/// </summary>
internal static class GeneratedMemberTableBuilder
{
    public static QuickMarkupGeneratedMemberTable Build(
        QuickMarkupWorkspaceCatalog catalog,
        IFileProvider fileProvider,
        Compilation compilation)
    {
        var members = new List<QuickMarkupGeneratedTypeMembers>();
        var quickMarkupAttributeSymbol = compilation.GetTypeByMetadataName("QuickMarkup.Infra.QuickMarkupAttribute");

        foreach (var entry in catalog.Entries)
        {
            try
            {
                QuickMarkupSFC? sfc = null;
                QuickMarkupTargetContext target = default;

                if (entry.Kind == QuickMarkupDefinitionKind.QmuiFile && !string.IsNullOrEmpty(entry.FilePath))
                {
                    sfc = ParseQmuiFile(entry, fileProvider);
                    if (sfc is not null)
                    {
                        target = new QuickMarkupTargetContext(
                            Namespace: entry.Namespace,
                            TypeName: entry.ShortName,
                            FullTypeName: entry.FullTypeName,
                            FileName: entry.FilePath,
                            AttributeLocation: default,
                            AttributeLineSpan: default);
                    }
                }
                else if (entry.Kind == QuickMarkupDefinitionKind.CSharpClass
                    && quickMarkupAttributeSymbol is not null
                    && !string.IsNullOrEmpty(entry.FilePath))
                {
                    sfc = ParseCSharpAttribute(entry, compilation, quickMarkupAttributeSymbol, out target);
                }

                if (sfc is not null && sfc.ClassDeclaration is not null)
                {
                    var typeResult = QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(
                        new QuickMarkupParsedAttribute(target, sfc),
                        compilation,
                        CancellationToken.None);

                    if (typeResult is { } result)
                        members.Add(result);
                }
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

    static QuickMarkupSFC? ParseCSharpAttribute(
        QuickMarkupTypeEntry entry,
        Compilation compilation,
        INamedTypeSymbol quickMarkupAttributeSymbol,
        out QuickMarkupTargetContext target)
    {
        target = default;

        var tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == entry.FilePath);
        if (tree is null) return null;

        var semanticModel = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();

        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == entry.ShortName
                && c.AttributeLists.SelectMany(a => a.Attributes)
                    .Any(a => SymbolEqualityComparer.Default.Equals(
                        semanticModel.GetSymbolInfo(a).Symbol, quickMarkupAttributeSymbol)));
        if (classDecl is null) return null;

        var typeSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (typeSymbol is null) return null;

        var attributeData = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(
                a.AttributeClass, quickMarkupAttributeSymbol));
        if (attributeData is null || attributeData.ConstructorArguments.Length == 0)
            return null;

        var markupString = attributeData.ConstructorArguments[0].Value as string;
        if (string.IsNullOrEmpty(markupString))
            return null;

        var sfc = QuickMarkupProviderExtension.Parse(markupString);
        if (sfc is null) return null;

        var syntaxRef = attributeData.ApplicationSyntaxReference;
        var attributeLocation = syntaxRef?.Span ?? default;
        var lineSpan = syntaxRef is not null
            ? tree.GetLineSpan(attributeLocation)
            : default;

        target = new QuickMarkupTargetContext(
            Namespace: entry.Namespace,
            TypeName: entry.ShortName,
            FullTypeName: entry.FullTypeName,
            FileName: entry.FilePath,
            AttributeLocation: attributeLocation,
            AttributeLineSpan: new(lineSpan.StartLinePosition, lineSpan.EndLinePosition));

        return sfc;
    }
}
