using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis;

public static class QuickMarkupFileAnalyzer
{
    public static QuickMarkupFileAnalysis Analyze(
        string qmuiContent,
        string filePath,
        string @namespace,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable generatedMemberTable,
        FrameworkConfiguration? frameworkConfiguration = null,
        bool failFast = false)
    {
        var (sfc, _) = QuickMarkupProviderExtension.ParseWithErrors(qmuiContent);
        if (sfc is null)
        {
            var emptyCtx = CreateTargetContext(filePath, @namespace, "");
            return new QuickMarkupFileAnalysis(null!, emptyCtx, [], null, [], null, false);
        }
        return Analyze(sfc, filePath, @namespace, compilation, generatedMemberTable, frameworkConfiguration, failFast);
    }

    public static QuickMarkupFileAnalysis Analyze(
        QuickMarkupSFC sfc,
        string filePath,
        string @namespace,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable generatedMemberTable,
        FrameworkConfiguration? frameworkConfiguration = null,
        bool failFast = false,
        bool hasQuickMarkup = true)
    {
        var typeName = sfc.ClassDeclaration?.Name ?? "";
        var target = CreateTargetContext(filePath, @namespace, typeName);

        var resolver = new CodeTypeResolver(compilation, sfc.Usings, @namespace, generatedMemberTable, frameworkConfiguration: frameworkConfiguration);
        var containingType = TryGetContainingType(compilation, target.FullTypeName);
        var binder = new QuickMarkupBinder(resolver, failFast ? Binder.FailFast : Binder.Collect);

        var isComponent = false;
        if (containingType is not null)
            isComponent = resolver.GetComponentKind(containingType, out _) is not QMComponentKind.None;

        IReadOnlyList<QMRefDeclarationSymbol<ITypeSymbol?>> refDeclarations = [];
        try { refDeclarations = binder.BindRefDeclarations(sfc.Refs, containingType, hasQuickMarkup); }
        catch (Exception ex) { Console.Error.WriteLine($"[QuickMarkup] Ref binding failed for {target.FullTypeName}: {ex.Message}"); }

        QMNodeSymbol<ITypeSymbol?>? boundTemplate = null;
        if (sfc.Template is not null && containingType is not null)
        {
            try { boundTemplate = binder.Bind(sfc.Template, containingType); }
            catch (Exception ex) { Console.Error.WriteLine($"[QuickMarkup] Template binding failed for {target.FullTypeName}: {ex.Message}"); }
        }

        QuickMarkupGeneratedTypeMembers? generatedMembers = null;
        try
        {
            generatedMembers = QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(
                new QuickMarkupParsedAttribute(target, sfc), compilation, CancellationToken.None);
        }
        catch (Exception ex) { Console.Error.WriteLine($"[QuickMarkup] Member table building failed for {target.FullTypeName}: {ex.Message}"); }

        return new QuickMarkupFileAnalysis(
            sfc, target, refDeclarations, boundTemplate,
            binder.Diagnostics, generatedMembers, isComponent);
    }

    public static QuickMarkupFileAnalysis AnalyzeMerged(
        QuickMarkupMergedType merged,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable generatedMemberTable,
        FrameworkConfiguration? frameworkConfiguration = null,
        bool failFast = false)
    {
        foreach (var refsSource in merged.RefsSources)
        {
            if (refsSource.MarkupTags.Count > 0)
                throw new InvalidOperationException("[QuickRefs] only allows reference declarations, but markup tags were found");
            if (refsSource.Scirpt is not null)
                throw new InvalidOperationException("[QuickRefs] only allows reference declarations, but <setup> script was found");
        }
        var mergedSfc = new QuickMarkupSFC(merged.MergedUsings, new ListAST<RefDeclaration>(merged.MergedRefs()))
        {
            Scirpt = merged.MarkupSource?.Scirpt,
            MarkupTags = merged.MarkupSource?.MarkupTags ?? new(),
        };
        var target = merged.Target;
        var resolver = new CodeTypeResolver(compilation, merged.MergedUsings, target.Namespace, generatedMemberTable, frameworkConfiguration: frameworkConfiguration);
        var containingType = TryGetContainingType(compilation, target.FullTypeName);
        var binder = new QuickMarkupBinder(resolver, failFast ? Binder.FailFast : Binder.Collect);

        var isComponent = false;
        if (containingType is not null)
            isComponent = resolver.GetComponentKind(containingType, out _) is not QMComponentKind.None;

        IReadOnlyList<QMRefDeclarationSymbol<ITypeSymbol?>> refDeclarations = [];
        try { refDeclarations = binder.BindRefDeclarations(mergedSfc.Refs, containingType, merged.HasQuickMarkup); }
        catch (Exception ex) { Console.Error.WriteLine($"[QuickMarkup] Ref binding failed for {target.FullTypeName}: {ex.Message}"); }

        return new QuickMarkupFileAnalysis(
            mergedSfc, target, refDeclarations, null,
            binder.Diagnostics, null, isComponent);
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
