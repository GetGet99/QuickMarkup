using System.Collections.Immutable;
using Get.EasyCSharp.GeneratorTools;
using Get.Parser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using static QuickMarkup.SourceGen.QuickMarkupDiagnosticReporter;

namespace QuickMarkup.SourceGen;

[AddAttributeConverter(typeof(QuickMarkupAttribute), ParametersAsString = "\"\"")]
[DiagnosticAnalyzer(LanguageNames.CSharp)]
partial class QuickMarkupAnalyzer : DiagnosticAnalyzer
{
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
        BindErrorTagUnexpected,
        BindErrorTypeMismatch,
        BindErrorRequiredPropertyMissing
    );
    internal readonly static DiagnosticDescriptor ParseErrorUnexpectedInput = new(
        "QM1001",
        "QuickMarkup parse error due to unexpected token",
        "Unexpected {0}",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    internal readonly static DiagnosticDescriptor ParseErrorUnexpectedEnding = new(
        "QM1002",
        "QuickMarkup parse error due to unexpected ending",
        "Expect {0} after the last parameter",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
#pragma warning disable RS1037 // Add "CompilationEnd" custom tag to compilation end diagnostic descriptor
    internal readonly static DiagnosticDescriptor BindErrorGeneral = new(
#pragma warning restore RS1037 // Add "CompilationEnd" custom tag to compilation end diagnostic descriptor
        "QM1003",
        "QuickMarkup general typing error",
        "{0}",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    internal readonly static DiagnosticDescriptor BindErrorChildrenTooMany = new(
        "QM1004",
        "QuickMarkup typing error too many children",
        "Too many children were provided, <{0}> expects {1}",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    internal readonly static DiagnosticDescriptor TagCloseMismatchedError = new(
        "QM1005",
        "QuickMarkup close tag mismatched",
        "Tag open and close mismatched: <{0}>...</{1}>",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    internal readonly static DiagnosticDescriptor BindErrorPropertyUnknown = new(
        "QM1006",
        "QuickMarkup unknown property",
        "{0} does not have a definition for '{1}', {2}",
        "QuickMarkup",
        DiagnosticSeverity.Warning,
        true
    );
    internal readonly static DiagnosticDescriptor BindErrorEnumMemberUnknown = new(
        "QM1007",
        "QuickMarkup unknown enum member",
        "{0} does not contain a definition for '{1}', {2}",
        "QuickMarkup",
        DiagnosticSeverity.Warning,
        true
    );
    internal readonly static DiagnosticDescriptor BindErrorTypeUnknown = new(
        "QM1008",
        "QuickMarkup unknown type",
        "Unknown type '{0}'",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    internal readonly static DiagnosticDescriptor BindErrorTagMismatched = new(
        "QM1009",
        "QuickMarkup mismatched ending tag",
        "Mismatched ending tag: <{0}>...</{1}>",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    internal readonly static DiagnosticDescriptor BindErrorTagUnexpected = new(
        "QM1010",
        "QuickMarkup unexpected tag",
        "Expecting <{0} />, but got <{1} />",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    internal readonly static DiagnosticDescriptor BindErrorTypeMismatch = new(
        "QM1011",
        "QuickMarkup type mismatch",
        "Cannot assign value of type '{0}' to property of type '{1}'",
        "QuickMarkup",
        DiagnosticSeverity.Error,
        true
    );
    internal readonly static DiagnosticDescriptor BindErrorRequiredPropertyMissing = new(
        "QM1012",
        "QuickMarkup required property missing",
        "Required property '{0}' is not set on '{1}'",
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
                (qm, errors) = QuickMarkupProviderExtension.ParseWithErrorsCore(markup);
            }
            catch (Exception e) when (TryHandleParseException(e, locationProvider, d => ctx.ReportDiagnostic(d)))
            {
                return;
            }
            ReportErrorTerminals(errors, locationProvider, d => ctx.ReportDiagnostic(d));

            // Bind inline for immediate diagnostic feedback
            var resolver = new CodeTypeResolver(compilation, qm.Usings, target.Namespace);
            var binder = new QuickMarkupBinder(resolver, Binder.Collect);

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
            ReportBinderDiagnostics(binder.Diagnostics, locationProvider, resolver, d => ctx.ReportDiagnostic(d));
        }, SyntaxKind.ClassDeclaration);

        InitializeQmuiAnalysis(context);
    }
}
