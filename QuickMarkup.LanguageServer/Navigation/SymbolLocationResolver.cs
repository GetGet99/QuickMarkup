using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Diagnostics;
using LspLocation = OmniSharp.Extensions.LanguageServer.Protocol.Models.Location;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace QuickMarkup.LanguageServer.Navigation;

/// <summary>
/// Resolves definition locations for QuickMarkup type symbols.
/// Handles C# types, .qmui components, and generated file fallbacks.
/// </summary>
public class SymbolLocationResolver : ISymbolLocationResolver
{
    readonly IQmuiWorkspaceService _workspace;
    readonly ICompilationService _compilation;

    public SymbolLocationResolver(IQmuiWorkspaceService workspace, ICompilationService compilation)
    {
        _workspace = workspace;
        _compilation = compilation;
    }

    /// <summary>
    /// Gets the definition locations for a resolved symbol.
    /// Returns a single location or null if unresolved.
    /// </summary>
    public LspLocation? GetDefinitionLocation(INamedTypeSymbol symbol, string currentFilePath)
    {
        // Check if this symbol corresponds to a catalog entry
        if (_workspace.TryGetQmuiEntry(symbol.ToDisplayString(), out var catalogEntry))
        {
            // Prefer .qmui location over generated file location
            if (catalogEntry.Kind == QuickMarkupDefinitionKind.QmuiFile && !string.IsNullOrEmpty(catalogEntry.FilePath))
            {
                // For .qmui files, we'd ideally use the NameSpan from the catalog entry
                // But since ClassDeclaration doesn't currently store span, we'll use file path with approximate position
                // In a full implementation, we would use the actual span from the catalog entry
                return new LspLocation
                {
                    Uri = UriHelper.FromFilePath(catalogEntry.FilePath),
                    Range = new LspRange(
                        new LspPosition(0, 0),  // Placeholder - should be the actual class name span
                        new LspPosition(0, 0))  // Placeholder - should be the actual class name span
                };
            }
            
            // For C# classes with [QuickMarkup] attribute, use the symbol's locations
            if (catalogEntry.Kind == QuickMarkupDefinitionKind.CSharpClass && !string.IsNullOrEmpty(catalogEntry.FilePath))
            {
                var locations = symbol.Locations
                    .Where(l => l.SourceTree?.FilePath == catalogEntry.FilePath)
                    .Select(l => new LspLocation
                    {
                        Uri = UriHelper.FromFilePath(l.SourceTree!.FilePath),
                        Range = ConvertTextSpanToLspRange(l.SourceTree, l.SourceSpan)
                    })
                    .FirstOrDefault();
                
                if (locations != null)
                    return locations;
            }
        }

        // Fallback to standard symbol locations
        var primaryLocation = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (primaryLocation != null && primaryLocation.SourceTree != null)
        {
            return new LspLocation
            {
                Uri = UriHelper.FromFilePath(primaryLocation.SourceTree.FilePath),
                Range = ConvertTextSpanToLspRange(primaryLocation.SourceTree, primaryLocation.SourceSpan)
            };
        }

        return null;
    }

    /// <summary>
    /// Attempts to resolve a symbol from a tag resolution result and get its definition location.
    /// </summary>
    public LspLocation? GetDefinitionLocation(TagResolutionResult? tagResult, string currentFilePath)
    {
        if (tagResult == null || tagResult.ResolvedSymbol == null)
            return null;

        return GetDefinitionLocation(tagResult.ResolvedSymbol, currentFilePath);
    }

    /// <summary>
    /// Gets the definition location for a property symbol.
    /// </summary>
    public LspLocation? GetDefinitionLocation(IPropertySymbol propertySymbol, string currentFilePath)
    {
        var primaryLocation = propertySymbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (primaryLocation != null && primaryLocation.SourceTree != null)
        {
            return new LspLocation
            {
                Uri = UriHelper.FromFilePath(primaryLocation.SourceTree.FilePath),
                Range = ConvertTextSpanToLspRange(primaryLocation.SourceTree, primaryLocation.SourceSpan)
            };
        }

        return null;
    }

    /// <summary>
    /// Gets the definition location for a resolved property.
    /// Handles both Roslyn symbols and generated symbols (reactive/computed properties).
    /// </summary>
    public LspLocation? GetDefinitionLocation(PropertyResolutionResult? propertyResult, string currentFilePath)
    {
        if (propertyResult == null)
            return null;

        // For Roslyn properties, navigate to the C# declaration
        if (propertyResult.RoslynSymbol != null)
        {
            return GetDefinitionLocation(propertyResult.RoslynSymbol, currentFilePath);
        }

        // For type references in ref declarations, navigate to the type
        if (propertyResult.ResolvedTypeSymbol != null && propertyResult.Kind == PropertyResolutionKind.RefDeclarationType)
        {
            return GetDefinitionLocation(propertyResult.ResolvedTypeSymbol, currentFilePath);
        }

        // For generated properties (reactive/computed), find the ref declaration in the .qmui file
        if (propertyResult.GeneratedSymbol is { } generatedSymbol)
        {
            return GetGeneratedPropertyDefinitionLocation(generatedSymbol, propertyResult.RawPropertyName, currentFilePath, propertyResult.OwnerTypeSymbol);
        }

        // For ref declarations, try to find the .qmui file
        if (propertyResult.Kind == PropertyResolutionKind.RefDeclaration)
        {
            return GetRefDeclarationDefinitionLocation(propertyResult.RawPropertyName, currentFilePath);
        }

        return null;
    }

    private LspLocation? GetGeneratedPropertyDefinitionLocation(
        QuickMarkup.CodeAnalysis.QuickMarkupGeneratedPropertySymbol generatedSymbol,
        string propertyName,
        string currentFilePath,
        INamedTypeSymbol? ownerTypeSymbol)
    {
        if (ownerTypeSymbol is null)
            return null;

        // First try .qmui files from the catalog (uses cached AST, no disk I/O)
        if (_workspace.TryGetQmuiEntry(ownerTypeSymbol.ToDisplayString(), out var entry)
            && entry.Kind == QuickMarkupDefinitionKind.QmuiFile
            && !string.IsNullOrEmpty(entry.FilePath)
            && _workspace.Catalog.TryGetCachedAst(entry.FilePath, out var sfc))
        {
            foreach (var refDecl in sfc.Refs)
            {
                if (refDecl.Name.Name.Name == propertyName)
                {
                    return new LspLocation
                    {
                        Uri = UriHelper.FromFilePath(entry.FilePath),
                        Range = new LspRange(
                            new LspPosition(refDecl.Name.Name.Start.Line, refDecl.Name.Name.Start.Char),
                            new LspPosition(refDecl.Name.Name.End.Line, refDecl.Name.Name.End.Char))
                    };
                }
            }
        }

        // Then try [QuickMarkup] C# classes - search only this specific type
        return FindInQuickMarkupAttributeClass(ownerTypeSymbol, propertyName);
    }

    private LspLocation? FindInQuickMarkupAttributeClass(INamedTypeSymbol typeSymbol, string propertyName)
    {
        try
        {
            var compilation = _compilation.EnrichedCompilation ?? _compilation.Compilation;
            if (compilation is null)
                return null;

            var quickMarkupAttributeSymbol = compilation.GetTypeByMetadataName("QuickMarkup.SourceGen.QuickMarkupAttribute");
            if (quickMarkupAttributeSymbol is null)
                return null;

            var attribute = typeSymbol.GetAttributes()
                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, quickMarkupAttributeSymbol));
            if (attribute is null || attribute.ConstructorArguments.Length == 0)
                return null;

            var markup = attribute.ConstructorArguments[0].Value as string;
            if (string.IsNullOrEmpty(markup))
                return null;

            Console.Error.WriteLine($"[QuickMarkup] Parsing {typeSymbol.Name} in {typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "unknown"}");
            var sfc = QuickMarkupProviderExtension.Parse(markup);
            if (sfc is null)
                return null;

            var mapper = new AttributeStringLocationMapper(attribute);
            if (!mapper.IsValid)
                return null;

            foreach (var refDecl in sfc.Refs)
            {
                if (refDecl.Name.Name.Name != propertyName)
                    continue;

                var location = mapper.GetLocation(refDecl.Name.Name);
                var syntaxTree = location.SourceTree;
                if (syntaxTree is null)
                    return null;

                return new LspLocation
                {
                    Uri = UriHelper.FromFilePath(syntaxTree.FilePath),
                    Range = ConvertTextSpanToLspRange(syntaxTree, location.SourceSpan)
                };
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[QuickMarkup] Error resolving symbol location: {ex.Message}");
        }

        return null;
    }

    private LspLocation? GetRefDeclarationDefinitionLocation(string propertyName, string currentFilePath)
    {
        // Try to find the ref declaration in the current file first (from cached AST)
        if (_workspace.Catalog.TryGetCachedAst(currentFilePath, out var currentSfc))
        {
            foreach (var refDecl in currentSfc.Refs)
            {
                if (refDecl.Name.Name.Name == propertyName)
                {
                    return new LspLocation
                    {
                        Uri = UriHelper.FromFilePath(currentFilePath),
                        Range = new LspRange(
                            new LspPosition(refDecl.Name.Name.Start.Line, refDecl.Name.Name.Start.Char),
                            new LspPosition(refDecl.Name.Name.End.Line, refDecl.Name.Name.End.Char))
                    };
                }
            }
        }

        // Try other .qmui files in the catalog (uses cached ASTs, no disk I/O)
        foreach (var (filePath, sfc) in _workspace.Catalog.CachedAst)
        {
            if (string.Equals(filePath, currentFilePath, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var refDecl in sfc.Refs)
            {
                if (refDecl.Name.Name.Name == propertyName)
                {
                    return new LspLocation
                    {
                        Uri = UriHelper.FromFilePath(filePath),
                        Range = new LspRange(
                            new LspPosition(refDecl.Name.Name.Start.Line, refDecl.Name.Name.Start.Char),
                            new LspPosition(refDecl.Name.Name.End.Line, refDecl.Name.Name.End.Char))
                    };
                }
            }
        }

        return null;
    }

    private static LspRange ConvertTextSpanToLspRange(SyntaxTree tree, TextSpan span)
    {
        var startLine = tree.GetLineSpan(new TextSpan(span.Start, 0)).StartLinePosition;
        var endLine = tree.GetLineSpan(new TextSpan(span.End, 0)).StartLinePosition;
        
        var start = new Get.PLShared.Position(startLine.Line, startLine.Character);
        var end = new Get.PLShared.Position(endLine.Line, endLine.Character);
        
        return PositionConverter.ToLspRange(start, end);
    }
}

/// <summary>
/// Helper methods for URI conversion.
/// </summary>
internal static class UriHelper
{
    public static Uri FromFilePath(string filePath)
    {
        // Handle both absolute and relative paths
        var absolutePath = Path.IsPathRooted(filePath) 
            ? filePath 
            : Path.Combine(Environment.CurrentDirectory, filePath);
        
        return new Uri(absolutePath);
    }
}
