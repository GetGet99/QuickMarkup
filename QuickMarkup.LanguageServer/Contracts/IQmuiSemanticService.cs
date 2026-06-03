using Microsoft.CodeAnalysis;

namespace QuickMarkup.LanguageServer.Contracts;

/// <summary>
/// Provides shared semantic services for QuickMarkup language features.
/// Encapsulates parsing, binding, and type resolution logic.
/// </summary>
public interface IQmuiSemanticService
{
    /// <summary>
    /// Attempts to resolve a QuickMarkup tag at the specified position in the document.
    /// </summary>
    /// <param name="filePath">Path to the .qmui file</param>
    /// <param name="content">Content of the .qmui file</param>
    /// <param name="line">Zero-based line number</param>
    /// <param name="character">Zero-based character offset within the line</param>
    /// <returns>Tag resolution result if a tag was found at the position, otherwise null</returns>
    Task<TagResolutionResult?> TryResolveTagAtPositionAsync(
        string filePath, 
        string content, 
        int line, 
        int character, 
        CancellationToken ct = default);
}

/// <summary>
/// Result of attempting to resolve a QuickMarkup tag at a specific position.
/// </summary>
/// <param name="TagIdentifierAST">AST node representing the tag identifier (for range calculation)</param>
/// <param name="RawTagName">The raw tag name as it appears in the markup</param>
/// <param name="ResolvedSymbol">The resolved type symbol, if successful</param>
/// <param name="DisplayString">Formatted display string for hover tooltip</param>
public record TagResolutionResult(
    QuickMarkup.AST.AST TagIdentifierAST,
    string RawTagName,
    INamedTypeSymbol? ResolvedSymbol,
    string DisplayString);