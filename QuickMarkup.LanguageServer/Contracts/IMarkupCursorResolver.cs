using QuickMarkup.LanguageServer.Contracts;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace QuickMarkup.LanguageServer.Contracts;

public interface IMarkupCursorResolver
{
    Task<CursorResolutionResult?> ResolveAtPositionAsync(
        string filePath,
        string content,
        LspPosition position,
        CancellationToken ct = default);
}
