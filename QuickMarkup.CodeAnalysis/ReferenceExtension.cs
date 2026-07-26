using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis;

public static class ReferenceExtension
{
    extension<T>(QMRefDeclarationSymbol<T> refSym)
    {
        public string BackingSuffix => refSym.Kind switch
        {
            RefDeclarationKind.Computed => "Comp",
            RefDeclarationKind.AsyncComputed => "Async",
            RefDeclarationKind.Ref or RefDeclarationKind.Provide or RefDeclarationKind.Inject or RefDeclarationKind.InjectOptional =>
                "Prop",
            _ => throw new NotImplementedException()
        };
        public string BackingName => $"{refSym.Name}{refSym.BackingSuffix}";
    }
    extension(QMRefDeclarationSymbol<ITypeSymbol?> refSym)
    {
        public string? TypeName => RefTypeDisplayName(refSym.RefType, refSym.Name);
        public string BackingTypeName => refSym.Kind switch
        {
            RefDeclarationKind.Computed => $"global::QuickMarkup.Infra.Computed<{refSym.TypeName}>",
            RefDeclarationKind.AsyncComputed => $"global::QuickMarkup.Infra.AsyncComputed<{refSym.TypeName}>",
            RefDeclarationKind.Ref or RefDeclarationKind.Provide or RefDeclarationKind.Inject =>
                $"global::QuickMarkup.Infra.Reference<{refSym.TypeName}>",
            RefDeclarationKind.InjectOptional =>
                $"global::QuickMarkup.Infra.Reference<{refSym.TypeName}>?",
            _ => throw new NotImplementedException()
        };
        
    }
    public static string RefTypeDisplayName(ITypeSymbol? type, string fallbackName)
    {
        if (type is null)
            return fallbackName;
        var s = type.FullName();
        if (type is { IsValueType: true, NullableAnnotation: NullableAnnotation.Annotated }
            && !s.EndsWith("?", StringComparison.Ordinal))
            return s + "?";
        return s;
    }
}
