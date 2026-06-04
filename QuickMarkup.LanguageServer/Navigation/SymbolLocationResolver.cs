using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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
public class SymbolLocationResolver
{
    private readonly IQmuiWorkspaceService _workspace;

    public SymbolLocationResolver(IQmuiWorkspaceService workspace)
    {
        _workspace = workspace;
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
            return GetGeneratedPropertyDefinitionLocation(generatedSymbol, propertyResult.RawPropertyName, currentFilePath);
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
        string currentFilePath)
    {
        // Find the .qmui file that defines this property
        foreach (var entry in _workspace.GetAllQmuiEntries())
        {
            if (entry.Kind == QuickMarkupDefinitionKind.QmuiFile && !string.IsNullOrEmpty(entry.FilePath))
            {
                // Parse the file to find the ref declaration
                try
                {
                    var fileContent = File.ReadAllText(entry.FilePath);
                    var sfc = QuickMarkup.CodeAnalysis.Helpers.QuickMarkupProviderExtension.Parse(fileContent);
                    if (sfc is null)
                        continue;

                    foreach (var refDecl in sfc.Refs)
                    {
                        // Check if this ref declaration matches the property name
                        if (refDecl.Name.Name == propertyName)
                        {
                            return new LspLocation
                            {
                                Uri = UriHelper.FromFilePath(entry.FilePath),
                                Range = new LspRange(
                                    new LspPosition(refDecl.Name.Start.Line, refDecl.Name.Start.Char),
                                    new LspPosition(refDecl.Name.End.Line, refDecl.Name.End.Char))
                            };
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip problematic files
                }
            }
        }

        return null;
    }

    private LspLocation? GetRefDeclarationDefinitionLocation(string propertyName, string currentFilePath)
    {
        // Try to find the ref declaration in the current file first
        try
        {
            var fileContent = File.ReadAllText(currentFilePath);
            var sfc = QuickMarkup.CodeAnalysis.Helpers.QuickMarkupProviderExtension.Parse(fileContent);
            if (sfc is not null)
            {
                foreach (var refDecl in sfc.Refs)
                {
                    if (refDecl.Name.Name == propertyName)
                    {
                        return new LspLocation
                        {
                            Uri = UriHelper.FromFilePath(currentFilePath),
                            Range = new LspRange(
                                new LspPosition(refDecl.Name.Start.Line, refDecl.Name.Start.Char),
                                new LspPosition(refDecl.Name.End.Line, refDecl.Name.End.Char))
                        };
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors reading the current file
        }

        // Try other .qmui files in the catalog
        foreach (var entry in _workspace.GetAllQmuiEntries())
        {
            if (entry.Kind == QuickMarkupDefinitionKind.QmuiFile && !string.IsNullOrEmpty(entry.FilePath))
            {
                try
                {
                    var fileContent = File.ReadAllText(entry.FilePath);
                    var sfc = QuickMarkup.CodeAnalysis.Helpers.QuickMarkupProviderExtension.Parse(fileContent);
                    if (sfc is null)
                        continue;

                    foreach (var refDecl in sfc.Refs)
                    {
                        if (refDecl.Name.Name == propertyName)
                        {
                            return new LspLocation
                            {
                                Uri = UriHelper.FromFilePath(entry.FilePath),
                                Range = new LspRange(
                                    new LspPosition(refDecl.Name.Start.Line, refDecl.Name.Start.Char),
                                    new LspPosition(refDecl.Name.End.Line, refDecl.Name.End.Char))
                            };
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip problematic files
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
