using Get.LangSupport;
using Get.Lexer;
using Get.PLShared;
using Get.RegexMachine;
using System.Diagnostics;
using System.Globalization;
using System.Text;
namespace QuickMarkup.Parser;

[Lexer<Tokens>]
public partial class QuickMarkupLexer(ITextSeekable text) : LexerBase<QuickMarkupLexer.LexerStates, QuickMarkupLexer.Tokens>(text, LexerStates.Start)
{
    public enum LexerStates
    {
        Start,
        Default,
        InsideXMLOpenTag,
        InsideXMLCloseTag,
        InsideForeign
    }
    [CompileTimeConflictCheck]
    public enum Tokens
    {
        [Regex<string>(@"[^<]*", nameof(CaptureStart), State = (int)LexerStates.Start)]
        UsingStatements,
        [Regex(@"<", nameof(XMLOpenTagOpenHandler), State = (int)LexerStates.Default)]
        XMLOpenTagOpen,
        [Regex<string>(@"<script>[^]*</script>", nameof(GetScriptInner), State = (int)LexerStates.Default)]
        Script,
        [Regex<string>(@"<props>[^]*</props>", nameof(GetPropsInner), State = (int)LexerStates.Default)]
        Props,
        [Regex<string>(@"[a-zA-Z][a-zA-Z0-9]*", nameof(Identity), State = (int)LexerStates.InsideXMLOpenTag)]
        [Regex<string>(@"[a-zA-Z][a-zA-Z0-9]*", nameof(Identity), State = (int)LexerStates.InsideXMLCloseTag)]
        [TextmateOtherVariableScope(VariableType.Other, Priority = (int)TextmateOrder.Identifier)]
        Identifier,
        [Regex(@"@[a-zA-Z][a-zA-Z0-9]*", State = (int)LexerStates.InsideXMLOpenTag)]
        [TextmateOtherVariableScope(VariableType.Other, Priority = (int)TextmateOrder.Identifier)]
        EventIdentifier,
        [Regex(@"=", State = (int)LexerStates.InsideXMLOpenTag)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Equal,
        [Regex(@"\.", State = (int)LexerStates.InsideXMLOpenTag)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Dot,
        [Regex(@"!", State = (int)LexerStates.InsideXMLOpenTag)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Not,
        [Regex<string>("""
            "([^\r\n\"\\]|(\\(n|t|r|\'|\")))*"
            """, nameof(StringUnescape), State = (int)LexerStates.InsideXMLOpenTag)]
        [TextmateStringQuotedScope(StringQuotedType.Double, Priority = (int)TextmateOrder.StringChar)]
        String,
        [Regex(@"template", State = (int)LexerStates.InsideXMLOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [Regex(@"template", State = (int)LexerStates.InsideXMLCloseTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        TemplateKeyword,
        [Regex<bool>(@"true", nameof(TrueValue), State = (int)LexerStates.InsideXMLOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [Regex<bool>(@"false", nameof(FalseValue), State = (int)LexerStates.InsideXMLOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateConstantLanguageScope(ConstantLanguageType.Boolean, Priority = (int)TextmateOrder.Keywords)]
        Boolean,
        [Regex<int>(@"-[0-9][0-9_]*", nameof(ParseInt), State = (int)LexerStates.InsideXMLOpenTag)]
        [Regex<int>(@"[0-9][0-9_]*", nameof(ParseInt), State = (int)LexerStates.InsideXMLOpenTag)]
        [TextmateConstantNumericScope(NumericType.Decimal, Priority = (int)TextmateOrder.Number, Regexes = [@"(-|)[0-9][0-9_]*"])]
        [Regex<int>(@"0x[0-9a-fA-F]+", nameof(ParseHex), State = (int)LexerStates.InsideXMLOpenTag)]
        [TextmateConstantNumericScope(NumericType.Hex, Priority = (int)TextmateOrder.Number, Regexes = [@"0x[0-9a-fA-F]+"])]
        [Regex<int>(@"0b[01]+", nameof(ParseBinary), State = (int)LexerStates.InsideXMLOpenTag)]
        [TextmateConstantNumericScope(NumericType.Binary, Priority = (int)TextmateOrder.Number, Regexes = [@"0b[01]+"])]
        Integer,
        [Regex<string>(@"-/", nameof(HandleForeignEnd), State = (int)LexerStates.InsideForeign)]
        Foreign,
        [Regex(@"/-", nameof(HandleForeignStart), ShouldReturnToken = false, State = (int)LexerStates.InsideXMLOpenTag)]
        [Regex(@"[^\-/]+", nameof(AppendForeign), ShouldReturnToken = false, State = (int)LexerStates.InsideForeign)]
        ForeignHelperToken,
        [Regex(@">", nameof(XMLOpenTagCloseHandler), State = (int)LexerStates.InsideXMLOpenTag)]
        XMLOpenTagClose,
        [Regex(@"/>", nameof(XMLOpenTagAutoCloseHandler), State = (int)LexerStates.InsideXMLOpenTag)]
        XMLOpenTagCloseAuto,
        [Regex(@"</", nameof(XMLCloseTagOpenHandler), State = (int)LexerStates.Default)]
        XMLCloseTagOpen,
        [Regex(@">", nameof(XMLCloseTagCloseHandler), State = (int)LexerStates.InsideXMLCloseTag)]
        XMLCloseTagClose,
        // don't output anything against whitespace
        [Regex(@"[ \t\r\n]+", ShouldReturnToken = false, State = (int)LexerStates.Default)]
        // + cuz it will not invoke the empty rule
        [Regex(@"[ \t\r\n]+", ShouldReturnToken = false, State = (int)LexerStates.InsideXMLOpenTag)]
        Whitespace
    }
    private partial string CaptureStart()
    {
        GoTo(LexerStates.Default);
        return MatchedText;
    }
    private partial string Identity() => MatchedText;
    private partial string GetScriptInner() => MatchedText["<script>".Length..^"</script>".Length];
    private partial string GetPropsInner() => MatchedText["<props>".Length..^"</props>".Length];
    private partial bool TrueValue() => true;
    private partial bool FalseValue() => false;
    private partial int ParseInt() => int.Parse(MatchedText.Replace("_", ""));
    private partial int ParseHex() => Convert.ToInt32(MatchedText.Replace("_", "")[2..], 16);
    private partial int ParseBinary() => Convert.ToInt32(MatchedText.Replace("_", "")[2..], 2);
    string Foriegn = "";
    private partial void HandleForeignStart()
    {
        Foriegn = "";
        GoTo(LexerStates.InsideForeign);
    }
    private partial string HandleForeignEnd()
    {
        GoTo(LexerStates.InsideXMLOpenTag);
        return Foriegn;
    }
    private partial void AppendForeign()
    {
        Foriegn += MatchedText;
    }
    private partial IToken<Tokens> XMLOpenTagOpenHandler()
    {
        GoTo(LexerStates.InsideXMLOpenTag);
        return Make(Tokens.XMLOpenTagOpen);
    }
    private partial IToken<Tokens> XMLCloseTagOpenHandler()
    {
        GoTo(LexerStates.InsideXMLCloseTag);
        return Make(Tokens.XMLCloseTagOpen);
    }
    private partial IToken<Tokens> XMLOpenTagCloseHandler()
    {
        GoTo(LexerStates.Default);
        return Make(Tokens.XMLOpenTagClose);
    }
    private partial IToken<Tokens> XMLCloseTagCloseHandler()
    {
        GoTo(LexerStates.Default);
        return Make(Tokens.XMLCloseTagClose);
    }
    private partial IToken<Tokens> XMLOpenTagAutoCloseHandler()
    {
        GoTo(LexerStates.Default);
        return Make(Tokens.XMLOpenTagCloseAuto);
    }
    private partial string StringUnescape()
    {
        var ros = (ReadOnlySpan<char>)MatchedText;
        ros = ros[1..^1]; // remove first " and last "
        var sb = new StringBuilder(ros.Length);
        var enu = ros.GetEnumerator();
        while (enu.MoveNext())
        {
            if (enu.Current is not '\\')
            {
                sb.Append(enu.Current);
            }
            else
            {
                if (!enu.MoveNext())
                {
                    throw new UnreachableException("Regex should've make sure this");
                }
                sb.Append(EscapeChar(enu.Current));
            }
        }
        return sb.ToString();
    }
    static char EscapeChar(char charAfterSlash)
        => charAfterSlash switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            '\'' => '\'',
            '\"' => '\"',
            _ => throw new UnreachableException("Regex should've make sure this")
        };
    enum Order : int
    {
        Fallback = -1,
        Initial = 0,
        KeywordAndSpecialSyntax = 1,
        Comment = 2
    }
    enum TextmateOrder : int
    {
        Regular = 0,
        Identifier = 0,
        SpecialIdentifier = 1,
        Number = 2,
        OperatorsAndPunctuations = 2,
        Keywords = 3,
        StringChar = 4,
        LineComment = 5,
        BlockComment = 6
    }
}
