using System.Text;
using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.SourceGen.CodeGen;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;
using QuickMarkup.AST;

namespace QuickMarkup.SourceGen;

partial class QuickMarkupGenerator
{
    void InitializeQmuiPipeline(IncrementalGeneratorInitializationContext context)
    {
        var qmuiSource = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".qmui", StringComparison.OrdinalIgnoreCase))
            .Combine(context.CompilationProvider)
            .Select(static (pair, ct) =>
            {
                var (file, compilation) = pair;
                try
                {
                    var content = file.GetText(ct)!.ToString();
                    var sfc = QuickMarkupProviderExtension.Parse(content);
                    var ns = sfc.Namespace?.Name ?? "";
                    var name = sfc.ClassDeclaration?.Name ?? "";
                    var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
                    var target = new QuickMarkupTargetContext(
                        Namespace: ns,
                        TypeName: name,
                        FullTypeName: fullName,
                        FileName: file.Path,
                        AttributeLocation: default,
                        AttributeLineSpan: default
                    );
                    return (Target: target, Sfc: sfc, Compilation: compilation, Error: default(string));
                }
                catch (Exception e)
                {
                    var errTarget = new QuickMarkupTargetContext("", "", "", file.Path, default, default);
                    return (Target: errTarget, Sfc: default(QuickMarkupSFC), Compilation: compilation, Error: e.Message);
                }
            });

        var validQmui = qmuiSource.Where(static x => x.Error is null && !string.IsNullOrEmpty(x.Target.FullTypeName));

        // QMUI INIT (SETUP + MARKUP)
        {
            var initData = validQmui.Select(
                (x, ct) =>
                {
                    var tags = x.Sfc!.MarkupTags;
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
                    return (x.Target, x.Sfc, Markup: combined, x.Compilation);
                }
            );

            var initSources = initData.Select(
                (x, ct) => GenerateQmuiInit(x.Target, x.Sfc, x.Markup, x.Compilation, ct)
            );

            context.RegisterSourceOutput(initSources, (spc, value) =>
            {
                var (ctx, sfc, usings, code, error) = value;
                if (error is not null)
                {
                    code = $"""
                    /*
                        {error.Replace("*/", "*_/")}
                    */
                    {code}
                    """;
                }
                var classDecl = sfc?.ClassDeclaration;
                var baseTypes = classDecl is null ? "" : GetBaseTypesString(classDecl);
                var typeModifiers = classDecl?.Kind is ClassKind.Component or ClassKind.FragmentComponent
                    ? "sealed partial" : "partial";
                spc.AddSource(ctx, "INIT", code, usings, typeModifiers, baseTypes);
            });
        }

        // QMUI REFS
        {
            var refSources = validQmui.Select(
                (x, ct) => GenerateQmuiRefs(x.Target, x.Sfc, x.Compilation, ct)
            );

            context.RegisterSourceOutput(refSources, (spc, value) =>
            {
                var (ctx, sfc, usings, refsCode) = value;
                var classDecl = sfc?.ClassDeclaration;
                var baseTypes = classDecl is null ? "" : GetBaseTypesString(classDecl);
                var typeModifiers = classDecl?.Kind is ClassKind.Component or ClassKind.FragmentComponent
                    ? "sealed partial" : "partial";
                spc.AddSource(ctx, "REFS", refsCode, usings, typeModifiers, baseTypes);
            });
        }

        // QMUI ERRORS
        {
            var qmuiErrors = qmuiSource.Where(static x => x.Error is not null);

            context.RegisterSourceOutput(qmuiErrors, (spc, value) =>
            {
                var (target, _, _, error) = value;
                spc.AddSource(target, "INIT", $"""
                /*
                    {error.Replace("*/", "*_/")}
                */
                """);
            });
        }
    }

    static Compilation EnsureTypeSymbolInCompilation(QuickMarkupTargetContext target, QuickMarkupSFC sfc, Compilation compilation)
    {
        if (target.TryGetTypeSymbol(compilation, out _, out _))
            return compilation;

        var classDecl = sfc.ClassDeclaration;
        if (classDecl is null)
            return compilation;

        var baseClause = string.IsNullOrEmpty(classDecl.BaseTypes) ? "" : $" : {classDecl.BaseTypes}";
        var ns = string.IsNullOrEmpty(target.Namespace) ? "" : $"namespace {target.Namespace};";
        var source = $$"""
            #nullable enable
            {{sfc.Usings}}
            {{ns}}
            partial class {{target.TypeName}}{{baseClause}} { }
            """;
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
        return compilation.AddSyntaxTrees(tree);
    }

    static (QuickMarkupTargetContext Target, QuickMarkupSFC? Sfc, string Usings, string Code, string? Error)
        GenerateQmuiInit(QuickMarkupTargetContext target, QuickMarkupSFC sfc, QuickMarkupParsedTag? template,
            Compilation compilation, CancellationToken ct)
    {
        var usings = sfc.Usings;
        compilation = EnsureTypeSymbolInCompilation(target, sfc, compilation);

        if (!target.TryGetTypeSymbol(compilation, out var typeSymbol, out var failureReason))
        {
            var error = $"""
                Exception Occured during type resolving: {failureReason.GetType().FullName} {failureReason.Message}
                Messsage: {failureReason.Message}
                Stack Trace:
                    {failureReason.StackTrace.IndentWOF(1)}
                """;
            return (target, sfc, usings, "", error);
        }

        StringBuilder generatedProperties = new();
        StringBuilder codeBuilder = new();
        generatedProperties.AppendLine("global::System.Collections.Generic.List<global::System.IDisposable> QUICKMARKUP_DISPOSABLES { get; } = [];");
        var isConstructorMode = !typeSymbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared);
        var componentInfoResolver = new CodeTypeResolver(compilation, usings, target.Namespace);
        var componentKind = componentInfoResolver.GetComponentKind(typeSymbol, out var componentOutputType);
        var shouldGenerateComponentOutput = componentKind is not QMComponentKind.None && QuickMarkupGeneratedMemberTableBuilder.HasComponentRootOutput(template, componentKind);
        if (shouldGenerateComponentOutput)
        {
            if (CodeTypeResolver.FindRoslynProperty(typeSymbol, CodeTypeResolver.ComponentOutputPropertyName) is not null)
            {
                var error = $"Type {target.FullTypeName} already declares {CodeTypeResolver.ComponentOutputPropertyName}, but QuickMarkup needs to generate it from <root> children.";
                return (target, sfc, usings, "", error);
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
            return (target, sfc, usings, "", error);
        }

        string generatedMethod;
        if (isConstructorMode)
            generatedMethod = $$"""
            public {{typeSymbol.Name}}() {
                {{sfc.Scirpt?.RawScript ?? "// No raw scripts was provided"}}
                {{codeBuilder.ToString().IndentWOF()}}
            }
            """;
        else
            generatedMethod = $$"""
            private void Init() {
                {
                    foreach (global::System.IDisposable QUICKMARKUP_DISPOSABLE in QUICKMARKUP_DISPOSABLES) {
                        QUICKMARKUP_DISPOSABLE.Dispose();
                    }
                    QUICKMARKUP_DISPOSABLES.Clear();
                }
                {{sfc.Scirpt?.RawScript ?? "// No raw scripts was provided"}}
                {{codeBuilder.ToString().IndentWOF()}}
            }
            """;

        return (target, sfc, usings, $"""
                    {generatedProperties}
                    {generatedMethod}
                    """, error: default(string));
    }

    static (QuickMarkupTargetContext Target, QuickMarkupSFC? Sfc, string Usings, string Code)
        GenerateQmuiRefs(QuickMarkupTargetContext target, QuickMarkupSFC sfc, Compilation compilation, CancellationToken ct)
    {
        var usings = sfc.Usings;
        compilation = EnsureTypeSymbolInCompilation(target, sfc, compilation);

        if (!target.TryGetTypeSymbol(compilation, out var typeSymbol, out _))
        {
            return (target, sfc, usings, "");
        }

        var resolver = new CodeTypeResolver(compilation, usings, target.Namespace);
        var binder = new QuickMarkupBinder(resolver, failFast: true);
        var boundRefs = binder.BindRefDeclarations(sfc.Refs, typeSymbol);
        StringBuilder sb = new();
        var rgen = new RefsGenContext(sb, target.FullTypeName);
        rgen.CGenWrite(boundRefs, ct);

        return (target, sfc, usings, sb.ToString());
    }

    static string GetBaseTypesString(ClassDeclaration classDecl)
    {
        if (string.IsNullOrEmpty(classDecl.BaseTypes))
            return "";

        return classDecl.Kind switch
        {
            ClassKind.Component => $"global::QuickMarkup.Infra.IQuickMarkupComponent<{classDecl.BaseTypes}>",
            ClassKind.FragmentComponent => $"global::QuickMarkup.Infra.IQuickMarkupFragmentComponent<{classDecl.BaseTypes}>",
            _ => classDecl.BaseTypes
        };
    }
}
