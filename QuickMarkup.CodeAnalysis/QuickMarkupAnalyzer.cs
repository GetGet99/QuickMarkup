using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis;

public static class QuickMarkupAnalyzer
{
    public static QuickMarkupFileAnalysis Analyze(
        string qmuiContent,
        string filePath,
        string @namespace,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable generatedMemberTable,
        bool failFast = false)
    {
        var (sfc, _) = QuickMarkupProviderExtension.ParseWithErrors(qmuiContent);
        if (sfc is null)
        {
            var emptyCtx = CreateTargetContext(filePath, @namespace, "");
            return new QuickMarkupFileAnalysis(null!, emptyCtx, [], null, [], null, false);
        }
        return Analyze(sfc, filePath, @namespace, compilation, generatedMemberTable, failFast);
    }

    public static QuickMarkupFileAnalysis Analyze(
        QuickMarkupSFC sfc,
        string filePath,
        string @namespace,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable generatedMemberTable,
        bool failFast = false)
    {
        var typeName = sfc.ClassDeclaration?.Name ?? "";
        var target = CreateTargetContext(filePath, @namespace, typeName);

        var resolver = new CodeTypeResolver(compilation, sfc.Usings, @namespace, generatedMemberTable);
        var containingType = TryGetContainingType(compilation, target.FullTypeName);
        var binder = new QuickMarkupBinder(resolver, failFast);

        var isComponent = false;
        if (containingType is not null)
            isComponent = resolver.GetComponentKind(containingType, out _) is not QMComponentKind.None;

        IReadOnlyList<QMRefDeclarationSymbol<ITypeSymbol?>> refDeclarations = [];
        try { refDeclarations = binder.BindRefDeclarations(sfc.Refs, containingType); }
        catch { }

        QMNodeSymbol<ITypeSymbol?>? boundTemplate = null;
        if (sfc.Template is not null && containingType is not null)
        {
            try { boundTemplate = binder.Bind(sfc.Template, containingType); }
            catch { }
        }

        QuickMarkupGeneratedTypeMembers? generatedMembers = null;
        try
        {
            generatedMembers = QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(
                new QuickMarkupParsedAttribute(target, sfc), compilation, CancellationToken.None);
        }
        catch { }

        return new QuickMarkupFileAnalysis(
            sfc, target, refDeclarations, boundTemplate,
            binder.Diagnostics, generatedMembers, isComponent);
    }

    static INamedTypeSymbol? TryGetContainingType(Compilation compilation, string fullTypeName)
    {
        return !string.IsNullOrEmpty(fullTypeName)
            ? compilation.GetTypeByMetadataName(fullTypeName)
            : null;
    }

    static QuickMarkupTargetContext CreateTargetContext(string filePath, string ns, string typeName)
    {
        var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
        return new QuickMarkupTargetContext(ns, typeName, fullName, filePath, default, default);
    }
}
