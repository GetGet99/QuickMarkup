using Get.PLShared;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace QuickMarkup.LanguageServer.Diagnostics;

public static class PositionConverter
{
    public static LspPosition ToLspPosition(this Position pos, bool isEnd = false)
        => new(pos.Line, isEnd ? pos.Char + 1 : pos.Char);

    public static LspRange ToLspRange(Position start, Position end)
        => new(start.ToLspPosition(), end.ToLspPosition(isEnd: true));
}
