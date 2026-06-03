using System.Text;
using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.SourceGen.CodeGen;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;
using QuickMarkup.AST;

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
        var generatedMemberTable = nonErrorMarkups
            .Combine(context.CompilationProvider)
            .Select((x, ct) =>
            {
                var (markup, compilation) = x;
                return QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(markup, compilation, ct);
            })
            .Collect()
            .Select((items, _) => new QuickMarkupGeneratedMemberTable(items.Where(x => x is not null).Select(x => x!.Value)));
        
        // INIT (SETUP + MARKUP)
        {
            var sfcs = nonErrorMarkups.Select(
                (x, _) =>
                {
                    var tags = x.AST.MarkupTags;
                    QuickMarkupParsedTag? combined;
                    if (tags.Count == 0)
                        combined = null;
                    else if (tags.Count == 1)
                        combined = tags[0];
                    else
                        combined = new QuickMarkupParsedTag(
                            new QuickMarkupConstructor(new PositionedIdentifier("root")),
                            new ListAST<QuickMarkupInlineMember>(),
                            new ListAST<IQMNodeChild>(tags.Select(static t => (IQMNodeChild)t).ToList()),
                            null, true, null, false
                        );
                    return (x.Target, x.AST.Usings, x.AST.Scirpt, combined);
                }
            );

            var sources = sfcs.Combine(context.CompilationProvider).Combine(generatedMemberTable).Select(
                (x, ct) =>
                {
                    var (((target, usings, script, template), compilation), generatedMembers) = x;

                    if (!target.TryGetTypeSymbol(compilation, out var typeSymbol, out var failureReason))
                    {
                        var error = $"""
                            Exception Occured during type resolving: {failureReason.GetType().FullName} {failureReason.Message}
                            Messsage: {failureReason.Message}
                            Stack Trace:
                                {failureReason.StackTrace.IndentWOF(1)}
                            """;
                        return (target, usings, code: "", error, isComponent: false);
                    }
                    
                    StringBuilder generatedProperties = new();
                    StringBuilder codeBuilder = new();
                    generatedProperties.AppendLine("global::System.Collections.Generic.List<global::System.IDisposable> QUICKMARKUP_DISPOSABLES { get; } = [];");
                    var isConstructorMode = !typeSymbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared);
                    var componentInfoResolver = new CodeTypeResolver(compilation, usings, target.Namespace, generatedMembers, target.FullTypeName);
                    var componentKind = componentInfoResolver.GetComponentKind(typeSymbol, out var componentOutputType);
                    var shouldGenerateComponentOutput = componentKind is not QMComponentKind.None && QuickMarkupGeneratedMemberTableBuilder.HasComponentRootOutput(template, componentKind);
                    if (shouldGenerateComponentOutput)
                    {
                        if (CodeTypeResolver.FindRoslynProperty(typeSymbol, CodeTypeResolver.ComponentOutputPropertyName) is not null)
                        {
                            var error = $"Type {target.FullTypeName} already declares {CodeTypeResolver.ComponentOutputPropertyName}, but QuickMarkup needs to generate it from <root> children.";
                            return (target, usings, code: "", error, isComponent: componentKind is not QMComponentKind.None);
                        }

                        var outputType = componentKind is QMComponentKind.Fragment
                            ? $"global::QuickMarkup.Infra.FragmentBlock<{componentOutputType?.FullName() ?? "object"}>"
                            : componentOutputType?.FullName() ?? "object";
                        generatedProperties.AppendLine($"public {outputType} {CodeTypeResolver.ComponentOutputPropertyName} {{ get; private set; }} = null!;");
                    }
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (template is not null)
                        {
                            var analyzer = new QuickMarkupBinder(componentInfoResolver);
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
                        return (target, usings, code: "", error, isComponent: componentKind is not QMComponentKind.None);
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
                                // in case of re-initialize, cleanup all previous generated disposables
                                foreach (global::System.IDisposable QUICKMARKUP_DISPOSABLE in QUICKMARKUP_DISPOSABLES) {
                                    QUICKMARKUP_DISPOSABLE.Dispose();
                                }
                                QUICKMARKUP_DISPOSABLES.Clear();
                            }
                            {{script?.RawScript ?? "// No raw scripts was provided"}}
                            {{codeBuilder.ToString().IndentWOF()}}
                        }
                        """;
                    return (target, usings, code: $"""
                                {generatedProperties}
                                {generatedMethod}
                                """, error: default(string), isComponent: componentKind is not QMComponentKind.None);
                }
            );

            context.RegisterSourceOutput(sources, (sourceProductionContext, value) =>
            {
                var (ctx, usings, code, error, isComponent) = value;
                if (error is not null)
                {
                    code = $"""
                    /*
                        {error}
                    */
                    {code}
                    """;
                }
                var typeModifiers = isComponent ? "sealed partial" : "partial";
                sourceProductionContext.AddSource(ctx, "INIT", code, usings, typeModifiers);
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

            var withCompilation = refs.Combine(context.CompilationProvider).Combine(generatedMemberTable);

            var lines = withCompilation.Select((x, tok) =>
            {
                var (((target, usings, refs), compilation), generatedMembers) = x;
                var resolver = new CodeTypeResolver(compilation, usings, target.Namespace, generatedMembers, target.FullTypeName);
                var containingType = TryResolveTypeMetadataName(compilation, target.FullTypeName);
                var binder = new QuickMarkupBinder(resolver, failFast: true);
                var boundRefs = binder.BindRefDeclarations(refs, containingType);
                StringBuilder sb = new();
                var rgen = new RefsGenContext(sb, target.FullTypeName);
                rgen.CGenWrite(boundRefs, tok);
                var isComponent = resolver.GetComponentKind(containingType, out _) is not QMComponentKind.None;
                return (target, usings, sb.ToString(), isComponent);
            });

            context.RegisterSourceOutput(lines, (sourceProductionContext, value) =>
            {
                var (ctx, usings, refsCode, isComponent) = value;
                var typeModifiers = isComponent ? "sealed partial" : "partial";
                sourceProductionContext.AddSource(ctx, "REFS", refsCode, usings, typeModifiers);
            });
        }

        // ERRORS
        {
            context.RegisterSourceOutput(errorMarkups, (sourceProductionContext, value) =>
            {
                var (target, errors) = value;
                sourceProductionContext.AddSource(target, "ERROR", $"""
                /*
                    {errors.Replace("*/", "*_/")}
                */
                """);
            });
        }

        // .QMUI ADDITIONALFILES PIPELINE
        InitializeQmuiPipeline(context);
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
