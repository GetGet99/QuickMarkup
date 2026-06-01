using System.Collections.Immutable;
using Get.EasyCSharp.GeneratorTools;
using Get.Lexer;
using Get.Parser;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        TagCloseMismatchedError,
        BindErrorPropertyUnknown,
        BindErrorEnumMemberUnknown,
        BindErrorTypeUnknown,
        BindErrorTagMismatched,
        BindErrorTagUnexpected
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
    readonly static DiagnosticDescriptor BindErrorPropertyUnknown = new(
        "QM1006",
        "QuickMarkup unknown property",
        "{0} does not have a definition for '{1}', {2}",
        "QuickMarkup",
        DiagnosticSeverity.Warning,
        true
    );
    readonly static DiagnosticDescriptor BindErrorEnumMemberUnknown = new(
        "QM1007",
        "QuickMarkup unknown enum member",
        "{0} does not contain a definition for '{1}', {2}",
        "QuickMarkup",
        DiagnosticSeverity.Warning,
        true
    );
    readonly static DiagnosticDescriptor BindErrorTypeUnknown = new(
        "QM1008",
        "QuickMarkup unknown type",
        "Unknown type '{0}'",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    readonly static DiagnosticDescriptor BindErrorTagMismatched = new(
        "QM1009",
        "QuickMarkup mismatched ending tag",
        "Mismatched ending tag: <{0}>...</{1}>",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    readonly static DiagnosticDescriptor BindErrorTagUnexpected = new(
        "QM1010",
        "QuickMarkup unexpected tag",
        "Expecting <{0} />, but got <{1} />",
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
        context.RegisterSyntaxNodeAction(ctx =>
        {
            var syntaxNode = (TypeDeclarationSyntax)ctx.Node;
            if (syntaxNode.AttributeLists.Count is 0) return;

            var compilation = ctx.Compilation;
            if (ctx.SemanticModel.GetDeclaredSymbol(syntaxNode) is not ITypeSymbol typeSym)
                return;

            var quickMarkupAttrType = compilation.GetTypeByMetadataName(typeof(QuickMarkupAttribute).FullName!);
            if (quickMarkupAttrType is null) return;

            var attribute = (
                from x in typeSym.GetAttributes()
                where x.AttributeClass?.IsSubclassFrom(quickMarkupAttrType) ?? false
                select x
            ).FirstOrDefault();
            if (attribute is null) return;
            if (attribute.ConstructorArguments[0].Value is not string markup) return;

            var target = QuickMarkupTargetContext.FromSyntaxAndSymbol(
                typeSym, attribute.ApplicationSyntaxReference, ctx.CancellationToken);
            var locationProvider = new QuickMarkupSourceCodeLocationProvider(attribute, typeSym, ctx.CancellationToken);

            if (!target.TryGetTypeSymbol(compilation, out var resolvedTypeSym, out var failureReason))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
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
                qm = Parse(markup, out errors);
            }
            catch (LRParserRuntimeUnexpectedInputException e)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    ParseErrorUnexpectedInput,
                    locationProvider.GetLocation(e.UnexpectedElement.Start, e.UnexpectedElement.End),
                    e.UnexpectedElement
                ));
                return;
            }
            catch (LRParserRuntimeUnexpectedEndingException e)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    ParseErrorUnexpectedEnding,
                    locationProvider.Fallback,
                    $"{string.Join(", ", (object?[])e.ExpectedInputs)} after the last parameter"
                ));
                return;
            }
            catch (QuickMarkupTagMismatchException e)
            {
                var startTagName = e.FaultedTag.TagStart.TagName;
                var endTagName = e.FaultedTag.EndTagName;
                ctx.ReportDiagnostic(Diagnostic.Create(
                    TagCloseMismatchedError,
                    locationProvider.GetLocation(e.FaultedTag.TagStart.TagIdentifierAST),
                    startTagName,
                    endTagName
                ));
                ctx.ReportDiagnostic(Diagnostic.Create(
                    TagCloseMismatchedError,
                    locationProvider.GetLocation(e.FaultedTag.EndTagName),
                    startTagName,
                    endTagName
                ));
                return;
            }
            foreach (var error in errors)
            {
                if (error.Value is LRParserRuntimeUnexpectedInputException unexpectedInput)
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        ParseErrorUnexpectedInput,
                        locationProvider.GetLocation(unexpectedInput.UnexpectedElement.Start, unexpectedInput.UnexpectedElement.End),
                        unexpectedInput.UnexpectedElement
                    ));
                else if (error.Value is LRParserRuntimeUnexpectedEndingException unexpectedEnding)
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        ParseErrorUnexpectedEnding,
                        locationProvider.GetLocation(error.Start, error.End),
                        $"{string.Join(", ", (object?[])unexpectedEnding.ExpectedInputs)} after the last parameter"
                    ));
            }

            // Bind inline for immediate diagnostic feedback
            var resolver = new CodeTypeResolver(compilation, qm.Usings, target.Namespace);
            var binder = new QuickMarkupBinder(resolver, failFast: false);

            if (qm.Template is not null)
            {
                try
                {
                    binder.Bind(qm.Template, typeSym);
                }
                catch (Exception e)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
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
                ctx.ReportDiagnostic(Diagnostic.Create(
                    BindErrorGeneral,
                    locationProvider.Fallback,
                    e.Message
                ));
            }
            foreach (var diagnostic in binder.Diagnostics)
            {
                var loc = locationProvider.GetLocation(diagnostic.Node.Start, diagnostic.Node.End);
                if (diagnostic is QMBinderPropertyUnknownError propertyUnknown)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        BindErrorPropertyUnknown,
                        loc,
                        resolver.GetTypeSymbol(propertyUnknown.TypeName),
                        propertyUnknown.PropertyName,
                        QMDiagnosticSuggestion.FormatSuggestions(propertyUnknown.Suggestions)
                    ));
                }
                else if (diagnostic is QMBinderEnumMemberUnknownError enumMemberUnknown)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        BindErrorEnumMemberUnknown,
                        loc,
                        resolver.GetTypeSymbol(enumMemberUnknown.TypeName),
                        enumMemberUnknown.MemberName,
                        QMDiagnosticSuggestion.FormatSuggestions(enumMemberUnknown.Suggestions)
                    ));
                }
                else if (diagnostic is QMBinderChildrenTooMany childrenTooMany)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        BindErrorChildrenTooMany,
                        loc,
                        childrenTooMany.ParentTagInfo.TagType as object ?? childrenTooMany.ParentTagInfo.TagName,
                        childrenTooMany.Expecting
                    ));
                }
                else if (diagnostic is QMBinderTypeUnknownError typeUnknown)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        BindErrorTypeUnknown,
                        loc,
                        typeUnknown.TypeName
                    ));
                }
                else if (diagnostic is QMBinderTagMismatchedError tagMismatched)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        BindErrorTagMismatched,
                        loc,
                        tagMismatched.TagStart,
                        tagMismatched.TagEnd
                    ));
                }
                else if (diagnostic is QMBinderTagUnexpectedError tagUnexpected)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        BindErrorTagUnexpected,
                        loc,
                        tagUnexpected.ExpectedTag,
                        tagUnexpected.TagName
                    ));
                }
                else
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        BindErrorGeneral,
                        loc,
                        diagnostic.Message
                    ));
                }
            }
        }, SyntaxKind.ClassDeclaration);
    }
}
