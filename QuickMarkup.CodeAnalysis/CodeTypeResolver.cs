using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;
using System.Diagnostics.CodeAnalysis;

namespace QuickMarkup.CodeAnalysis;

class CodeTypeResolver(
    Compilation compilation,
    string usings,
    string @namespace,
    QuickMarkupGeneratedMemberTable? generatedMembers = null,
    string? currentTypeName = null)
{
    readonly QuickMarkupGeneratedMemberTable generatedMembers = generatedMembers ?? QuickMarkupGeneratedMemberTable.Empty;
    public const string ComponentOutputPropertyName = "MarkupNode";
    ITypeSymbol? Type<T>() => compilation.GetTypeByMetadataName(typeof(T).FullName!);
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
            #nullable enable
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

        var fieldSymbol = model.GetDeclaredSymbol(field.Declaration.Variables.Single()) as IFieldSymbol;
        var result = fieldSymbol?.Type as INamedTypeSymbol;
        if (result is IErrorTypeSymbol)
        {
            result = null;
        }
        Cache[typeName] = result;
        return result;
    }

    public bool TryGetContentProperty(ITypeSymbol? symbol, [MaybeNullWhen(false)] out ResolvedProperty? propertySymbol, out ChildrenModes childrenMode)
    {
        if (symbol is null)
        {
            propertySymbol = null;
            childrenMode = ChildrenModes.None;
            return false;
        }
        if (FindContentAttirbute(symbol) is { } result)
        {
            ResolvedProperty? property = null;
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

    public QMComponentKind GetComponentKind(ITypeSymbol? symbol, out ITypeSymbol? outputType)
    {
        outputType = null;
        if (symbol is null)
            return QMComponentKind.None;

        QMComponentKind kind = QMComponentKind.None;
        foreach (var @interface in symbol.AllInterfaces)
        {
            if (@interface is not INamedTypeSymbol namedInterface ||
                !namedInterface.IsGenericType ||
                namedInterface.TypeArguments.Length is not 1)
                continue;

            var interfaceName = $"{namedInterface.ConstructedFrom.ContainingNamespace}.{namedInterface.ConstructedFrom.MetadataName}";
            var nextKind = interfaceName switch
            {
                "QuickMarkup.Infra.IQuickMarkupComponent`1" => QMComponentKind.Single,
                "QuickMarkup.Infra.IQuickMarkupFragmentComponent`1" => QMComponentKind.Fragment,
                _ => QMComponentKind.None
            };
            if (nextKind is QMComponentKind.None)
                continue;

            if (kind is not QMComponentKind.None)
                continue;

            kind = nextKind;
            outputType = namedInterface.TypeArguments[0];
        }

        return kind;
    }

    public int CountComponentInterfaces(ITypeSymbol? symbol)
    {
        if (symbol is null)
            return 0;

        int count = 0;
        foreach (var @interface in symbol.AllInterfaces)
        {
            if (@interface is not INamedTypeSymbol namedInterface ||
                !namedInterface.IsGenericType ||
                namedInterface.TypeArguments.Length is not 1)
                continue;

            var interfaceName = $"{namedInterface.ConstructedFrom.ContainingNamespace}.{namedInterface.ConstructedFrom.MetadataName}";
            count += interfaceName switch
            {
                "QuickMarkup.Infra.IQuickMarkupComponent`1" => 1,
                "QuickMarkup.Infra.IQuickMarkupFragmentComponent`1" => 1,
                _ => 0
            };
        }

        return count;
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

    public ResolvedProperty? FindProperty(ITypeSymbol? type, string property)
        => generatedMembers.FindProperty(type, property, currentTypeName, ResolveGeneratedPropertyType,
            GenerateMembersForCSharpAttribute);

    QuickMarkupGeneratedTypeMembers? GenerateMembersForCSharpAttribute(INamedTypeSymbol type)
    {
        var attr = type.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.FullName() == "global::QuickMarkup.SourceGen.QuickMarkupAttribute");
        if (attr is null || attr.ConstructorArguments.Length == 0)
            return null;

        var markupString = attr.ConstructorArguments[0].Value as string;
        if (string.IsNullOrEmpty(markupString))
            return null;

        var sfc = QuickMarkupProviderExtension.Parse(markupString);

        var ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
        var target = new QuickMarkupTargetContext(
            Namespace: ns,
            TypeName: type.Name,
            FullTypeName: type.ToDisplayString(),
            FileName: type.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "",
            AttributeLocation: default,
            AttributeLineSpan: default);

        return QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(
            new QuickMarkupParsedAttribute(target, sfc),
            compilation,
            CancellationToken.None);
    }

    public HashSet<string> GetPropertyNames(ITypeSymbol type)
    {
        var names = new HashSet<string>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol prop)
                    names.Add(prop.Name);
            }
        }
        foreach (var name in generatedMembers.GetGeneratedPropertyNames(type))
            names.Add(name);
        return names;
    }

    ITypeSymbol? ResolveGeneratedPropertyType(string typeName)
        => GetTypeSymbol(typeName);

    public static IPropertySymbol? FindRoslynProperty(ITypeSymbol? type, string property)
    {
        for (ITypeSymbol? current = type;
             current != null;
             current = current.BaseType)
        {
            if (FindRoslynPropertyOnTypeOnly(current, property) is { } sym)
                return sym;
        }
        return null;
    }

    public static IPropertySymbol? FindRoslynPropertyOnTypeOnly(ITypeSymbol? type, string property)
    {
        if (type is null)
            return null;

        foreach (var prop in type.GetMembers(property))
        {
            if (prop is IPropertySymbol sym)
            {
                return sym;
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

    public static IFieldSymbol? FindField(ITypeSymbol? type, string field)
    {
        for (ITypeSymbol? current = type;
             current != null;
             current = current.BaseType)
        {
            foreach (var member in current.GetMembers(field))
            {
                if (member is IFieldSymbol sym)
                {
                    return sym;
                }
            }
        }
        return null;
    }

    public static bool TryGetDependencyProperty(ITypeSymbol? type, string property, [NotNullWhen(true)] out string? dependencyPropertyName)
    {
        dependencyPropertyName = null;
        var memberName = $"{property}Property";
        var prop = FindRoslynProperty(type, memberName);
        var field = FindField(type, memberName);
        var memberType = prop?.Type ?? field?.Type;
        if (memberType?.Name is not "DependencyProperty")
            return false;

        if (prop is { IsStatic: false } || field is { IsStatic: false })
            return false;

        if (prop is not null)
        {
            dependencyPropertyName = $"{prop.ContainingType.FullNameWithoutAnnotation()}.{prop.Name}";
            return true;
        }

        if (field is not null)
        {
            dependencyPropertyName = $"{field.ContainingType.FullNameWithoutAnnotation()}.{field.Name}";
            return true;
        }

        return false;
    }

    public bool TryGetAttachedPropertyInfo(
        ITypeSymbol? attachedType,
        string propertyName,
        [NotNullWhen(true)] out ITypeSymbol? valueType,
        out bool isDependencyProperty,
        out string dependencyPropertyName)
    {
        valueType = null;
        isDependencyProperty = false;
        dependencyPropertyName = "";

        if (attachedType is null)
            return false;

        // Look for Set{PropertyName}(DependencyObject, valueType) static method
        var setMethod = FindMethod(attachedType, $"Set{propertyName}");
        if (setMethod is { IsStatic: true, Parameters.Length: 2 })
        {
            valueType = setMethod.Parameters[1].Type;

            // Also check for dependency property pattern (FooProperty field)
            isDependencyProperty = TryGetDependencyProperty(attachedType, propertyName, out var depName);
            dependencyPropertyName = depName ?? "";

            return true;
        }

        return false;
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
