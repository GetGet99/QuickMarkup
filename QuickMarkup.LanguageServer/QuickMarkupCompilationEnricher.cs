using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Helpers;

namespace QuickMarkup.LanguageServer;

/// <summary>
/// Helper for enriching compilations with QuickMarkup type symbols.
/// </summary>
internal static class QuickMarkupCompilationEnricher
{
    /// <summary>
    /// Ensures the type symbol for a QuickMarkup target exists in the compilation.
    /// If not found, creates and adds a dummy type declaration.
    /// </summary>
    public static Compilation EnsureTypeSymbolInCompilation(QuickMarkupTargetContext target, QuickMarkupSFC sfc, Compilation compilation)
    {
        if (target.TryGetTypeSymbol(compilation, out _, out _))
            return compilation;

        var classDecl = sfc.ClassDeclaration;
        if (classDecl is null)
            return compilation;

        var effectiveBaseTypes = classDecl.Kind switch
        {
            ClassKind.Component => $"global::QuickMarkup.Infra.IQuickMarkupComponent<{classDecl.BaseTypes}>",
            ClassKind.FragmentComponent => $"global::QuickMarkup.Infra.IQuickMarkupFragmentComponent<{classDecl.BaseTypes}>",
            _ => classDecl.BaseTypes ?? ""
        };
        var baseClause = string.IsNullOrEmpty(effectiveBaseTypes) ? "" : $" : {effectiveBaseTypes}";
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
}
