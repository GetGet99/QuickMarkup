using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Get.PLShared;
using QuickMarkup.AST;

namespace QuickMarkup.SourceGen;

class QuickMarkupQmuiLocationProvider : IQuickMarkupLocationProvider
{
    readonly string _filePath;
    readonly SourceText _text;
    readonly TextLineCollection _lines;
    readonly Location _fallback;

    public QuickMarkupQmuiLocationProvider(string filePath, string content)
    {
        _filePath = filePath;
        _text = SourceText.From(content);
        _lines = _text.Lines;
        _fallback = Location.Create(filePath, new TextSpan(0, 0), new LinePositionSpan());
    }

    public Location Fallback => _fallback;

    public Location GetLocation(Position start, Position end)
    {
        var startPos = _lines.GetPosition(new LinePosition(start.Line, start.Char));
        var endPos = _lines.GetPosition(new LinePosition(end.Line, end.Char + 1));
        var span = TextSpan.FromBounds(startPos, endPos);
        var lineSpan = _lines.GetLinePositionSpan(span);
        return Location.Create(_filePath, span, lineSpan);
    }

    public Location GetLocation(AST.AST? node)
        => node is null ? Fallback : GetLocation(node.Start, node.End);
}
