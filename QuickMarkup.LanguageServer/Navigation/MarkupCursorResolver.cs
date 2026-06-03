using QuickMarkup.LanguageServer.Contracts;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace QuickMarkup.LanguageServer.Navigation;

/// <summary>
/// Resolves the QuickMarkup tag at a given LSP position.
/// Converts LSP position to markup Position and walks the AST to find the opening tag identifier.
/// </summary>
public class MarkupCursorResolver
{
    private readonly IQmuiSemanticService _semanticService;

    public MarkupCursorResolver(IQmuiSemanticService semanticService)
    {
        _semanticService = semanticService;
    }

    /// <summary>
    /// Attempts to resolve a tag at the specified LSP position.
    /// </summary>
    public async Task<TagResolutionResult?> ResolveTagAtPositionAsync(
        string filePath, 
        string content, 
        LspPosition position, 
        CancellationToken ct = default)
    {
        // Convert LSP position to our Position type (0-based, exclusive end)
        var markupPosition = new Get.PLShared.Position(position.Line, position.Character);
        
        // Delegate to the semantic service which handles parsing and resolution
        return await _semanticService.TryResolveTagAtPositionAsync(
            filePath, 
            content, 
            markupPosition.Line, 
            markupPosition.Char, 
            ct);
    }
}
