using Get.EasyCSharp.GeneratorTools;
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

        var defaultSym = Bind(r.DefaultValue?.Value ?? new QuickMarkupDefault(IsExplicitlyNull: false), typeSym, null);

        var attrs = new List<QMCompileTimeAttributeSymbol>(r.Attributes.Count);
        var kind = r.Kind;

        bool shouldSuppressNullOnCreate = false;
        foreach (var a in r.Attributes)
            attrs.Add(BindCompileTimeAttribute(a));
            
        if (r.DefaultValue?.Kind is DefaultValueKind.Computed)
        {
            if (kind is not RefDeclarationKind.Ref)
                Error(r, "Unsupported: Provide/Inject cannot use computed syntax");
            else
                kind = RefDeclarationKind.Computed;
        }

        if (r.DefaultValue?.Kind is DefaultValueKind.AsyncComputed)
        {
            if (kind is not RefDeclarationKind.Ref)
                Error(r, "Unsupported: Provide/Inject cannot use async computed syntax");
            else
                kind = RefDeclarationKind.AsyncComputed;
        }

        if (kind is RefDeclarationKind.Ref &&
            r.DefaultValue is null &&
            typeSym is not null &&
            !r.Type.IsTypeNullable &&
            !typeSym.IsValueType)
        {
            if (r.IsRequired)
            {
                shouldSuppressNullOnCreate = true;
            } else
            {
                Warn(new QMBinderRefMissingDefaultValueWarning(r.Name.Name, r.Name.Name.Name, typeSym.FullNameWithoutAnnotation()));
            }
        }

        string name = r.Name.Name.Name;
        string? contextName = null;

        
        if (r.Name.AsAllias is not null)
        {
            if (kind is RefDeclarationKind.Provide)
            {
                contextName = r.Name.AsAllias.Name;
            }
            else if (kind is RefDeclarationKind.Inject or RefDeclarationKind.InjectOptional)
            {
                contextName = r.Name.Name.Name;
                name = r.Name.AsAllias.Name;
            }
            else
            {
                Error(r.Name.AsAllias, "`as` keyword can only be used with provide or inject");
            }
        } else
        {
            if (kind is RefDeclarationKind.Provide or RefDeclarationKind.Inject or RefDeclarationKind.InjectOptional)
            {
                contextName = name;
            }
        }

        if (kind is not (RefDeclarationKind.Ref or RefDeclarationKind.Computed or RefDeclarationKind.AsyncComputed))
        {
            if (r.IsStatic)
                Error(r, "Unsupported: Provide/Inject cannot be static");
            if (r.IsRequired)
                Error(r, "Unsupported: required keyword is not supported on Provide/Inject");
            if (r.DefaultValue is not null && kind is RefDeclarationKind.Inject or RefDeclarationKind.InjectOptional)
                Error(r, "Unsupported: Inject does not support default value yet");
            r = r with
            {
                IsStatic = false,
                IsRequired = false
            };
        }

        return new QMRefDeclarationSymbol<ITypeSymbol?>(
            kind,
            typeSym,
            name,
            contextName,
            defaultSym,
            r.Accessibility switch
            {
                AST.Accessibility.Public => ResolvedAccessibility.Public,
                AST.Accessibility.Private => ResolvedAccessibility.Private,
                AST.Accessibility.Protected => ResolvedAccessibility.Protected,
                AST.Accessibility.Default => kind switch
                {
                    RefDeclarationKind.Ref or RefDeclarationKind.Computed or RefDeclarationKind.AsyncComputed => ResolvedAccessibility.Public,
                    RefDeclarationKind.Provide or RefDeclarationKind.Inject or RefDeclarationKind.InjectOptional => ResolvedAccessibility.Private,
                    _ => throw new NotImplementedException()
                },
                _ => throw new NotImplementedException()
            },
            r.IsStatic,
            r.IsRequired,
            shouldSuppressNullOnCreate,
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
