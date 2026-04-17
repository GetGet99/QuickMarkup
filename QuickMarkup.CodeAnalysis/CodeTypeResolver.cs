using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickMarkup.Language.Symbols;
using System.Diagnostics.CodeAnalysis;

namespace QuickMarkup.CodeAnalysis;

class CodeTypeResolver(Compilation compilation, string usings, string @namespace)
{
    ITypeSymbol? Type<T>() => compilation.GetTypeByMetadataName(typeof(T).FullName);
    public ITypeSymbol? String => field ??= Type<string>();
    public ITypeSymbol? Int32 => field ??= Type<int>();
    public ITypeSymbol? Double => field ??= Type<double>();
    public ITypeSymbol? Boolean => field ??= Type<bool>();
    readonly Dictionary<string, INamedTypeSymbol?> Cache = [];
    public INamedTypeSymbol? GetTypeSymbol(string typeName)
    {
        if (Cache.TryGetValue(typeName, out var cached)) return cached;
        var parseOptions = (CSharpParseOptions)
            compilation.SyntaxTrees.First().Options;

        var source = $$"""
            {{usings}}
            namespace {{@namespace}}.QUICKMARKUP_TEMP_NAMESPACE;

            class QUICKMARKUP__TypeResolutionDummy2
            {
                {{typeName}} __field;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var newCompilation = compilation.AddSyntaxTrees(tree);
        var model = newCompilation.GetSemanticModel(tree);

        var field = tree.GetRoot()
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Single();

        var result = model.GetTypeInfo(field.Declaration.Type)
            .Type as INamedTypeSymbol;
        if (result is IErrorTypeSymbol)
        {
            result = null;
        }
        Cache[typeName] = result;
        return result;
    }

    public bool TryGetContentProperty(ITypeSymbol? symbol, [MaybeNullWhen(false)] out IPropertySymbol propertySymbol, out ChildrenModes childrenMode)
    {
        if (symbol is null)
        {
            propertySymbol = null;
            childrenMode = ChildrenModes.None;
            return false;
        }
        if (FindContentAttirbute(symbol) is { } result)
        {
            IPropertySymbol? property = null;
            if (result.ConstructorArguments.Length > 0)
                property = FindProperty(symbol, result.ConstructorArguments[0].Value?.ToString() ?? "");
            else if (result.NamedArguments.Length > 0)
                property = FindProperty(symbol, result.NamedArguments[0].Value.Value?.ToString() ?? "");
            propertySymbol = property;
            childrenMode = FindMethod(propertySymbol?.Type, "Add") is not null ? ChildrenModes.Add : ChildrenModes.Assignment;
            return propertySymbol is not null;
        }
        childrenMode = ChildrenModes.Add;
        propertySymbol = FindProperty(symbol, "Children") ?? FindProperty(symbol, "Items");
        if (propertySymbol is null)
        {
            propertySymbol = FindProperty(symbol, "Child");
            childrenMode = ChildrenModes.Assignment;
        }
        if (propertySymbol is null)
        {
            propertySymbol = FindProperty(symbol, "Content");
            childrenMode = ChildrenModes.Assignment;
        }
        if (propertySymbol is null)
            return false;

        return true;
    }

    public ITypeSymbol? GetCollectionElementType(ITypeSymbol? collectionType)
    {
        var addMethod = FindMethod(collectionType, "Add");
        if (addMethod?.Parameters.Length is 1)
            return addMethod.Parameters[0].Type;

        if (collectionType is not null)
        {
            foreach (var @interface in collectionType.AllInterfaces)
            {
                if (@interface is INamedTypeSymbol namedInterface &&
                    namedInterface.IsGenericType &&
                    namedInterface.TypeArguments.Length is 1 &&
                    namedInterface.ConstructedFrom.FullNameWithoutAnnotation() is
                        "global::System.Collections.Generic.ICollection<T>" or
                        "global::System.Collections.Generic.IList<T>" or
                        "global::System.Collections.Generic.IEnumerable<T>")
                {
                    return namedInterface.TypeArguments[0];
                }
            }
        }

        return null;
    }

    static AttributeData? FindContentAttirbute(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type;
             current != null;
             current = current.BaseType)
        {
            foreach (var attr in current.GetAttributes())
            {
                if (attr.AttributeClass?.FullName() is "global::Windows.UI.Xaml.Markup.ContentPropertyAttribute" or "global::Microsoft.UI.Xaml.Markup.ContentPropertyAttribute")
                {
                    return attr;
                }
            }
        }
        return null;
    }

    public static IPropertySymbol? FindProperty(ITypeSymbol? type, string property)
    {
        for (ITypeSymbol? current = type;
             current != null;
             current = current.BaseType)
        {
            foreach (var prop in current.GetMembers(property))
            {
                if (prop is IPropertySymbol sym)
                {
                    return sym;
                }
            }
        }
        return null;
    }

    public static IEventSymbol? FindEvent(ITypeSymbol? type, string property)
    {
        for (ITypeSymbol? current = type;
             current != null;
             current = current.BaseType)
        {
            foreach (var prop in current.GetMembers(property))
            {
                if (prop is IEventSymbol sym)
                {
                    return sym;
                }
            }
        }
        return null;
    }

    public bool ShouldAutoNew(ITypeSymbol? value, ITypeSymbol target)
    {
        if (CanAssign(value, target))
            return false;
        if (target is not INamedTypeSymbol sym)
            return false;
        return sym.Constructors.Any(x => x.Parameters.Length is 1 && CanAssign(value, x.Parameters[0].Type));
    }
    // not perfect, will not handle implicit cast
    public bool CanAssign(ITypeSymbol? value, ITypeSymbol? target)
    {
        // null is treated as unknown type
        if (value is null) return false;
        if (target is null) return false;
        if (target.TypeKind is TypeKind.Struct)
        {
            if (value.Equals(target, SymbolEqualityComparer.Default))
            {
                return true;
            }
            if (value.Equals(Int32, SymbolEqualityComparer.Default) && target.Equals(Double, SymbolEqualityComparer.Default))
            {
                return true;
            }
        }
        else if (target.TypeKind is TypeKind.Interface)
        {
            foreach (var @interface in value.AllInterfaces)
            {
                if (target.Equals(@interface, SymbolEqualityComparer.Default))
                {
                    return true;
                }
            }
        }
        else
        {
            for (ITypeSymbol? current = value;
             current != null;
             current = current.BaseType)
            {
                if (current.Equals(target, SymbolEqualityComparer.Default))
                {
                    return true;
                }
            }
        }
        return false;
    }

    static IMethodSymbol? FindMethod(ITypeSymbol? type, string method)
    {
        for (ITypeSymbol? current = type;
             current != null;
             current = current.BaseType)
        {
            foreach (var prop in current.GetMembers(method))
            {
                if (prop is IMethodSymbol sym)
                {
                    return sym;
                }
            }
        }
        if (type is not null)
            foreach (ITypeSymbol? current in type.AllInterfaces)
            {
                foreach (var prop in current.GetMembers(method))
                {
                    if (prop is IMethodSymbol sym)
                    {
                        return sym;
                    }
                }
            }
        return null;
    }
}
