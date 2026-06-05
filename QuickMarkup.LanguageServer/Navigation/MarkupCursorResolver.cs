using QuickMarkup.LanguageServer.Contracts;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace QuickMarkup.LanguageServer.Navigation;

/// <summary>
/// Resolves the QuickMarkup tag or property at a given LSP position.
/// Converts LSP position to markup Position and walks the AST once to find either a tag name or property.
/// </summary>
public class MarkupCursorResolver : IMarkupCursorResolver
{
    private readonly IQmuiSemanticService _semanticService;

    public MarkupCursorResolver(IQmuiSemanticService semanticService)
    {
        _semanticService = semanticService;
    }

    /// <summary>
    /// Attempts to resolve a tag or property at the specified LSP position.
    /// Traverses the AST once and returns whichever result is found.
    /// </summary>
    public async Task<CursorResolutionResult?> ResolveAtPositionAsync(
        string filePath,
        string content,
        LspPosition position,
        CancellationToken ct = default)
    {
        var markupPosition = new Get.PLShared.Position(position.Line, position.Character);
        
        return await _semanticService.TryResolveAtPositionAsync(
            filePath,
            content,
            markupPosition.Line,
            markupPosition.Char,
            ct);
    }
}
