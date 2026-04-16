using System.Collections.Immutable;
using Get.EasyCSharp.GeneratorTools;
using Get.Lexer;
using Get.Parser;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using QuickMarkup.AST;
using QuickMarkup.Parser;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;

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
            try
            {
                lexer = new QuickMarkupLexer(new StringTextSeeker(code));
                break;
            } catch { }
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


    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        ParseErrorUnexpectedInput,
        ParseErrorUnexpectedEnding,
        BindErrorGeneral,
        BindErrorChildrenTooMany,
        TagCloseMismatchedError
    );
    readonly static DiagnosticDescriptor ParseErrorUnexpectedInput = new(
        "QM1001",
        "QuickMarkup parse error due to unexpected token",
        "Unexpected {0}",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    readonly static DiagnosticDescriptor ParseErrorUnexpectedEnding = new(
        "QM1002",
        "QuickMarkup parse error due to unexpected ending",
        "Expect {0} after the last parameter",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    readonly static DiagnosticDescriptor BindErrorGeneral = new(
        "QM1003",
        "QuickMarkup general typing error",
        "{0}",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    readonly static DiagnosticDescriptor BindErrorChildrenTooMany = new(
        "QM1004",
        "QuickMarkup typing error too many children",
        "Too many children were provided, <{0}> expects {1}",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    readonly static DiagnosticDescriptor TagCloseMismatchedError = new(
        "QM1005",
        "QuickMarkup close tag mismatched",
        "Tag open and close mismatched: <{0}>...</{1}>",
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
        context.RegisterQuickMarkupAttributeInStringSyntaxAction((context, markupStr, locationProvider) =>
        {
            if (!markupStr.Target.TryGetTypeSymbol(context.Compilation, out var typeSym, out var failureReason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    BindErrorGeneral,
                    locationProvider.Fallback,
                    $"Internal Error while trying to get type symbol: {failureReason.Message}"
                ));
                return;
            }
            QuickMarkupSFC qm;
            List<ErrorTerminalValue> errors;
            try
            {
                qm = Parse(markupStr.MarkupString, out errors);
            }
            catch (LRParserRuntimeUnexpectedInputException e)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ParseErrorUnexpectedInput,
                    locationProvider.GetLocation(e.UnexpectedElement.Start, e.UnexpectedElement.End),
                    e.UnexpectedElement
                ));
                goto exit;
            }
            catch (LRParserRuntimeUnexpectedEndingException e)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ParseErrorUnexpectedEnding,
                    locationProvider.Fallback,
                    $"{string.Join(", ", (object?[])e.ExpectedInputs)} after the last parameter"
                ));
                goto exit;
            }
            catch (QuickMarkupTagMismatchException e)
            {
                var startTagName = e.FaultedTag.TagStart.TagName;
                var endTagName = e.FaultedTag.EndTagName;
                context.ReportDiagnostic(Diagnostic.Create(
                    TagCloseMismatchedError,
                    locationProvider.GetLocation(e.FaultedTag.TagStart.TagIdentifierAST),
                    startTagName,
                    endTagName
                ));
                context.ReportDiagnostic(Diagnostic.Create(
                    TagCloseMismatchedError,
                    locationProvider.GetLocation(e.FaultedTag.EndTagName),
                    startTagName,
                    endTagName
                ));
                goto exit;
            }
            foreach (var error in errors)
            {
                //var loc = locationProvider.GetLocation(error.Start, error.End);

                if (error.Value is LRParserRuntimeUnexpectedInputException unexpectedInput)
                    context.ReportDiagnostic(Diagnostic.Create(
                        ParseErrorUnexpectedInput,
                        locationProvider.GetLocation(unexpectedInput.UnexpectedElement.Start, unexpectedInput.UnexpectedElement.End),
                        unexpectedInput.UnexpectedElement
                    ));
                else if (error.Value is LRParserRuntimeUnexpectedEndingException unexpectedEnding)
                    context.ReportDiagnostic(Diagnostic.Create(
                        ParseErrorUnexpectedEnding,
                        locationProvider.GetLocation(error.Start, error.End),
                        $"{string.Join(", ", (object?[])unexpectedEnding.ExpectedInputs)} after the last parameter"
                    ));
            }
            var binder = new QuickMarkupBinder(
                new CodeTypeResolver(
                    context.Compilation,
                    qm.Usings,
                    markupStr.Target.Namespace
                ),
                failFast: false
            );
            if (qm.Template is not null)
            {
                try
                {
                    binder.Bind(qm.Template, typeSym);
                }
                catch (Exception e)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        BindErrorGeneral,
                        locationProvider.Fallback,
                        e.Message
                    ));
                }
            }
            try
            {
                _ = binder.BindRefDeclarations(qm.Refs, typeSym);
            }
            catch (Exception e)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    BindErrorGeneral,
                    locationProvider.Fallback,
                    e.Message
                ));
            }
            foreach (var error in binder.Diagnostics)
            {
                var loc = locationProvider.GetLocation(error.Node.Start, error.Node.End);
                if (error is QMBinderChildrenTooMany childrenTooMany)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        BindErrorChildrenTooMany,
                        loc,
                        childrenTooMany.ParentTagInfo.TagType as object ?? childrenTooMany.ParentTagInfo.TagName,
                        childrenTooMany.Expecting
                    ));
                }
                else
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        BindErrorGeneral,
                        loc,
                        error.ToString()
                    ));
                }
            }
exit:
            ;
        });
    }
}
