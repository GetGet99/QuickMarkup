using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis.Binders;

partial class QMSourceGenBinders
{
    static readonly QMBinderTagInfo LooseRefValueTagInfo = new(null, "", null, null, ChildrenModes.None);

    /// <summary>Binds ref/computed declarations for tooling and future plugins; phase 1 does not affect ref field codegen.</summary>
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
            defaultSym = Bind(dv, typeSym, LooseRefValueTagInfo);
        }

        var attrs = new List<QMCompileTimeAttributeSymbol>(r.Attributes.Count);
        foreach (var a in r.Attributes)
            attrs.Add(BindCompileTimeAttribute(a));

        return new QMRefDeclarationSymbol<ITypeSymbol?>(
            typeSym,
            r.Name,
            defaultSym,
            r.IsPrivate,
            r.IsComputedDeclaration,
            attrs);
    }

    QMCompileTimeAttributeSymbol BindCompileTimeAttribute(QMCompileTimeAttribute attr)
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
        QuickMarkupIdentifier id => Value(null, id.Identifier),
        _ => Bind(value, null, LooseRefValueTagInfo),
    };
}
