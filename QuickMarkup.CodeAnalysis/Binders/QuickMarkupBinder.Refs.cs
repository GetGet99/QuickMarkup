using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis.Binders;

partial class QuickMarkupBinder
{
    /// <summary>Binds ref/computed/provide/inject declarations.</summary>
    public IReadOnlyList<QMRefDeclarationSymbol<ITypeSymbol?>> BindRefDeclarations(
        IEnumerable<RefDeclaration> refs,
        ITypeSymbol? containingType)
    {
        _ = containingType;
        var list = new List<QMRefDeclarationSymbol<ITypeSymbol?>>();
        foreach (var r in refs)
            list.Add(BindRefDeclaration(r));
        return list;
    }

    QMRefDeclarationSymbol<ITypeSymbol?> BindRefDeclaration(RefDeclaration r)
    {
        ITypeSymbol? typeSym = resolver.GetTypeSymbol(r.Type.Type);
        if (r.Type.IsTypeNullable && typeSym is not null)
            typeSym = typeSym.WithNullableAnnotation(NullableAnnotation.Annotated);

        IQMValueSymbol? defaultSym = null;
        if (r.DefaultValue is { } dv)
        {
            defaultSym = Bind(dv, typeSym, null);
        }

        var attrs = new List<QMCompileTimeAttributeSymbol>(r.Attributes.Count);
        foreach (var a in r.Attributes)
            attrs.Add(BindCompileTimeAttribute(a));

        if (r.Kind is not RefDeclarationKind.Ref)
        {
            if (r.IsStatic)
                Error(r, "Unsupported: Provide/Inject cannot be static");
            if (r.IsRequired)
                Error(r, "Unsupported: required keyword is not supported on Provide/Inject");
            if (r.IsComputedDeclaration)
                Error(r, "Unsupported: Provide/Inject cannot use computed syntax");
            r = r with
            {
                IsStatic = false,
                IsRequired = false,
                IsComputedDeclaration = false
            };
        }

        return new QMRefDeclarationSymbol<ITypeSymbol?>(
            r.IsComputedDeclaration ? RefDeclarationKind.Computed : r.Kind,
            typeSym,
            r.Name.Name,
            defaultSym,
            r.Accessibility switch
            {
                AST.Accessibility.Public => ResolvedAccessibility.Public,
                AST.Accessibility.Private => ResolvedAccessibility.Private,
                AST.Accessibility.Protected => ResolvedAccessibility.Protected,
                AST.Accessibility.Default => r.Kind switch
                {
                    RefDeclarationKind.Ref or RefDeclarationKind.Computed => ResolvedAccessibility.Public,
                    RefDeclarationKind.Provide or RefDeclarationKind.Inject or RefDeclarationKind.InjectOptional => ResolvedAccessibility.Private,
                    _ => throw new NotImplementedException()
                },
                _ => throw new NotImplementedException()
            },
            r.IsStatic,
            r.IsRequired,
            attrs);
    }

    QMCompileTimeAttributeSymbol BindCompileTimeAttribute(QMAttribute attr)
    {
        var pos = new List<IQMValueSymbol>(attr.Arguments.Positionals.Count);
        foreach (var p in attr.Arguments.Positionals)
            pos.Add(BindCompileTimeArgumentValue(p));

        var named = new List<QMAttributeNamedArgumentSymbol>(attr.Arguments.Named.Count);
        foreach (var n in attr.Arguments.Named)
            named.Add(new QMAttributeNamedArgumentSymbol(n.Name.Name, BindCompileTimeArgumentValue(n.Value)));

        return new QMCompileTimeAttributeSymbol(
            attr.TargetSpecifier?.Name,
            attr.AttributeName.Name,
            pos,
            named);
    }

    /// <summary>Attribute arguments may use bare identifiers (no enum type); otherwise reuse template value binding.</summary>
    IQMValueSymbol BindCompileTimeArgumentValue(QuickMarkupValue value) => value switch
    {
        QuickMarkupIdentifier id => new QMValueSymbol<ITypeSymbol>(null, id.Identifier),
        _ => utils.Bind(value, null),
    };
}
