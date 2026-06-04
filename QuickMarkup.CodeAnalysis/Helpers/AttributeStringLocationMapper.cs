using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.AST;

namespace QuickMarkup.CodeAnalysis.Helpers;

/// <summary>
/// Maps AST positions inside a [QuickMarkup("...")] attribute string
/// back to positions in the C# source file. Handles multi-line """ strings
/// with indentation stripping.
/// </summary>
public class AttributeStringLocationMapper
{
    readonly SyntaxTree? _syntaxTree;
    readonly TextLineCollection _textLines;
    readonly int _startLine;
    readonly int _startIndent;
    readonly bool _ok;

    public AttributeStringLocationMapper(AttributeData attribute, CancellationToken ct = default)
    {
        var syn = attribute.ApplicationSyntaxReference;
        _syntaxTree = syn?.SyntaxTree;
        _textLines = null!;

        if (syn is null || _syntaxTree is null)
            return;

        if (syn.GetSyntax(ct) is not AttributeSyntax attrSyntax)
            return;

        if (attrSyntax.ArgumentList?.Arguments[0].Expression is not LiteralExpressionSyntax strLitSyntax)
            return;

        var text = _syntaxTree.GetText(ct);
        _textLines = text.Lines;

        var strLitSpan = strLitSyntax.Span;
        var lpspan = _textLines.GetLinePositionSpan(strLitSpan);
        var startLine = lpspan.Start.Line;
        var endLine = lpspan.End.Line;

        if (startLine == endLine)
            return;

        // Skip line with starting """
        startLine++;
        if (startLine >= _textLines.Count)
            return;

        var startLineSpan = _textLines[startLine].Span;
        // Skip empty lines
        for (int i = startLineSpan.Start; i < startLineSpan.End; i++)
        {
            if (!char.IsWhiteSpace(text[i])) goto skipIncrement;
        }
        startLine++;
    skipIncrement:

        // Get indentation from end line
        var endLineSpan = _textLines[endLine].Span;
        int indent = 0;
        while (indent < endLineSpan.End - endLineSpan.Start && text[endLineSpan.Start + indent] is ' ' or '\t')
        {
            indent++;
        }

        _startLine = startLine;
        _startIndent = indent;
        _ok = true;
    }

    public bool IsValid => _ok && _syntaxTree is not null;

    public Location GetLocation(Position start, Position end)
    {
        if (!_ok || _syntaxTree is null)
            return Location.Create(_syntaxTree ?? null!, new TextSpan(0, 0));

        var startPos = _textLines.GetPosition(new LinePosition(_startLine + start.Line, _startIndent + start.Char));
        var endPos = _textLines.GetPosition(new LinePosition(_startLine + end.Line, _startIndent + end.Char + 1));
        return Location.Create(_syntaxTree, new TextSpan(startPos, endPos - startPos));
    }

    public Location GetLocation(AST.AST? node)
    {
        if (node is null || !_ok || _syntaxTree is null)
            return Location.Create(_syntaxTree ?? null!, new TextSpan(0, 0));
        return GetLocation(node.Start, node.End);
    }
}
