using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.SemanticService;

/// <summary>
/// Provides shared semantic services for QuickMarkup language features.
/// Encapsulates parsing, binding, and type resolution logic.
/// </summary>
public class QmuiSemanticService : IQmuiSemanticService
{
    private readonly IRoslynWorkspaceManager _workspaceManager;
    private readonly QuickMarkupWorkspaceCatalog _catalog;
    private readonly IFileProvider _fileProvider;

    public QmuiSemanticService(
        IRoslynWorkspaceManager workspaceManager,
        QuickMarkupWorkspaceCatalog catalog,
        IFileProvider fileProvider)
    {
        _workspaceManager = workspaceManager;
        _catalog = catalog;
        _fileProvider = fileProvider;
    }

    public async Task<TagResolutionResult?> TryResolveTagAtPositionAsync(
        string filePath,
        string content,
        int line,
        int character,
        CancellationToken ct = default)
    {
        // Parse the content using shared parser
        var (sfc, parseErrors) = QuickMarkupProviderExtension.ParseWithErrors(content);
        if (sfc is null || sfc.Template is null)
            return null;

        // Get compilation and enrich with catalog
        var compilation = await GetEnrichedCompilationAsync(ct);
        if (compilation is null)
            return null;

        // Find the tag at the specified position
        var tagAtPosition = FindTagAtPosition(sfc.Template, line, character);
        if (tagAtPosition is null)
            return null;

        var tagName = tagAtPosition.TagStart.TagName;

        // Handle root tag specially - resolve to the component class
        if (tagName == "root" && sfc.ClassDeclaration is not null)
        {
            var componentClass = ResolveComponentClass(compilation, sfc);
            var displayString = componentClass != null
                ? componentClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : sfc.ClassDeclaration.Name;

            return new TagResolutionResult(
                TagIdentifierAST: tagAtPosition.TagStart.TagIdentifierAST,
                RawTagName: "root",
                ResolvedSymbol: componentClass,
                DisplayString: displayString);
        }

        // Try to resolve the tag's type symbol
        var resolvedSymbol = TryResolveTagType(compilation, sfc, tagAtPosition);

        // Create display string for hover
        var displayString2 = resolvedSymbol != null
            ? resolvedSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : $"{(resolvedSymbol == null ? "(unresolved) " : "")}{tagName}";

        return new TagResolutionResult(
            TagIdentifierAST: tagAtPosition.TagStart.TagIdentifierAST,
            RawTagName: tagName,
            ResolvedSymbol: resolvedSymbol,
            DisplayString: displayString2);
    }

    private async Task<Compilation?> GetEnrichedCompilationAsync(CancellationToken ct)
    {
        var compilation = _workspaceManager.Compilation;
        if (compilation is null)
            return null;

        // If catalog is empty and we have a workspace root, rebuild it
        if (_catalog.Entries.Length == 0 && _workspaceManager.CurrentProjectPath is not null)
        {
            var workspaceRoot = System.IO.Path.GetDirectoryName(_workspaceManager.CurrentProjectPath);
            if (workspaceRoot is not null)
            {
                _catalog.Rebuild(compilation, workspaceRoot, _fileProvider);
            }
        }

        // Enrich compilation with all catalog .qmui entries
        foreach (var entry in _catalog.Entries)
        {
            if (entry.Kind == QuickMarkupDefinitionKind.QmuiFile && !string.IsNullOrEmpty(entry.FilePath))
            {
                try
                {
                    var fileContent = _fileProvider.ReadAllText(entry.FilePath);
                    var sfc = QuickMarkupProviderExtension.Parse(fileContent);
                    if (sfc is not null && sfc.ClassDeclaration is not null)
                    {
                        var target = new QuickMarkupTargetContext(
                            Namespace: entry.Namespace,
                            TypeName: entry.ShortName,
                            FullTypeName: entry.FullTypeName,
                            FileName: entry.FilePath,
                            AttributeLocation: default,
                            AttributeLineSpan: default);

                        compilation = QuickMarkupCompilationEnricher.EnsureTypeSymbolInCompilation(target, sfc, compilation);
                    }
                }
                catch (Exception)
                {
                    // Skip problematic files
                }
            }
        }

        return compilation;
    }

    private QuickMarkupParsedTag? FindTagAtPosition(QuickMarkupParsedTag template, int line, int character)
    {
        return FindTagInNode(template, line, character);
    }

    private QuickMarkupParsedTag? FindTagInNode(IQMNodeChild node, int line, int character)
    {
        if (node is QuickMarkupParsedTag tag)
        {
            // Check if this tag's opening identifier is at the position
            if (tag.TagStart is QuickMarkupConstructor constructor)
            {
                var identifierAst = constructor.TagIdentifierAST as PositionedIdentifier;
                if (identifierAst is not null &&
                    identifierAst.Start.Line <= line && identifierAst.End.Line >= line &&
                    identifierAst.Start.Char <= character && identifierAst.End.Char >= character)
                {
                    return tag;
                }
            }

            // Check if cursor is on the closing tag
            if (tag.EndTagName is not null)
            {
                if (tag.EndTagName.Start.Line <= line && tag.EndTagName.End.Line >= line &&
                    tag.EndTagName.Start.Char <= character && tag.EndTagName.End.Char >= character)
                {
                    return tag;
                }
            }

            // Recursively check children
            if (tag.Children is not null)
            {
                foreach (var child in tag.Children)
                {
                    var childResult = FindTagInNode(child, line, character);
                    if (childResult is not null)
                        return childResult;
                }
            }
        }
        else if (node is QuickMarkupParsedIfNode ifNode)
        {
            // Check body when true
            var result = FindTagInNode(ifNode.BodyWhenTrue, line, character);
            if (result is not null) return result;

            // Check body when false
            if (ifNode.BodyWhenFalse is not null)
            {
                result = FindTagInNode(ifNode.BodyWhenFalse, line, character);
                if (result is not null) return result;
            }
        }
        else if (node is QuickMarkupParsedForNode forNode)
        {
            // Check loop body
            var result = FindTagInNode(forNode.Body, line, character);
            if (result is not null) return result;
        }
        else if (node is QuickMarkupParsedFragmentNode fragmentNode)
        {
            // Check all children in fragment
            foreach (var child in fragmentNode.Children)
            {
                var childResult = FindTagInNode(child, line, character);
                if (childResult is not null)
                    return childResult;
            }
        }

        return null;
    }

    private INamedTypeSymbol? TryResolveTagType(Compilation compilation, QuickMarkupSFC sfc, QuickMarkupParsedTag tag)
    {
        if (tag.TagStart is not QuickMarkupConstructor constructor)
            return null;

        var tagName = constructor.TagIdentifierAST.ToString();
        if (string.IsNullOrEmpty(tagName))
            return null;

        // Try to find the type in the compilation first
        var ns = sfc.Namespace?.Name ?? "";
        var fullName = string.IsNullOrEmpty(ns) ? tagName : $"{ns}.{tagName}";
        var typeSymbol = compilation.GetTypeByMetadataName(fullName);
        if (typeSymbol is not null)
            return typeSymbol;

        // If not found, check our catalog for cross-file references
        foreach (var entry in _catalog.GetEntriesByShortName(tagName))
        {
            // Try to get the type symbol from compilation
            typeSymbol = compilation.GetTypeByMetadataName(entry.FullTypeName);
            if (typeSymbol is not null)
                return typeSymbol;

            // If still not found, we might need to add it via enrichment
            // This would be handled in GetEnrichedCompilationAsync
        }

        return null;
    }

    private INamedTypeSymbol? ResolveComponentClass(Compilation compilation, QuickMarkupSFC sfc)
    {
        if (sfc.ClassDeclaration is null)
            return null;

        var ns = sfc.Namespace?.Name ?? "";
        var typeName = sfc.ClassDeclaration.Name;
        var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";

        return compilation.GetTypeByMetadataName(fullName);
    }
}
