using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;

namespace QuickMarkup.CodeAnalysis;

enum QuickMarkupGeneratedPropertyKind
{
    RefValue,
    RefBacking,
    ComputedValue,
    ComputedBacking,
    ComponentOutput
}

readonly record struct QuickMarkupGeneratedPropertySymbol(
    string Name,
    string? TypeName,
    bool IsPrivate,
    QuickMarkupGeneratedPropertyKind Kind
);

readonly record struct ResolvedProperty(
    string Name,
    ITypeSymbol? Type,
    IPropertySymbol? RoslynSymbol,
    QuickMarkupGeneratedPropertySymbol? GeneratedSymbol
)
{
    public static ResolvedProperty FromRoslyn(IPropertySymbol property)
        => new(property.Name, property.Type, property, null);

    public static ResolvedProperty FromGenerated(QuickMarkupGeneratedPropertySymbol property, ITypeSymbol? type)
        => new(property.Name, type, null, property);
}

readonly record struct QuickMarkupGeneratedTypeMembers(
    string FullTypeName,
    IReadOnlyDictionary<string, QuickMarkupGeneratedPropertySymbol> Properties
);

sealed class QuickMarkupGeneratedMemberTable
{
    public static QuickMarkupGeneratedMemberTable Empty { get; } = new([]);

    readonly IReadOnlyDictionary<string, QuickMarkupGeneratedTypeMembers> types;

    public QuickMarkupGeneratedMemberTable(IEnumerable<QuickMarkupGeneratedTypeMembers> types)
    {
        Dictionary<string, QuickMarkupGeneratedTypeMembers> dict = [];
        foreach (var type in types)
        {
            dict[type.FullTypeName] = type;
        }
        this.types = dict;
    }

    public ResolvedProperty? FindProperty(
        ITypeSymbol? type,
        string property,
        string? currentTypeName,
        Func<string, ITypeSymbol?> resolveType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (CodeTypeResolver.FindRoslynPropertyOnTypeOnly(current, property) is { } roslynProperty)
                return ResolvedProperty.FromRoslyn(roslynProperty);

            if (!TryGetTypeMembers(current, out var members, out var currentFullName))
                continue;

            if (!members.Properties.TryGetValue(property, out var generatedProperty))
                continue;

            if (generatedProperty.IsPrivate && currentFullName != currentTypeName)
                continue;

            var generatedPropertyType = generatedProperty.TypeName is null
                ? null
                : resolveType(generatedProperty.TypeName);
            return ResolvedProperty.FromGenerated(generatedProperty, generatedPropertyType);
        }

        return null;
    }

    public IEnumerable<string> GetGeneratedPropertyNames(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (TryGetTypeMembers(current, out var members, out _))
            {
                foreach (var name in members.Properties.Keys)
                    yield return name;
            }
        }
    }

    bool TryGetTypeMembers(
        ITypeSymbol type,
        out QuickMarkupGeneratedTypeMembers members,
        out string currentFullName)
    {
        currentFullName = type.FullNameWithoutAnnotation();
        if (types.TryGetValue(currentFullName, out members))
            return true;

        if (type is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            currentFullName = namedType.ConstructedFrom.FullNameWithoutAnnotation();
            if (types.TryGetValue(currentFullName, out members))
                return true;
        }

        members = default;
        return false;
    }
}
