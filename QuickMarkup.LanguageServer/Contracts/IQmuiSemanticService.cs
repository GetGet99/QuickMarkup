using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis;

namespace QuickMarkup.LanguageServer.Contracts;

/// <summary>
/// Provides shared semantic services for QuickMarkup language features.
/// Encapsulates parsing, binding, and type resolution logic.
/// </summary>
public interface IQmuiSemanticService
{
    /// <summary>
    /// Attempts to resolve a tag or property at the specified position in the document.
    /// Traverses the AST once and returns whichever result is found.
    /// </summary>
    /// <param name="filePath">Path to the .qmui file</param>
    /// <param name="content">Content of the .qmui file</param>
    /// <param name="line">Zero-based line number</param>
    /// <param name="character">Zero-based character offset within the line</param>
    /// <returns>Resolution result (tag or property) if found at the position, otherwise null</returns>
    Task<CursorResolutionResult?> TryResolveAtPositionAsync(
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

/// <summary>
/// Kind of property resolution.
/// </summary>
public enum PropertyResolutionKind
{
    /// <summary>Property attribute on a tag, e.g. Content="Hello" on &lt;Button Content="Hello" /&gt;</summary>
    TagAttribute,
    /// <summary>Property tag child, e.g. &lt;.Content&gt;...&lt;/.Content&gt;</summary>
    PropertyTag,
    /// <summary>Attached property tag child, e.g. &lt;Grid.Row&gt;0&lt;/Grid.Row&gt;</summary>
    AttachedPropertyTag,
    /// <summary>Ref declaration at the top of a .qmui file, e.g. ref string Name = "default";</summary>
    RefDeclaration,
    /// <summary>Type reference in a ref declaration, e.g. the "string" in ref string Name = "default";</summary>
    RefDeclarationType
}

/// <summary>
/// Result of attempting to resolve a property at a specific position.
/// </summary>
/// <param name="PropertyAST">AST node representing the property (for range calculation)</param>
/// <param name="RawPropertyName">The raw property name as it appears in the markup</param>
/// <param name="RoslynSymbol">The resolved Roslyn property symbol (for regular C# properties)</param>
/// <param name="GeneratedSymbol">The resolved generated property symbol (for reactive/computed properties)</param>
/// <param name="DisplayString">Formatted display string for hover tooltip</param>
/// <param name="Kind">The kind of property resolution</param>
/// <param name="ResolvedTypeSymbol">The resolved type symbol (for type references in ref declarations)</param>
/// <param name="OwnerTypeSymbol">The type that owns this property (for navigating to definition)</param>
public record PropertyResolutionResult(
    QuickMarkup.AST.AST? PropertyAST,
    string RawPropertyName,
    IPropertySymbol? RoslynSymbol,
    QuickMarkupGeneratedPropertySymbol? GeneratedSymbol,
    string DisplayString,
    PropertyResolutionKind Kind,
    INamedTypeSymbol? ResolvedTypeSymbol = null,
    INamedTypeSymbol? OwnerTypeSymbol = null);

/// <summary>
/// Combined result for cursor resolution. Contains either a tag or property result.
/// </summary>
/// <param name="Tag">Tag resolution result if cursor is on a tag name</param>
/// <param name="Property">Property resolution result if cursor is on a property</param>
public record CursorResolutionResult(
    TagResolutionResult? Tag,
    PropertyResolutionResult? Property)
{
    /// <summary>True if this result contains a tag resolution.</summary>
    public bool IsTag => Tag is not null;
    /// <summary>True if this result contains a property resolution.</summary>
    public bool IsProperty => Property is not null;
}