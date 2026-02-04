using Get.LangSupport;
using Get.Lexer;
using Get.PLShared;
using Get.RegexMachine;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using static QuickMarkup.Parser.QuickMarkupLexer;
namespace QuickMarkup.Parser;

[Lexer<Tokens>]
public partial class QuickMarkupLexer(ITextSeekable text, LexerStates initState = LexerStates.Usings) : LexerBase<LexerStates, Tokens>(text, initState)
{
    public enum LexerStates
    {
        CatchAll,
        Usings,
        Props,
        BeforeRoot,
        InsideQMOpenTag,
        InsideQMCloseTag,
        InsideForeign,
        InsideCuryForeign,
        InsideBlockComment,
        InsideLineComment,
        End
    }
    [CompileTimeConflictCheck]
    public enum Tokens
    {
        [Regex<string>(@"using[^<\r\n]*;", nameof(Identity), State = (int)LexerStates.Usings)]
        UsingStatement,
        [Regex(@"", nameof(GotoProps), ShouldReturnToken = false, State = (int)LexerStates.Usings)]
        UsingHelper,
        [Regex(@"", nameof(GotoBeforeRoot), ShouldReturnToken = false, State = (int)LexerStates.Props)]
        PropsHelper,

        [Regex(@"<", nameof(QMOpenTagOpenHandler), State = (int)LexerStates.BeforeRoot)]
        [Regex(@"<", nameof(QMOpenTagOpenHandler), State = (int)LexerStates.InsideQMOpenTag)]
        QMOpenTagOpen,
        [Regex<string>(@"<setup>[^]*</setup>", nameof(GetScriptInner), State = (int)LexerStates.BeforeRoot)]
        Setup,
        [Regex<string>(@"[a-zA-Z_][a-zA-Z0-9_]*", nameof(Identity), State = (int)LexerStates.Props)]
        [Regex<string>(@"[a-zA-Z_][a-zA-Z0-9_]*", nameof(Identity), State = (int)LexerStates.InsideQMOpenTag)]
        [Regex<string>(@"[a-zA-Z_][a-zA-Z0-9_]*", nameof(Identity), State = (int)LexerStates.InsideQMCloseTag)]
        [Regex<string>(@"[a-zA-Z_][a-zA-Z0-9_]*", nameof(Identity), State = (int)LexerStates.BeforeRoot)]
        [TextmateOtherVariableScope(VariableType.Other, Priority = (int)TextmateOrder.Identifier)]
        Identifier,
        [Regex(@"@[a-zA-Z_][a-zA-Z0-9]*", State = (int)LexerStates.InsideQMOpenTag)]
        [TextmateOtherVariableScope(VariableType.Other, Priority = (int)TextmateOrder.Identifier)]
        EventIdentifier,
        [Regex(@"=", State = (int)LexerStates.Props)]
        [Regex(@"=", State = (int)LexerStates.InsideQMOpenTag)]
        [Regex(@"=", State = (int)LexerStates.BeforeRoot)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Equal,
        [Regex(@";", State = (int)LexerStates.Props)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Semicolon,
        [Regex(@"=>", State = (int)LexerStates.Props)]
        [Regex(@"=>", State = (int)LexerStates.InsideQMOpenTag)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        EqualArrowRight,
        [Regex(@"\+=", State = (int)LexerStates.InsideQMOpenTag)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        AddEqual,
        [Regex(@"\.", State = (int)LexerStates.InsideQMOpenTag)]
        [Regex(@"\.", State = (int)LexerStates.InsideQMCloseTag)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Dot,
        [Regex(@",", State = (int)LexerStates.InsideQMOpenTag)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Comma,
        [Regex(@"\?", State = (int)LexerStates.Props)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        QuestionMark,
        [Regex(@"!", State = (int)LexerStates.InsideQMOpenTag)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Not,
        [Regex<string>("""
            "([^\r\n\"\\]|(\\(n|t|r|\'|\")))*"
            """, nameof(StringUnescape), State = (int)LexerStates.Props)]
        [Regex<string>("""
            "([^\r\n\"\\]|(\\(n|t|r|\'|\")))*"
            """, nameof(StringUnescape), State = (int)LexerStates.InsideQMOpenTag)]
        [TextmateStringQuotedScope(StringQuotedType.Double, Priority = (int)TextmateOrder.StringChar)]
        String,
        [Regex(@"root", State = (int)LexerStates.InsideQMOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [Regex(@"root", State = (int)LexerStates.InsideQMCloseTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        RootKeyword,
        [Regex(@"private", State = (int)LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Private,
        [Regex(@"set", State = (int)LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Set,
        [Regex<bool>(@"true", nameof(TrueValue), State = (int)LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [Regex<bool>(@"false", nameof(FalseValue), State = (int)LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [Regex<bool>(@"true", nameof(TrueValue), State = (int)LexerStates.InsideQMOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [Regex<bool>(@"false", nameof(FalseValue), State = (int)LexerStates.InsideQMOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateConstantLanguageScope(ConstantLanguageType.Boolean, Priority = (int)TextmateOrder.Keywords)]
        Boolean,
        [Regex(@"null", State = (int)LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateConstantLanguageScope(ConstantLanguageType.Boolean, Priority = (int)TextmateOrder.Keywords)]
        Null,
        [Regex(@"default", State = (int)LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateConstantLanguageScope(ConstantLanguageType.Boolean, Priority = (int)TextmateOrder.Keywords)]
        Default,
        [Regex<int>(@"-[0-9][0-9_]*", nameof(ParseInt), State = (int)LexerStates.Props)]
        [Regex<int>(@"[0-9][0-9_]*", nameof(ParseInt), State = (int)LexerStates.Props)]
        [Regex<int>(@"-[0-9][0-9_]*", nameof(ParseInt), State = (int)LexerStates.InsideQMOpenTag)]
        [Regex<int>(@"[0-9][0-9_]*", nameof(ParseInt), State = (int)LexerStates.InsideQMOpenTag)]
        [Regex<int>(@"-[0-9][0-9_]*", nameof(ParseInt), State = (int)LexerStates.BeforeRoot)]
        [Regex<int>(@"[0-9][0-9_]*", nameof(ParseInt), State = (int)LexerStates.BeforeRoot)]
        [TextmateConstantNumericScope(NumericType.Decimal, Priority = (int)TextmateOrder.Number, Regexes = [@"(-|)[0-9][0-9_]*"])]
        [Regex<int>(@"0x[0-9a-fA-F]+", nameof(ParseHex), State = (int)LexerStates.Props)]
        [Regex<int>(@"0x[0-9a-fA-F]+", nameof(ParseHex), State = (int)LexerStates.InsideQMOpenTag)]
        [Regex<int>(@"0x[0-9a-fA-F]+", nameof(ParseHex), State = (int)LexerStates.BeforeRoot)]
        [TextmateConstantNumericScope(NumericType.Hex, Priority = (int)TextmateOrder.Number, Regexes = [@"0x[0-9a-fA-F]+"])]
        [Regex<int>(@"0b[01]+", nameof(ParseBinary), State = (int)LexerStates.Props)]
        [Regex<int>(@"0b[01]+", nameof(ParseBinary), State = (int)LexerStates.InsideQMOpenTag)]
        [Regex<int>(@"0b[01]+", nameof(ParseBinary), State = (int)LexerStates.BeforeRoot)]
        [TextmateConstantNumericScope(NumericType.Binary, Priority = (int)TextmateOrder.Number, Regexes = [@"0b[01]+"])]
        Integer,
        [Regex<double>(@"-[0-9][0-9_]*\.[0-9][0-9_]*", nameof(ParseDouble), State = (int)LexerStates.Props)]
        [Regex<double>(@"[0-9][0-9_]*\.[0-9][0-9_]*", nameof(ParseDouble), State = (int)LexerStates.Props)]
        [Regex<double>(@"-[0-9][0-9_]*\.[0-9][0-9_]*", nameof(ParseDouble), State = (int)LexerStates.InsideQMOpenTag)]
        [Regex<double>(@"[0-9][0-9_]*\.[0-9][0-9_]*", nameof(ParseDouble), State = (int)LexerStates.InsideQMOpenTag)]
        [Regex<double>(@"-[0-9][0-9_]*\.[0-9][0-9_]*", nameof(ParseDouble), State = (int)LexerStates.BeforeRoot)]
        [Regex<double>(@"[0-9][0-9_]*\.[0-9][0-9_]*", nameof(ParseDouble), State = (int)LexerStates.BeforeRoot)]
        [TextmateConstantNumericScope(NumericType.Decimal, Priority = (int)TextmateOrder.Number, Regexes = [@"(-|)[0-9][0-9_]*\.[0-9][0-9_]*"])]
        Double,
        [Regex<string>(@"-/", nameof(HandleForeignEnd), State = (int)LexerStates.InsideForeign)]
        [Regex<string>(@"\}", nameof(HandleForeignEnd), State = (int)LexerStates.InsideCuryForeign)]
        Foreign,
        [Regex(@"/-", nameof(HandleForeignStart), ShouldReturnToken = false, State = (int)LexerStates.Props)]
        [Regex(@"/-", nameof(HandleForeignStart), ShouldReturnToken = false, State = (int)LexerStates.InsideQMOpenTag)]
        [Regex(@"/-", nameof(HandleForeignStart), ShouldReturnToken = false, State = (int)LexerStates.BeforeRoot)]
        [Regex(@"\{", nameof(HandleCuryForeignStart), ShouldReturnToken = false, State = (int)LexerStates.Props)]
        [Regex(@"\{", nameof(HandleCuryForeignStart), ShouldReturnToken = false, State = (int)LexerStates.InsideQMOpenTag)]
        // disallow as conflicting with for block structure
        //[Regex(@"\{", nameof(HandleCuryForeignStart), ShouldReturnToken = false, State = (int)LexerStates.BeforeRoot)]
        [Regex(@"[^\-/]+", nameof(AppendForeign), ShouldReturnToken = false, State = (int)LexerStates.InsideForeign)]
        [Regex(@"[\-/]", nameof(AppendForeign), ShouldReturnToken = false, State = (int)LexerStates.InsideForeign)]
        [Regex(@"[^\}]+", nameof(AppendForeign), ShouldReturnToken = false, State = (int)LexerStates.InsideCuryForeign)]
        ForeignHelperToken,
        [Regex(@">", nameof(QMOpenTagCloseHandler), State = (int)LexerStates.InsideQMOpenTag)]
        QMOpenTagClose,
        [Regex(@"/>", nameof(QMOpenTagAutoCloseHandler), State = (int)LexerStates.InsideQMOpenTag)]
        QMOpenTagCloseAuto,
        [Regex(@"</", nameof(QMCloseTagOpenHandler), State = (int)LexerStates.BeforeRoot)]
        QMCloseTagOpen,
        [Regex(@">", nameof(QMCloseTagCloseHandler), State = (int)LexerStates.InsideQMCloseTag)]
        QMCloseTagClose,
        [Regex(@"for", State = (int)LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [Regex(@"foreach", State = (int)LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        For,
        [Regex(@"if", State = (int)LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        If,
        [Regex(@"else", State = (int)LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        Else,
        [Regex(@"in", State = (int)LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        In,
        [Regex(@"\.\.", State = (int)LexerStates.BeforeRoot)]
        Range,
        [Regex(@"\(", State = (int)LexerStates.BeforeRoot)]
        [Regex(@"\(", State = (int)LexerStates.InsideQMOpenTag)]
        OpenBracket,
        [Regex(@"\)", State = (int)LexerStates.BeforeRoot)]
        [Regex(@"\)", State = (int)LexerStates.InsideQMOpenTag)]
        CloseBracket,
        [Regex(@"\{", State = (int)LexerStates.BeforeRoot)]
        OpenCuryBracket,
        [Regex(@"\}", State = (int)LexerStates.BeforeRoot)]
        CloseCuryBracket,
        // don't output anything against whitespace
        [Regex(@"[ \t\r\n]+", ShouldReturnToken = false, State = (int)LexerStates.Usings)]
        [Regex(@"[ \t\r\n]+", ShouldReturnToken = false, State = (int)LexerStates.Props)]
        [Regex(@"[ \t\r\n]+", ShouldReturnToken = false, State = (int)LexerStates.BeforeRoot)]
        // + cuz it will not invoke the empty rule
        [Regex(@"[ \t\r\n]+", ShouldReturnToken = false, State = (int)LexerStates.InsideQMOpenTag)]
        Whitespace,

        // line comments
        [Regex(@"//[^\r\n]*[\r\n]", ShouldReturnToken = false, State = (int)LexerStates.Usings)]
        [Regex(@"//[^\r\n]*[\r\n]", ShouldReturnToken = false, State = (int)LexerStates.Props)]
        [Regex(@"//[^\r\n]*[\r\n]", ShouldReturnToken = false, State = (int)LexerStates.BeforeRoot)]
        [Regex(@"//[^\r\n]*[\r\n]", ShouldReturnToken = false, State = (int)LexerStates.InsideQMOpenTag)]
        [Regex(@"//[^\r\n]*[\r\n]", ShouldReturnToken = false, State = (int)LexerStates.InsideQMCloseTag)]


        // block comments
        [Regex(@"/\*", nameof(HandleBlockCommentStart), ShouldReturnToken = false, State = (int)LexerStates.Usings)]
        [Regex(@"/\*", nameof(HandleBlockCommentStart), ShouldReturnToken = false, State = (int)LexerStates.Props)]
        [Regex(@"/\*", nameof(HandleBlockCommentStart), ShouldReturnToken = false, State = (int)LexerStates.BeforeRoot)]
        [Regex(@"/\*", nameof(HandleBlockCommentStart), ShouldReturnToken = false, State = (int)LexerStates.InsideQMCloseTag)]
        [Regex(@"/\*", nameof(HandleBlockCommentStart), ShouldReturnToken = false, State = (int)LexerStates.InsideQMOpenTag)]
        [Regex(@"[^\*/]*", ShouldReturnToken = false, State = (int)LexerStates.InsideBlockComment)]
        [Regex(@"[\*/]", ShouldReturnToken = false, State = (int)LexerStates.InsideBlockComment)]
        [Regex(@"\*/", nameof(HandleBlockCommentEnd), ShouldReturnToken = false, State = (int)LexerStates.InsideBlockComment)]
        Comment,
        [Regex<string>(@"[^]+", nameof(Identity), State = (int)LexerStates.CatchAll, Order = (int)Order.CatchAll)]
        CatchAll,
        [Regex(@"", nameof(CatchAllHandler), ShouldReturnToken = false, State = (int)LexerStates.BeforeRoot, Order = (int)Order.CatchAll)]
        [Regex(@"", nameof(CatchAllHandler), ShouldReturnToken = false, State = (int)LexerStates.InsideQMOpenTag, Order = (int)Order.CatchAll)]
        [Regex(@"", nameof(CatchAllHandler), ShouldReturnToken = false, State = (int)LexerStates.InsideQMCloseTag, Order = (int)Order.CatchAll)]
        [Regex(@"", nameof(CatchAllHandler), ShouldReturnToken = false, State = (int)LexerStates.InsideBlockComment, Order = (int)Order.CatchAll)]
        [Regex(@"", nameof(CatchAllHandler), ShouldReturnToken = false, State = (int)LexerStates.InsideForeign, Order = (int)Order.CatchAll)]
        [Regex(@"", nameof(CatchAllHandler), ShouldReturnToken = false, State = (int)LexerStates.InsideLineComment, Order = (int)Order.CatchAll)]
        CatchAllHelper,
        [Regex(@"do_not_match", ShouldReturnToken = false, State = (int)LexerStates.End)]
        EndHelpder
    }
    private partial void CatchAllHandler()
    {
        if (!HasReachedEOF)
            GoTo(LexerStates.CatchAll);
        else
            GoTo(LexerStates.End);
    }
    //private partial string CaptureStart()
    //{
    //    GoTo(LexerStates.BeforeRoot);
    //    return MatchedText;
    //}
    private partial void GotoProps()
    {
        GoTo(LexerStates.Props);
    }
    private partial void GotoBeforeRoot()
    {
        GoTo(LexerStates.BeforeRoot);
    }
    private partial string Identity() => MatchedText;
    private partial string GetScriptInner() => MatchedText["<setup>".Length..^"</setup>".Length];
    private partial bool TrueValue() => true;
    private partial bool FalseValue() => false;
    private partial int ParseInt() => int.Parse(MatchedText.Replace("_", ""));
    private partial double ParseDouble() => double.Parse(MatchedText.Replace("_", ""));
    private partial int ParseHex() => Convert.ToInt32(MatchedText.Replace("_", "")[2..], 16);
    private partial int ParseBinary() => Convert.ToInt32(MatchedText.Replace("_", "")[2..], 2);
    string Foriegn = "";
    Stack<LexerStates> ForeignStoredStates = [];
    private partial void HandleForeignStart()
    {
        Foriegn = "";
        ForeignStoredStates.Push(CurrentState);
        GoTo(LexerStates.InsideForeign);
    }
    private partial void HandleCuryForeignStart()
    {
        Foriegn = "";
        ForeignStoredStates.Push(CurrentState);
        GoTo(LexerStates.InsideForeign);
    }
    private partial string HandleForeignEnd()
    {
        GoTo(ForeignStoredStates.Pop());
        return Foriegn;
    }
    Stack<LexerStates> BlockCommentStoredStates = [];
    private partial void HandleBlockCommentStart()
    {
        BlockCommentStoredStates.Push(CurrentState);
        GoTo(LexerStates.InsideBlockComment);
    }
    private partial void HandleBlockCommentEnd()
    {
        GoTo(BlockCommentStoredStates.Pop());
    }
    private partial void AppendForeign()
    {
        Foriegn += MatchedText;
    }
    Stack<LexerStates> OpenTagStoredStates = [];
    private partial IToken<Tokens> QMOpenTagOpenHandler()
    {
        OpenTagStoredStates.Push(CurrentState);
        GoTo(LexerStates.InsideQMOpenTag);
        return Make(Tokens.QMOpenTagOpen);
    }
    private partial IToken<Tokens> QMCloseTagOpenHandler()
    {
        GoTo(LexerStates.InsideQMCloseTag);
        return Make(Tokens.QMCloseTagOpen);
    }
    private partial IToken<Tokens> QMOpenTagCloseHandler()
    {
        GoTo(LexerStates.BeforeRoot);
        return Make(Tokens.QMOpenTagClose);
    }
    private partial IToken<Tokens> QMCloseTagCloseHandler()
    {
        GoTo(OpenTagStoredStates.Pop());
        return Make(Tokens.QMCloseTagClose);
    }
    private partial IToken<Tokens> QMOpenTagAutoCloseHandler()
    {
        GoTo(OpenTagStoredStates.Pop());
        return Make(Tokens.QMOpenTagCloseAuto);
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
        CatchAll = -2,
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
