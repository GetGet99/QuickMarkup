using System.Text;
using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Attributes;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Get.Lexer;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.AST;
using QuickMarkup.Parser;
using QuickMarkup.SourceGen.Analyzers;
using QuickMarkup.SourceGen.CodeGen;

namespace QuickMarkup.SourceGen;

[AddAttributeConverter(typeof(QuickMarkupAttribute), ParametersAsString = "\"\"")]
[Generator]
partial class QuickMarkupGenerator : IIncrementalGenerator
{
    static readonly DiagnosticDescriptor compileError = new(
        "QMC001",
        "Compilation error in generated code",
        "One more errors occured on the generated source file\n{0}",
        "QuickMarkupSourceCompiler",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    static readonly DiagnosticDescriptor compileWarning = new(
        "QMC002",
        "Compilation warning in generated code",
        "One more warnings occured on the generated source file\n{0}",
        "QuickMarkupSourceCompiler",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
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
    QuickMarkupSFC Parse(IEnumerable<IToken<QuickMarkupLexer.Tokens>> tokens)
    {
        return ParserPerThread.Value.Parse(tokens, out _);
    }
    QuickMarkupSFC Parse(string code)
    {
        return Parse(Lex(code));
    }

    static readonly string FullAttributeName;
    static QuickMarkupGenerator()
    {
        FullAttributeName = typeof(QuickMarkupAttribute).FullName;
    }
    static readonly SymbolDisplayFormat withoutNamespace = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );
    protected void OnInitialize(IncrementalGeneratorPostInitializationContext context) { }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(OnInitialize);
        var markupStrings = context.SyntaxProvider.ForAttributeWithMetadataName(
            FullAttributeName,
            static (syntaxNode, cancelationToken)
                => syntaxNode is TypeDeclarationSyntax,
            static (ctx, ct) =>
            {
                var type = (ITypeSymbol)ctx.TargetSymbol;
                var name = type.ToDisplayString(withoutNamespace);
                var syn = ctx.Attributes[0].ApplicationSyntaxReference;
                var syntaxTree = syn?.SyntaxTree ?? type.Locations[0].SourceTree;
                var attributeLocation = syn is null ? type.Locations[0].SourceSpan : syn.Span;
                if (syn?.GetSyntax(ct) is AttributeSyntax attrSyntax)
                {
                    // move fallback to just the attribute name
                    attributeLocation = attrSyntax.Name.Span;
                }
                var linespan = syn?.SyntaxTree.GetLineSpan(attributeLocation, ct) ?? default;

                return (ctx: new SourceGenContext(
                    type.ContainingNamespace.ToString(),
                    name,
                    syntaxTree?.FilePath,
                    attributeLocation,
                    new(linespan.StartLinePosition, linespan.EndLinePosition)
                ), type: new FullType(type).TypeWithNamespace, markup: ctx.Attributes[0].ConstructorArguments[0].Value as string);
            }
        );
        var markups = markupStrings.Where(static ctx => ctx.markup is not null).Select(
            (x, _) =>
            {
                QuickMarkupSFC? markup = null;
                string? error = null;
                try
                {
                    markup = Parse(x.markup!);
                }
                catch (Exception e)
                {
                    error = $"""
                        Exception Occured during Parsing: {e.GetType().FullName} {e.Message}
                        Messsage: {e.Message}
                        Stack Trace:
                            {e.StackTrace.IndentWOF(1)}
                        """;
                }
                return (x.ctx, x.type, markup, error);
            }
        );

        var nonErrorMarkups = markups.Where(static ctx => ctx.markup is not null).Select(
            (x, _) =>
            {
                return (x.ctx, x.type, usings: x.markup!.Usings, markup: x.markup!);
            }
        );

        // INIT (SETUP + MARKUP)
        {
            var sfcs = nonErrorMarkups.Select(
                (x, _) =>
                {
                    return (x.ctx, x.type, x.usings, x.markup.Scirpt, x.markup.Template);
                }
            );

            var sources = sfcs.Combine(context.CompilationProvider).Select(
                (x, ct) =>
                {
                    var ((ctx, type, usings, script, template), compilation) = x;
                    StringBuilder generatedProperties = new();
                    StringBuilder codeBuilder = new();
                    generatedProperties.AppendLine("global::System.Collections.Generic.List<global::QuickMarkup.Infra.RefEffect> QUICKMARKUP_EFFECTS { get; } = [];");
                    INamedTypeSymbol? typeSymbol;
                    try
                    {
                        string searchTypeName;
                        if (type.StartsWith("global::"))
                        {
                            searchTypeName = type["global::".Length..];
                        }
                        else
                        {
                            searchTypeName = type;
                        }
                        var idx = searchTypeName.IndexOf('<');
                        if (idx >= 0)
                        {
                            searchTypeName = searchTypeName[..idx];
                        }
                        typeSymbol = compilation.GetTypeByMetadataName(searchTypeName);
                        if (typeSymbol is null)
                            return (ctx, usings, code: "", error: $"Error: compilation.GetTypeByMetadataName(\"{searchTypeName}\") returns null");
                    }
                    catch (Exception e)
                    {
                        var error = $"""
                            Exception Occured during type resolving: {e.GetType().FullName} {e.Message}
                            Messsage: {e.Message}
                            Stack Trace:
                                {e.StackTrace.IndentWOF(1)}
                            """;
                        return (ctx, usings, code: "", error);
                    }
                    var isConstructorMode = !typeSymbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared);
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (template is not null)
                        {
                            var resolver = new CodeGenTypeResolver(compilation, usings, ctx.Namespace);
                            var analyzer = new QMSourceGenBinders(resolver);
                            var output = analyzer.Bind(template, typeSymbol);
                            ct.ThrowIfCancellationRequested();
                            var cgen = new CodeGenContext(
                                resolver,
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
                        return (ctx, usings, code: "", error);
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
                    return (ctx, usings, code: $"""
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
                sourceProductionContext.AddSource($"{ctx.TypeNameWithoutNamespace.Replace('<', '[').Replace('>', ']')}.INIT.g.cs", $$"""
                #nullable enable
                {{usings}}

                namespace {{ctx.Namespace}};
                
                partial class {{ctx.TypeNameWithoutNamespace}} {
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
                    return (x.ctx, x.type, x.usings, x.markup.Refs);
                }
            );

            var withCompilation = refs.Combine(context.CompilationProvider);

            var lines = withCompilation.Select((x, tok) =>
            {
                var ((ctx, type, usings, refs), compilation) = x;
                var resolver = new CodeGenTypeResolver(compilation, usings, ctx.Namespace);
                var containingType = TryResolveTypeMetadataName(compilation, type);
                var binder = new QMSourceGenBinders(resolver, failFast: true);
                var boundRefs = binder.BindRefDeclarations(refs, containingType);
                StringBuilder sb = new();
                var rgen = new RefsGenContext(sb, type);
                rgen.CGenWrite(boundRefs, tok);
                return (ctx, usings, sb.ToString());
            });

            context.RegisterSourceOutput(lines, (sourceProductionContext, value) =>
            {
                var (ctx, usings, refsCode) = value;
                sourceProductionContext.AddSource($"{ctx.TypeNameWithoutNamespace.Replace('<', '[').Replace('>', ']')}.REFS.g.cs", $$"""
                #nullable enable
                {{usings}}

                namespace {{ctx.Namespace}};
                
                partial class {{ctx.TypeNameWithoutNamespace}} {
                    {{refsCode}}
                }
                
                """);
            });
        }

        // ERRORS
        {
            var errors = markups.Where(static ctx => ctx.error is not null).Select(
                (x, _) =>
                {
                    return (x.ctx, x.error!);
                }
            );

            context.RegisterSourceOutput(errors, (sourceProductionContext, value) =>
            {
                var (ctx, errors) = value;
                sourceProductionContext.AddSource($"{ctx.TypeNameWithoutNamespace.Replace('<', '[').Replace('>', ']')}.ERROR.g.cs", $$"""
                #nullable enable
                namespace {{ctx.Namespace}};
                
                partial class {{ctx.TypeNameWithoutNamespace}} {
                    /*
                        {{errors.Replace("*/", "*_/")}}
                    */
                }
                
                """);
            });
        }
    }
    readonly record struct SourceGenContext(
        string Namespace,
        string TypeNameWithoutNamespace,
        string? FileName,
        TextSpan AttributeLocation,
        LinePositionSpan AttributeLineSpan
    );
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
