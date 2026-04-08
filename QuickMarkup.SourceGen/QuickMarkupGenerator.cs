using System.Text;
using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.SourceGen.CodeGen;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;

namespace QuickMarkup.SourceGen;

[Generator]
partial class QuickMarkupGenerator : IIncrementalGenerator
{
    //static readonly DiagnosticDescriptor compileError = new(
    //    "QMC001",
    //    "Compilation error in generated code",
    //    "One more errors occured on the generated source file\n{0}",
    //    "QuickMarkupSourceCompiler",
    //    DiagnosticSeverity.Error,
    //    isEnabledByDefault: true
    //);
    //static readonly DiagnosticDescriptor compileWarning = new(
    //    "QMC002",
    //    "Compilation warning in generated code",
    //    "One more warnings occured on the generated source file\n{0}",
    //    "QuickMarkupSourceCompiler",
    //    DiagnosticSeverity.Warning,
    //    isEnabledByDefault: true
    //);
    protected void OnInitialize(IncrementalGeneratorPostInitializationContext context) { }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(OnInitialize);
        var (nonErrorMarkups, errorMarkups) = context.SyntaxProvider.ForAllParsedQuickMarkup();
        
        // INIT (SETUP + MARKUP)
        {
            var sfcs = nonErrorMarkups.Select(
                (x, _) =>
                {
                    return (x.Target, x.AST.Usings, x.AST.Scirpt, x.AST.Template);
                }
            );

            var sources = sfcs.Combine(context.CompilationProvider).Select(
                (x, ct) =>
                {
                    var ((target, usings, script, template), compilation) = x;

                    if (!target.TryGetTypeSymbol(compilation, out var typeSymbol, out var failureReason))
                    {
                        var error = $"""
                            Exception Occured during type resolving: {failureReason.GetType().FullName} {failureReason.Message}
                            Messsage: {failureReason.Message}
                            Stack Trace:
                                {failureReason.StackTrace.IndentWOF(1)}
                            """;
                        return (target, usings, code: "", error);
                    }
                    
                    StringBuilder generatedProperties = new();
                    StringBuilder codeBuilder = new();
                    generatedProperties.AppendLine("global::System.Collections.Generic.List<global::QuickMarkup.Infra.RefEffect> QUICKMARKUP_EFFECTS { get; } = [];");
                    var isConstructorMode = !typeSymbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared);
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (template is not null)
                        {
                            var resolver = new CodeTypeResolver(compilation, usings, target.Namespace);
                            var analyzer = new Binder(resolver);
                            var output = analyzer.Bind(template, typeSymbol);
                            ct.ThrowIfCancellationRequested();
                            var cgen = new CodeGenContext(
                                generatedProperties,
                                codeBuilder,
                                isConstructorMode
                            );
                            cgen.CGenWrite(output, "this");
                            ct.ThrowIfCancellationRequested();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        var error = $"""
                            Exception Occured during Bindings or Codegen: {e.GetType().FullName} {e.Message}
                            Messsage: {e.Message}
                            Stack Trace:
                                {e.StackTrace.IndentWOF(1)}
                            """;
                        return (target, usings, code: "", error);
                    }
                    string generatedMethod;
                    if (isConstructorMode)
                        generatedMethod = $$"""
                        public {{typeSymbol.Name}}() {
                            {{script?.RawScript ?? "// No raw scripts was provided"}}
                            {{codeBuilder.ToString().IndentWOF()}}
                        }
                        """;
                    else
                        generatedMethod = $$"""
                        private void Init() {
                            {
                                // in case of re-initialize, cleanup all previous effects
                                foreach (global::QuickMarkup.Infra.RefEffect QUICKMARKUP_EFFECT in QUICKMARKUP_EFFECTS) {
                                    QUICKMARKUP_EFFECT.Dispose();
                                }
                                QUICKMARKUP_EFFECTS.Clear();
                            }
                            {{script?.RawScript ?? "// No raw scripts was provided"}}
                            {{codeBuilder.ToString().IndentWOF()}}
                        }
                        """;
                    return (target, usings, code: $"""
                                {generatedProperties}
                                {generatedMethod}
                                """, error: default(string));
                }
            );

            context.RegisterSourceOutput(sources, (sourceProductionContext, value) =>
            {
                var (ctx, usings, code, error) = value;
                if (error is not null)
                {
                    code = $"""
                    /*
                        {error}
                    */
                    {code}
                    """;
                }
                sourceProductionContext.AddSource($"{ctx.FullTypeName.Replace('<', '[').Replace('>', ']')}.INIT.g.cs", $$"""
                #nullable enable
                {{usings}}

                namespace {{ctx.Namespace}};
                
                partial class {{ctx.TypeName}} {
                    {{code}}
                }
                
                """);
            });
            /*
            // ERRORS from source file:
            var compilationErrors = sources.Combine(context.CompilationProvider).Select((value, ct) =>
            {
                var ((ctx, usings, code, _), compilation) = value;
                var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
                var tree = CSharpSyntaxTree.ParseText($$"""
                #nullable enable
                {{usings}}

                namespace {{ctx.Namespace}};
                
                partial class {{ctx.TypeNameWithoutNamespace}} {
                    {{code}}
                }
                
                """, parseOptions);
                var newCompilation = compilation.AddSyntaxTrees(tree);
                var model = newCompilation.GetSemanticModel(tree);
                var diagnostics = model.GetDiagnostics(cancellationToken: ct);
                string error = "";
                string warning = "";
                foreach (var diagnostic in diagnostics)
                {
                    if (diagnostic.IsSuppressed) continue;
                    if (diagnostic.Severity is not (DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                        continue;
                    var source = GetExpandedLineText(diagnostic.Location)?.Trim();
                    if (diagnostic.Severity is DiagnosticSeverity.Error)
                    {
                        if (source is not null)
                            error += $"\n{source}";
                        error += $"\nError {diagnostic.Id}: {diagnostic.GetMessage()}";
                    }
                    if (!diagnostic.IsSuppressed && diagnostic.Severity is DiagnosticSeverity.Warning)
                    {
                        if (source is not null)
                            warning += $"\n{source}";
                        warning += $"\nWarning {diagnostic.Id}: {diagnostic.GetMessage()}";
                    }
                }
                return (ctx, error, warning);
            });

            context.RegisterSourceOutput(compilationErrors, (sourceProductionContext, value) =>
            {
                var (ctx, error, warning) = value;

                if (ctx.FileName is null) return;

                if (!string.IsNullOrWhiteSpace(error))
                    sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                        compileError,
                        Location.Create(ctx.FileName, ctx.AttributeLocation, ctx.AttributeLineSpan),
                        error
                    ));
                if (!string.IsNullOrWhiteSpace(warning))
                    sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                        compileWarning,
                        Location.Create(ctx.FileName, ctx.AttributeLocation, ctx.AttributeLineSpan),
                        warning
                    ));
            });
            */
        }

        // REFS
        {
            var refs = nonErrorMarkups.Select(
                (x, _) =>
                {
                    return (x.Target, x.AST.Usings, x.AST.Refs);
                }
            );

            var withCompilation = refs.Combine(context.CompilationProvider);

            var lines = withCompilation.Select((x, tok) =>
            {
                var ((target, usings, refs), compilation) = x;
                var resolver = new CodeTypeResolver(compilation, usings, target.Namespace);
                var containingType = TryResolveTypeMetadataName(compilation, target.FullTypeName);
                var binder = new Binder(resolver, failFast: true);
                var boundRefs = binder.BindRefDeclarations(refs, containingType);
                StringBuilder sb = new();
                var rgen = new RefsGenContext(sb, target.FullTypeName);
                rgen.CGenWrite(boundRefs, tok);
                return (target, usings, sb.ToString());
            });

            context.RegisterSourceOutput(lines, (sourceProductionContext, value) =>
            {
                var (ctx, usings, refsCode) = value;
                sourceProductionContext.AddSource($"{ctx.FullTypeName.Replace('<', '[').Replace('>', ']')}.REFS.g.cs", $$"""
                #nullable enable
                {{usings}}

                namespace {{ctx.Namespace}};
                
                partial class {{ctx.TypeName}} {
                    {{refsCode}}
                }
                
                """);
            });
        }

        // ERRORS
        {
            context.RegisterSourceOutput(errorMarkups, (sourceProductionContext, value) =>
            {
                var (target, errors) = value;
                sourceProductionContext.AddSource($"{target.FullTypeName.Replace('<', '[').Replace('>', ']')}.ERROR.g.cs", $$"""
                #nullable enable
                namespace {{target.Namespace}};
                
                partial class {{target.TypeName}} {
                    /*
                        {{errors.Replace("*/", "*_/")}}
                    */
                }
                
                """);
            });
        }
    }
    public static string? GetExpandedLineText(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        if (!location.IsInSource)
            return null; // or throw, depending on your use case

        var sourceTree = location.SourceTree;
        var sourceText = sourceTree.GetText();

        var span = location.SourceSpan;

        // Get line numbers
        var startLine = sourceText.Lines.GetLineFromPosition(span.Start);
        var endLine = sourceText.Lines.GetLineFromPosition(span.End);

        // Expand to full lines (including line breaks)
        var expandedStart = startLine.Start;
        var expandedEnd = endLine.EndIncludingLineBreak;

        var expandedSpan = TextSpan.FromBounds(expandedStart, expandedEnd);

        return sourceText.ToString(expandedSpan);
    }

    static INamedTypeSymbol? TryResolveTypeMetadataName(Compilation compilation, string typeDisplayString)
    {
        var searchTypeName = typeDisplayString.StartsWith("global::", StringComparison.Ordinal)
            ? typeDisplayString["global::".Length..]
            : typeDisplayString;
        var idx = searchTypeName.IndexOf('<');
        if (idx >= 0)
            searchTypeName = searchTypeName[..idx];
        return compilation.GetTypeByMetadataName(searchTypeName);
    }
}
