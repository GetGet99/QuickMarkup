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
    string? currentTypeName = null,
    FrameworkConfiguration? frameworkConfiguration = null)
{
    readonly FrameworkConfiguration frameworkConfig = frameworkConfiguration ?? FrameworkConfiguration.Default;
    readonly QuickMarkupGeneratedMemberTable generatedMembers = generatedMembers ?? QuickMarkupGeneratedMemberTable.Empty;
    public QuickMarkupGeneratedMemberTable GeneratedMemberTable => generatedMembers;
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

        // Step 1: Property-level marker attributes
        if (TryGetMarkerProperty(symbol, frameworkConfig.ChildrenPropertyMarkerAttribute, ChildrenModes.Add, out propertySymbol, out childrenMode))
            return true;
        if (TryGetMarkerProperty(symbol, frameworkConfig.ContentPropertyMarkerAttribute, ChildrenModes.Assignment, out propertySymbol, out childrenMode))
            return true;

        // Step 2: Type-specific overrides from framework config
        var fullTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (frameworkConfig.TypeSpecificOverrides.TryGetValue(fullTypeName, out var typeOverride))
        {
            propertySymbol = FindProperty(symbol, typeOverride.PropertyName);
            if (propertySymbol is not null)
            {
                childrenMode = typeOverride.Mode;
                return true;
            }
        }

        // Step 3: External framework attributes (like ContentPropertyAttribute)
        if (FindContentAttribute(symbol, frameworkConfig.ExternalContentPropertyAttributeFullNames) is { } result)
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

        // Step 4: Framework default property names
        foreach (var rule in frameworkConfig.DefaultContentPropertyNames)
        {
            propertySymbol = FindProperty(symbol, rule.PropertyName);
            if (propertySymbol is not null)
            {
                childrenMode = rule.Mode;
                return true;
            }
        }

        // Step 5: No match
        propertySymbol = null;
        childrenMode = ChildrenModes.None;
        return false;
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

    AttributeData? FindContentAttribute(ITypeSymbol type, IReadOnlyList<string> externalAttributeFullNames)
    {
        foreach (var attrFullName in externalAttributeFullNames)
        {
            for (ITypeSymbol? current = type; current != null; current = current.BaseType)
            {
                foreach (var attr in current.GetAttributes())
                {
                    if (attr.AttributeClass?.FullName() == attrFullName)
                        return attr;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if the type has a property marked with the specified marker attribute.
    /// Returns true if found, with the property and mode.
    /// </summary>
    bool TryGetMarkerProperty(ITypeSymbol type, string markerAttributeFullName, ChildrenModes mode, [MaybeNullWhen(false)] out ResolvedProperty? propertySymbol, out ChildrenModes childrenMode)
    {
        for (ITypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol prop)
                {
                    foreach (var attr in prop.GetAttributes())
                    {
                        if (attr.AttributeClass?.FullName() == markerAttributeFullName)
                        {
                            propertySymbol = FindProperty(type, prop.Name);
                            if (propertySymbol is not null)
                            {
                                childrenMode = mode;
                                return true;
                            }
                        }
                    }
                }
            }
        }
        propertySymbol = null;
        childrenMode = ChildrenModes.None;
        return false;
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

    public HashSet<string> GetRequiredPropertyNames(ITypeSymbol type)
    {
        var names = new HashSet<string>();
        // Check generated member table
        var foundMembers = generatedMembers.FindTypeMembers(type);
        if (foundMembers.HasValue)
        {
            foreach (var kvp in foundMembers.Value.Properties)
            {
                if (kvp.Value is { Kind: QuickMarkupGeneratedPropertyKind.RefValue, IsRequired: true })
                    names.Add(kvp.Key);
            }
        }
        // Check Roslyn properties for the attribute (covers compiled libraries)
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol prop &&
                    prop.GetAttributes().Any(a =>
                        a.AttributeClass?.Name is "QuickMarkupRequiredPropertyAttribute"))
                {
                    names.Add(prop.Name);
                }
            }
        }
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

    public bool TryGetDependencyProperty(ITypeSymbol? type, string property, [NotNullWhen(true)] out string? dependencyPropertyName)
    {
        dependencyPropertyName = null;
        var suffix = frameworkConfig.DependencyProperty.Suffix;
        var typeName = frameworkConfig.DependencyProperty.TypeName;
        var memberName = $"{property}{suffix}";
        var prop = FindRoslynProperty(type, memberName);
        var field = FindField(type, memberName);
        var memberType = prop?.Type ?? field?.Type;
        if (memberType is null || memberType.Name != typeName)
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

        var prefix = frameworkConfig.AttachedProperty.SetPrefix;
        var setMethod = FindMethod(attachedType, $"{prefix}{propertyName}");
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
