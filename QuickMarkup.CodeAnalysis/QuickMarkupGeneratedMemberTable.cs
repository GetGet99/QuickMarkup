using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis;

public enum QuickMarkupGeneratedPropertyKind
{
    RefValue,
    RefBacking,
    ComputedValue,
    ComputedBacking,
    AsyncComputedValue,
    AsyncComputedBacking,
    AsyncComputedStatus,
    AsyncComputedFailure,
    ComponentOutput,
    ProvideValue,
    InjectValue
}

public readonly record struct QuickMarkupGeneratedPropertySymbol(
    string Name,
    string? TypeName,
    ResolvedAccessibility Accessibility,
    QuickMarkupGeneratedPropertyKind Kind,
    bool IsRequired,
    bool IsNullableAware
);

public readonly record struct ResolvedProperty(
    string Name,
    ITypeSymbol? Type,
    IPropertySymbol? RoslynSymbol,
    QuickMarkupGeneratedPropertySymbol? GeneratedSymbol,
    bool IsRequired,
    bool IsNullableAware
)
{
    public static ResolvedProperty FromRoslyn(IPropertySymbol property, bool isRequired)
        => new(property.Name, property.Type, property, null, isRequired, property.NullableAnnotation is not NullableAnnotation.None);

    public static ResolvedProperty FromGenerated(QuickMarkupGeneratedPropertySymbol property, ITypeSymbol? type)
        => new(property.Name, type, null, property, property.IsRequired, property.IsNullableAware);
}

public readonly record struct QuickMarkupConstructorParameter(
    string TypeName,
    string Name
);

public readonly record struct QuickMarkupGeneratedTypeMembers(
    string FullTypeName,
    IReadOnlyDictionary<string, QuickMarkupGeneratedPropertySymbol> Properties,
    QuickMarkupInitializationMode InitMode,
    bool SupportsContext = false,
    string? QuickMarkupConstructorMethodName = null,
    IReadOnlyList<QuickMarkupConstructorParameter>? ConstructorParameters = null
);

public sealed class QuickMarkupGeneratedMemberTable
{
    public static QuickMarkupGeneratedMemberTable Empty { get; } = new([]);

    readonly Dictionary<string, QuickMarkupGeneratedTypeMembers> types;

    public QuickMarkupGeneratedMemberTable(IEnumerable<QuickMarkupGeneratedTypeMembers> types)
    {
        Dictionary<string, QuickMarkupGeneratedTypeMembers> dict = [];
        foreach (var type in types)
        {
            dict[type.FullTypeName] = type;
        }
        this.types = dict;
    }

    public void RemoveType(string fullTypeName)
    {
        types.Remove(fullTypeName);
    }

    public void UpdateType(QuickMarkupGeneratedTypeMembers members)
    {
        types[members.FullTypeName] = members;
    }

    public ResolvedProperty? FindProperty(
        ITypeSymbol? type,
        string property,
        string? currentTypeName,
        Func<string, ITypeSymbol?> resolveType,
        Func<INamedTypeSymbol, QuickMarkupGeneratedTypeMembers?>? generateForType = null)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (CodeTypeResolver.FindRoslynPropertyOnTypeOnly(current, property) is { } roslynProperty)
            {
                var isRequired = roslynProperty.GetAttributes().Any(a =>
                    a.AttributeClass?.Name is "QuickMarkupRequiredPropertyAttribute");
                return ResolvedProperty.FromRoslyn(roslynProperty, isRequired);
            }

            if (!TryGetTypeMembers(current, out var members, out var currentFullName))
            {
                // Try on-demand generation (e.g. for C# [QuickMarkup] attribute types)
                if (generateForType is not null
                    && current is INamedTypeSymbol namedType
                    && generateForType(namedType) is { } generated)
                {
                    members = generated;
                    currentFullName = generated.FullTypeName;
                    // Cache for future lookups
                    types[generated.FullTypeName] = generated;
                }
                else
                {
                    continue;
                }
            }

            if (!members.Properties.TryGetValue(property, out var generatedProperty))
                continue;

            // TODO: PROTECTED
            if (generatedProperty.Accessibility is not ResolvedAccessibility.Public && currentFullName != currentTypeName)
                continue;

            var generatedPropertyType = generatedProperty.TypeName is null
                ? null
                : resolveType(generatedProperty.TypeName);
            return ResolvedProperty.FromGenerated(generatedProperty, generatedPropertyType);
        }

        return null;
    }

    public QuickMarkupGeneratedTypeMembers? FindTypeMembers(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (TryGetTypeMembers(current, out var members, out _))
                return members;
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

        // FullNameWithoutAnnotation returns "global::Ns.Type" but catalog keys are "Ns.Type"
        if (currentFullName.StartsWith("global::"))
        {
            var withoutGlobal = currentFullName["global::".Length..];
            if (types.TryGetValue(withoutGlobal, out members))
            {
                currentFullName = withoutGlobal;
                return true;
            }
        }

        if (type is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            currentFullName = namedType.ConstructedFrom.FullNameWithoutAnnotation();
            if (types.TryGetValue(currentFullName, out members))
                return true;

            if (currentFullName.StartsWith("global::"))
            {
                var withoutGlobal = currentFullName["global::".Length..];
                if (types.TryGetValue(withoutGlobal, out members))
                {
                    currentFullName = withoutGlobal;
                    return true;
                }
            }
        }

        members = default;
        return false;
    }
}
