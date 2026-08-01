using Get.LangSupport;
using Get.Lexer;
using QuickMarkup.Textmate;
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
    // Individual states are bit values so regex rules can target combined stages.
    [Flags]
    public enum LexerStates
    {
        CatchAll = 0,
        Usings = 1 << 0,
        Props = 1 << 1,
        BeforeRoot = 1 << 2,
        InsideQMOpenTag = 1 << 3,
        InsideQMCloseTag = 1 << 4,
        InsideForeign = 1 << 5,
        InsideTickForeign = 1 << 6,
        InsideBlockComment = 1 << 7,
        InsideLineComment = 1 << 8,
        End = 1 << 9,
        BeforeRefs = 1 << 10,
        BeforeRefsNamespace = 1 << 11,
        BeforeRefsClassName = 1 << 12,
        BeforeRefsBaseTypes = 1 << 13,

        PropsAndBeforeRoot = Props | BeforeRoot,
        PropsAndInsideQMOpenTag = Props | InsideQMOpenTag,
        PropsBeforeRootAndInsideQMOpenTag = Props | BeforeRoot | InsideQMOpenTag,
        BeforeRootAndInsideQMOpenTag = BeforeRoot | InsideQMOpenTag,
        QMTag = InsideQMOpenTag | InsideQMCloseTag,
        CommentsAllowed = Usings | Props | BeforeRoot | InsideQMOpenTag | InsideQMCloseTag,
        CatchAllStates = BeforeRoot | InsideQMOpenTag | InsideQMCloseTag | InsideBlockComment | InsideForeign | InsideLineComment
    }
    [CompileTimeConflictCheck]
    public enum Tokens
    {
        [Regex<string>(@"using[^<\r\n]*;", nameof(Identity), State = LexerStates.Usings)]
        [TextmateScope("keyword.import", Priority = (int)TextmateOrder.Keywords, AddBoundary = false, Regexes = ["using"])]
        UsingStatement,
        [Regex(@"", nameof(GotoBeforeRefs), ShouldReturnToken = false, State = LexerStates.Usings)]
        UsingHelper,
        [Regex(@"", nameof(GotoBeforeRoot), ShouldReturnToken = false, State = LexerStates.Props)]
        PropsHelper,

        [Regex(@"<", nameof(QMOpenTagOpenHandler), State = LexerStates.BeforeRootAndInsideQMOpenTag)]
        [TextmateTagScope(TagMatchKind.Open, Priority = (int)TextmateOrder.Tag)]
        QMOpenTagOpen,
        [Regex<string>(@"<setup>[^]*</setup>", nameof(GetScriptInner), State = LexerStates.BeforeRoot)]
        [TextmateTagScope(TagMatchKind.Setup, Priority = (int)TextmateOrder.StringChar)]
        Setup,
        // Temporary disabled
        [Type<string>]
        // [Regex<string>(@"<setup[ \t\r\n]+async>[^]*</setup>", nameof(GetScriptInnerAsync), State = LexerStates.BeforeRoot)]
        [TextmateTagScope(TagMatchKind.Setup, Priority = (int)TextmateOrder.StringChar)]
        SetupAsync,
        [Regex<string>(@"[a-zA-Z_][a-zA-Z0-9_]*", nameof(Identity), State = LexerStates.Props | LexerStates.BeforeRoot | LexerStates.QMTag)]
        [Regex<string>(@"@[a-zA-Z_][a-zA-Z0-9]*", nameof(Identity), State = LexerStates.InsideQMOpenTag)]
        [TextmateOtherVariableScope(VariableType.Other, Priority = (int)TextmateOrder.Identifier)]
        Identifier,
        [Regex(@"=", State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [TextmateKeywordOperatorScope(OperatorType.Assignment, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Equal,
        [Regex(@";", State = LexerStates.PropsAndBeforeRoot)]
        [TextmatePunctuationScope("terminator", Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Semicolon,
        [Regex(@"=>", State = LexerStates.PropsAndInsideQMOpenTag)]
        [TextmateKeywordOperatorScope(OperatorType.Assignment, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        EqualArrowRight,
        [Regex(@"<=>", State = LexerStates.InsideQMOpenTag)]
        [TextmateKeywordOperatorScope(OperatorType.Comparison, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        EqualArrowLeftRight,
        [Regex(@"\+=", State = LexerStates.InsideQMOpenTag)]
        [TextmateKeywordOperatorScope(OperatorType.Assignment, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        AddEqual,
        [Regex(@"\.", State = LexerStates.QMTag)]
        [TextmatePunctuationSeparatorScope(PunctuationSeparatorType.Dot, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Dot,
        [Regex(@",", State = LexerStates.PropsAndInsideQMOpenTag)]
        [TextmatePunctuationSeparatorScope(PunctuationSeparatorType.Comma, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Comma,
        [Regex(@"\[", State = LexerStates.Props)]
        [TextmatePunctuationScope(PunctuationType.Bracket, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        OpenSquareBracket,
        [Regex(@"\]", State = LexerStates.Props)]
        [TextmatePunctuationScope(PunctuationType.Bracket, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        CloseSquareBracket,
        [Regex(@":", State = LexerStates.Props)]
        [TextmatePunctuationSeparatorScope(PunctuationSeparatorType.Colon, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Colon,
        [Regex(@"\?", State = LexerStates.PropsAndBeforeRoot | LexerStates.InsideQMOpenTag)]
        [TextmateKeywordOperatorScope(OperatorType.Ternary, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        QuestionMark,
        [Regex(@"!", State = LexerStates.InsideQMOpenTag)]
        [TextmateKeywordOperatorScope(OperatorType.Logical, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Not,
        [Regex<string>("""
            "([^\r\n\"\\]|(\\(n|t|r|\'|\")))*"
            """, nameof(StringUnescape), State = LexerStates.Props)]
        [Regex<string>("""
            "([^\r\n\"\\]|(\\(n|t|r|\'|\")))*"
            """, nameof(StringUnescape), State = LexerStates.InsideQMOpenTag)]
        [QuickMarkupStringQuotedScope(StringQuotedType.Double, Priority = (int)TextmateOrder.StringChar)]
        String,
        [Regex(@"required", State = LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Required,
        [Regex(@"provide", State = LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Provide,
        [Regex(@"inject", State = LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Inject,
        [Regex(@"as", State = LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        As,
        [Regex(@"async", State = LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Async,
        [Regex(@"private", State = LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Private,
        [Regex(@"public", State = LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Public,
        [Regex(@"static", State = LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Static,
        [Regex(@"set", State = LexerStates.Props, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Set,
        [Regex<bool>(@"true", nameof(TrueValue), State = LexerStates.PropsBeforeRootAndInsideQMOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [Regex<bool>(@"false", nameof(FalseValue), State = LexerStates.PropsBeforeRootAndInsideQMOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateConstantLanguageScope(ConstantLanguageType.Boolean, Priority = (int)TextmateOrder.Keywords)]
        Boolean,
        [Regex(@"null", State = LexerStates.PropsBeforeRootAndInsideQMOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateConstantLanguageScope(ConstantLanguageType.Boolean, Priority = (int)TextmateOrder.Keywords)]
        Null,
        [Regex(@"default", State = LexerStates.PropsBeforeRootAndInsideQMOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateConstantLanguageScope(ConstantLanguageType.Boolean, Priority = (int)TextmateOrder.Keywords)]
        Default,
        [Regex<int>(@"-[0-9][0-9_]*", nameof(ParseInt), State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [Regex<int>(@"[0-9][0-9_]*", nameof(ParseInt), State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [TextmateConstantNumericScope(NumericType.Decimal, Priority = (int)TextmateOrder.Number, Regexes = [@"(-|)[0-9][0-9_]*"])]
        [Regex<int>(@"0x[0-9a-fA-F]+", nameof(ParseHex), State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [TextmateConstantNumericScope(NumericType.Hex, Priority = (int)TextmateOrder.Number, Regexes = [@"0x[0-9a-fA-F]+"])]
        [Regex<int>(@"0b[01]+", nameof(ParseBinary), State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [TextmateConstantNumericScope(NumericType.Binary, Priority = (int)TextmateOrder.Number, Regexes = [@"0b[01]+"])]
        Integer,
        [Regex<double>(@"-[0-9][0-9_]*\.[0-9][0-9_]*", nameof(ParseDouble), State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [Regex<double>(@"[0-9][0-9_]*\.[0-9][0-9_]*", nameof(ParseDouble), State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [TextmateConstantNumericScope(NumericType.Decimal, Priority = (int)TextmateOrder.Number, Regexes = [@"(-|)[0-9][0-9_]*\.[0-9][0-9_]*"])]
        Double,
        [Regex<string>(@"-/", nameof(HandleForeignEnd), State = LexerStates.InsideForeign)]
        [Regex<string>(@"`", nameof(HandleForeignEnd), State = LexerStates.InsideTickForeign)]
        [QuickMarkupEmbeddedCSharpScope(@"`", @"`", Priority = (int)TextmateOrder.StringChar)]
        [QuickMarkupEmbeddedCSharpScope(@"/-", @"-/", Priority = (int)TextmateOrder.StringChar)]
        Foreign,
        [Regex(@"/-", nameof(HandleForeignStart), ShouldReturnToken = false, State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [Regex(@"`", nameof(HandleCuryForeignStart), ShouldReturnToken = false, State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        // disallow as conflicting with for block structure
        //[Regex(@"\{", nameof(HandleCuryForeignStart), ShouldReturnToken = false, State = LexerStates.BeforeRoot)]
        [Regex(@"[^\-/]+", nameof(AppendForeign), ShouldReturnToken = false, State = LexerStates.InsideForeign)]
        [Regex(@"[\-/]", nameof(AppendForeign), ShouldReturnToken = false, State = LexerStates.InsideForeign)]
        [Regex(@"[^`]+", nameof(AppendForeign), ShouldReturnToken = false, State = LexerStates.InsideTickForeign)]
        ForeignHelperToken,
        [Regex(@">", nameof(QMOpenTagCloseHandler), State = LexerStates.InsideQMOpenTag)]
        QMOpenTagClose,
        [Regex(@"/>", nameof(QMOpenTagAutoCloseHandler), State = LexerStates.InsideQMOpenTag)]
        QMOpenTagCloseAuto,
        [Regex(@"</", nameof(QMCloseTagOpenHandler), State = LexerStates.BeforeRoot)]
        [TextmateTagScope(TagMatchKind.Close, Priority = (int)TextmateOrder.Tag)]
        QMCloseTagOpen,
        [Regex(@">", nameof(QMCloseTagCloseHandler), State = LexerStates.InsideQMCloseTag)]
        QMCloseTagClose,
        [Regex(@"ref", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Other, Priority = (int)TextmateOrder.Keywords)]
        Ref,
        // HEADER TOKENS (namespace, class declaration, base types)
        [Regex(@"namespace", nameof(HandleNamespaceKeyword), State = LexerStates.BeforeRefs, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        NamespaceKw,
        [Regex(@"class", nameof(HandleClassKeyword), State = LexerStates.BeforeRefs, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        ClassKw,
        [Regex(@"component", nameof(HandleComponentKeyword), State = LexerStates.BeforeRefs, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        ComponentKw,
        [Regex(@"fragment", nameof(HandleFragmentKeyword), State = LexerStates.BeforeRefs, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        FragmentKw,
        [Regex<string>(@"[a-zA-Z_][a-zA-Z0-9_]*", nameof(Identity), State = LexerStates.BeforeRefsNamespace | LexerStates.BeforeRefsClassName)]
        HeaderIdentifier,
        [Regex(@"\.", State = LexerStates.BeforeRefsNamespace)]
        [TextmatePunctuationSeparatorScope(PunctuationSeparatorType.Dot, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        HeaderDot,
        [Regex(@";", nameof(HandleNamespaceSemicolon), State = LexerStates.BeforeRefsNamespace)]
        [TextmatePunctuationScope("terminator", Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        NamespaceSemicolon,
        [Regex(@":", nameof(HandleClassColon), State = LexerStates.BeforeRefsClassName)]
        [TextmatePunctuationSeparatorScope(PunctuationSeparatorType.Colon, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        ClassColon,
        [Regex(@";", nameof(HandleClassSemicolon), State = LexerStates.BeforeRefsClassName)]
        [TextmatePunctuationScope("terminator", Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        ClassSemicolon,
        [Regex<string>(@"[^;]+", nameof(Identity), State = LexerStates.BeforeRefsBaseTypes)]
        // [TextmateScope("storage.type", Priority = (int)TextmateOrder.Identifier)]
        RawBaseTypes,
        [Regex(@";", nameof(HandleBaseTypesSemicolon), State = LexerStates.BeforeRefsBaseTypes)]
        [TextmatePunctuationScope("terminator", Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        BaseTypesSemicolon,
        [Regex(@"[ \t\r\n]+", ShouldReturnToken = false, State = LexerStates.BeforeRefsNamespace | LexerStates.BeforeRefsClassName | LexerStates.BeforeRefs)]
        HeaderWhitespace,
        [Regex(@"", nameof(GotoProps), ShouldReturnToken = false, State = LexerStates.BeforeRefs)]
        BeforeRefsHelper,

        [Regex(@"var", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Control, Priority = (int)TextmateOrder.Keywords)]
        Var,
        [Regex(@"foreach", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Control, Priority = (int)TextmateOrder.Keywords)]
        Foreach,
        [Regex(@"await", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Control, Priority = (int)TextmateOrder.Keywords)]
        Await,
        [Regex(@"with", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Control, Priority = (int)TextmateOrder.Keywords)]
        With,
        [Regex(@"catch", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Control, Priority = (int)TextmateOrder.Keywords)]
        Catch,
        [Regex(@"then", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Control, Priority = (int)TextmateOrder.Keywords)]
        Then,
        [Regex(@"if", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Control, Priority = (int)TextmateOrder.Keywords)]
        If,
        [Regex(@"else", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Control, Priority = (int)TextmateOrder.Keywords)]
        Else,
        [Regex(@"in", State = LexerStates.BeforeRoot, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Control, Priority = (int)TextmateOrder.Keywords)]
        In,
        [Regex(@"\.\.", State = LexerStates.BeforeRoot)]
        [TextmateKeywordOperatorScope(OperatorType.Arithmetic, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        Range,
        [Regex(@"\(", State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [TextmatePunctuationScope(PunctuationType.Bracket, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        OpenBracket,
        [Regex(@"\)", State = LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        [TextmatePunctuationScope(PunctuationType.Bracket, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        CloseBracket,
        [Regex(@"\{", nameof(HandleOpenCuryBracket), State = LexerStates.BeforeRoot | LexerStates.InsideQMOpenTag)]
        [TextmatePunctuationScope(PunctuationType.Bracket, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        OpenCuryBracket,
        [Regex(@"\}", nameof(HandleCloseCuryBracket), State = LexerStates.BeforeRoot | LexerStates.InsideQMOpenTag)]
        [TextmatePunctuationScope(PunctuationType.Bracket, Priority = (int)TextmateOrder.OperatorsAndPunctuations)]
        CloseCuryBracket,
        [Regex(@"template", State = LexerStates.PropsAndInsideQMOpenTag, Order = (int)Order.KeywordAndSpecialSyntax)]
        [TextmateKeywordScope(KeywordType.Declaration, Priority = (int)TextmateOrder.Keywords)]
        Template,
        // + cuz it will not invoke the empty rule
        [Regex(@"[ \t\r\n]+", ShouldReturnToken = false, State = LexerStates.Usings | LexerStates.PropsBeforeRootAndInsideQMOpenTag)]
        Whitespace,

        // line comments
        [Regex(@"//[^\r\n]*[\r\n]", ShouldReturnToken = false, State = LexerStates.CommentsAllowed)]


        // block comments
        [Regex(@"/\*", nameof(HandleBlockCommentStart), ShouldReturnToken = false, State = LexerStates.CommentsAllowed)]
        [Regex(@"[^\*/]*", ShouldReturnToken = false, State = LexerStates.InsideBlockComment)]
        [Regex(@"[\*/]", ShouldReturnToken = false, State = LexerStates.InsideBlockComment)]
        [Regex(@"\*/", nameof(HandleBlockCommentEnd), ShouldReturnToken = false, State = LexerStates.InsideBlockComment)]
        [QuickMarkupCommentScope(Priority = (int)TextmateOrder.LineComment, Regexes = [@"//[^\r\n]*"])]
        [QuickMarkupCommentScope(Priority = (int)TextmateOrder.BlockComment, Begin = "/\\*", End = "\\*/")]
        Comment,
        [Regex<string>(@"[^]", nameof(Identity), State = LexerStates.CatchAllStates, Order = (int)Order.CatchAll)]
        UnexpectedCharacter,
        [Regex(@"", nameof(CatchAllHandler), ShouldReturnToken = false, State = LexerStates.CatchAllStates, Order = (int)Order.CatchAll)]
        CatchAllHelper,
        [Regex(@"do_not_match", ShouldReturnToken = false, State = LexerStates.End)]
        EndHelpder
    }
    private partial IToken<Tokens> HandleNamespaceKeyword()
    {
        GoTo(LexerStates.BeforeRefsNamespace);
        return Make(Tokens.NamespaceKw);
    }
    private partial IToken<Tokens> HandleClassKeyword()
    {
        GoTo(LexerStates.BeforeRefsClassName);
        return Make(Tokens.ClassKw);
    }
    private partial IToken<Tokens> HandleComponentKeyword()
    {
        GoTo(LexerStates.BeforeRefsClassName);
        return Make(Tokens.ComponentKw);
    }
    private partial IToken<Tokens> HandleFragmentKeyword()
    {
        return Make(Tokens.FragmentKw);
    }
    private partial IToken<Tokens> HandleNamespaceSemicolon()
    {
        GoTo(LexerStates.BeforeRefs);
        return Make(Tokens.NamespaceSemicolon);
    }
    private partial IToken<Tokens> HandleClassColon()
    {
        GoTo(LexerStates.BeforeRefsBaseTypes);
        return Make(Tokens.ClassColon);
    }
    private partial IToken<Tokens> HandleClassSemicolon()
    {
        GoTo(LexerStates.Props);
        return Make(Tokens.ClassSemicolon);
    }
    private partial IToken<Tokens> HandleBaseTypesSemicolon()
    {
        GoTo(LexerStates.Props);
        return Make(Tokens.BaseTypesSemicolon);
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
    private partial void GotoBeforeRefs()
    {
        GoTo(LexerStates.BeforeRefs);
    }
    private partial void GotoBeforeRoot()
    {
        GoTo(LexerStates.BeforeRoot);
    }
    private partial string Identity() => MatchedText;
    private partial string GetScriptInner() => MatchedText["<setup>".Length..^"</setup>".Length];
    // private partial string GetScriptInnerAsync() => MatchedText[(MatchedText.IndexOf('>') + 1)..^"</setup>".Length];
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
        GoTo(LexerStates.InsideTickForeign);
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
    int TemplateBodyDepth;
    private partial IToken<Tokens> HandleOpenCuryBracket()
    {
        if (CurrentState is LexerStates.InsideQMOpenTag)
        {
            TemplateBodyDepth = 1;
            GoTo(LexerStates.BeforeRoot);
        }
        else if (TemplateBodyDepth > 0)
        {
            TemplateBodyDepth++;
        }
        return Make(Tokens.OpenCuryBracket);
    }
    private partial IToken<Tokens> HandleCloseCuryBracket()
    {
        if (TemplateBodyDepth > 0)
        {
            TemplateBodyDepth--;
            if (TemplateBodyDepth == 0)
                GoTo(LexerStates.InsideQMOpenTag);
        }
        return Make(Tokens.CloseCuryBracket);
    }
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
        Tag = 4,
        StringChar = 5,
        LineComment = 6,
        BlockComment = 7
    }
}
