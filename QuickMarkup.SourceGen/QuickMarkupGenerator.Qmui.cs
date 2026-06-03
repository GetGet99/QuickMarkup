using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.CodeAnalysis;
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
                    var sfc = x.Sfc!;
                    var combined = CombineMarkupTags(sfc.MarkupTags);
                    var compilation = EnsureTypeSymbolInCompilation(x.Target, sfc, x.Compilation);
                    return (x.Target, Sfc: sfc, Markup: combined, Compilation: compilation);
                }
            );

            var initSources = initData.Select(
                (x, ct) =>
                {
                    var (_, usings, code, error, _) = GenerateInitSource(x.Target, x.Sfc.Usings, x.Markup, x.Sfc.Scirpt?.RawScript, x.Compilation, null, ct);
                    return (Ctx: x.Target, Sfc: x.Sfc, usings, code, error);
                }
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
                (x, ct) =>
                {
                    var compilation = EnsureTypeSymbolInCompilation(x.Target, x.Sfc!, x.Compilation);
                    var (code, _) = GenerateRefsSource(x.Target, x.Sfc!.Usings, x.Sfc!.Refs, compilation, null, ct);
                    return (x.Target, x.Sfc, x.Sfc!.Usings, code);
                }
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
