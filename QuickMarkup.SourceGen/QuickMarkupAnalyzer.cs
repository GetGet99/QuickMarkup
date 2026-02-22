using System.Collections.Immutable;
using Get.EasyCSharp.GeneratorTools;
using Get.Lexer;
using Get.Parser;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.AST;
using QuickMarkup.Parser;

namespace QuickMarkup.SourceGen;

[AddAttributeConverter(typeof(QuickMarkupAttribute), ParametersAsString = "\"\"")]
[DiagnosticAnalyzer(LanguageNames.CSharp)]
partial class QuickMarkupAnalyzer : DiagnosticAnalyzer
{
    IEnumerable<IToken<QuickMarkupLexer.Tokens>> Lex(string code)
    {
        // retry as it is flaky
        QuickMarkupLexer? lexer = null;
        for (int i = 0; i < 10; i++)
        {
            lexer = new QuickMarkupLexer(new StringTextSeeker(code));
            break;
        }
        lexer ??= new QuickMarkupLexer(new StringTextSeeker(code));
        return lexer.GetTokens();
    }
    ThreadLocal<QuickMarkupParser> ParserPerThread { get; } = new(static () =>
    {
        // retry as it is flaky
        for (int i = 0; i < 10; i++)
        {
            try
            {
                return new QuickMarkupParser();
            }
            catch
            {

            }
        }
        return new QuickMarkupParser();
    });

    QuickMarkupSFC Parse(IEnumerable<IToken<QuickMarkupLexer.Tokens>> tokens, out List<ErrorTerminalValue> errors)
    {
        return ParserPerThread.Value.Parse(tokens, out errors);
    }
    QuickMarkupSFC Parse(string code, out List<ErrorTerminalValue> errors)
    {
        return Parse(Lex(code), out errors);
    }

    static readonly string FullAttributeName;
    static QuickMarkupAnalyzer()
    {
        FullAttributeName = typeof(QuickMarkupAttribute).FullName;
    }
    static readonly SymbolDisplayFormat withoutNamespace = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        ParseErrorUnexpectedInput,
        ParseErrorUnexpectedEnding
    );
    readonly static DiagnosticDescriptor ParseErrorUnexpectedInput = new(
        "QM1001",
        "QuickMakrup parse error due to unexpected token",
        "Unexpected {0}",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    readonly static DiagnosticDescriptor ParseErrorUnexpectedEnding = new(
        "QM1002",
        "QuickMakrup parse error due to unexpected ending",
        "Expect {0} after the last parameter",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics
        );
        context.RegisterSyntaxNodeAction((genContext) =>
        {
            var syntaxNode = (TypeDeclarationSyntax)genContext.Node;
            // Filter out everything which has no attribute
            if (syntaxNode.AttributeLists.Count is 0) return;

            var compilation = genContext.Compilation;
            // Get Symbol
            if (genContext.SemanticModel.GetDeclaredSymbol(syntaxNode) is not ITypeSymbol typeSym)
                return;

            // Get Attributes
            var Class = compilation.GetTypeByMetadataName(FullAttributeName);
            if (Class is null) return;

            var attribute = (
                from x in typeSym.GetAttributes()
                where x.AttributeClass?.IsSubclassFrom(Class) ?? false
                select x
            ).FirstOrDefault();
            if (attribute is null) return;
            if (attribute.ConstructorArguments[0].Value is not string markup) return;
            var locationProvider = new LocationProvider(attribute, typeSym, genContext.CancellationToken);
            QuickMarkupSFC qm;
            List<ErrorTerminalValue> errors;
            try
            {
                qm = Parse(markup, out errors);
            }
            catch (LRParserRuntimeUnexpectedInputException e)
            {
                genContext.ReportDiagnostic(Diagnostic.Create(
                    ParseErrorUnexpectedInput,
                    locationProvider.GetLocation(e.UnexpectedElement.Start, e.UnexpectedElement.End),
                    e.UnexpectedElement
                ));
                goto exit;
            }
            catch (LRParserRuntimeUnexpectedEndingException e)
            {
                genContext.ReportDiagnostic(Diagnostic.Create(
                    ParseErrorUnexpectedEnding,
                    locationProvider.Fallback,
                    $"{string.Join(", ", (object?[])e.ExpectedInputs)} after the last parameter"
                ));
                goto exit;
            }
            foreach (var error in errors)
            {
                //var loc = locationProvider.GetLocation(error.Start, error.End);

                if (error.Value is LRParserRuntimeUnexpectedInputException unexpectedInput)
                    genContext.ReportDiagnostic(Diagnostic.Create(
                        ParseErrorUnexpectedInput,
                        locationProvider.GetLocation(unexpectedInput.UnexpectedElement.Start, unexpectedInput.UnexpectedElement.End),
                        unexpectedInput.UnexpectedElement
                    ));
                else if (error.Value is LRParserRuntimeUnexpectedEndingException unexpectedEnding)
                    genContext.ReportDiagnostic(Diagnostic.Create(
                        ParseErrorUnexpectedEnding,
                        locationProvider.GetLocation(error.Start, error.End),
                        $"{string.Join(", ", (object?[])unexpectedEnding.ExpectedInputs)} after the last parameter"
                    ));
            }
exit:
            ;
        }, SyntaxKind.ClassDeclaration);
    }
    class LocationProvider
    {
        Location fallback;
        SyntaxTree? syntaxTree = null;
        TextLineCollection textLines = null!;
        int startLine = 0;
        int startIndent = 0;
        bool ok;
        public LocationProvider(AttributeData attribute, ITypeSymbol typeSym, CancellationToken ct)
        {
            var syn = attribute.ApplicationSyntaxReference;
            syntaxTree = syn?.SyntaxTree;
            fallback = syn is null ? typeSym.Locations[0] : Location.Create(syn.SyntaxTree, syn.Span);
            if (syn is null) return;
            if (syntaxTree is null) return;
            var attrSyntax = syn?.GetSyntax(ct) as AttributeSyntax;
            // TO USE
            if (attrSyntax?.ArgumentList?.Arguments[0].Expression is not Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax strLitSyntax) return;
            var strLitSpan = strLitSyntax.Span;
            var text = syn!.SyntaxTree.GetText(ct);
            textLines = text.Lines;
            var lpspan = text.Lines.GetLinePositionSpan(strLitSpan);
            startLine = lpspan.Start.Line;
            var endLine = lpspan.End.Line;
            if (startLine == endLine)
            {
                // let's just not deal with """ single line """
                return;
            }
            // skip line with starting """
            startLine++;
            var startLineSpan = text.Lines[startLine].Span;
            // skip empty lines, they don't count towards string literal
            for (int i = startLineSpan.Start; i < startLineSpan.End; i++)
            {
                if (!char.IsWhiteSpace(text[i])) goto skipIncrement;
            }
            // skip first empty line
            startLine++;
    skipIncrement:
            // end line consists of whitespaces and """ charcater and whatever after it
            var endLineSpan = text.Lines[endLine].Span;
            // get the index of first " as the indent start
            int indent = 0;
            while (indent < endLineSpan.End - endLineSpan.Start && text[endLineSpan.Start + indent] is ' ' or '\t')
            {
                indent++;
            }
            startIndent = indent;
            ok = true;
        }
        public Location Fallback => fallback;
        public Location GetLocation(Position start, Position end)
        {
            if (!ok)
                return fallback;
            var startPos = textLines.GetPosition(new LinePosition(startLine + start.Line, startIndent + start.Char));
            var endPos = textLines.GetPosition(new LinePosition(startLine + end.Line, startIndent + end.Char));
            return Location.Create(syntaxTree!, new TextSpan(startPos, endPos - startPos));
        }
    }
    readonly record struct SourceGenContext(
        string Namespace,
        string TypeNameWithoutNamespace
    );
}
