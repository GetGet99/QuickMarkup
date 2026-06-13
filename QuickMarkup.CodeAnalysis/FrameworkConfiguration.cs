namespace QuickMarkup.CodeAnalysis;

using QuickMarkup.Language.Symbols;

/// <summary>
/// Represents the fully-resolved configuration for a UI framework,
/// defining how the QuickMarkup source generator discovers children
/// collections, content properties, dependency properties, and attached properties.
/// </summary>
public sealed partial record FrameworkConfiguration
{
    /// <summary>Default content/children property names to try, in priority order.</summary>
    public required IReadOnlyList<ContentPropertyRule> DefaultContentPropertyNames { get; init; }

    /// <summary>Type-specific overrides keyed by fully-qualified type name.</summary>
    public required IReadOnlyDictionary<string, TypeSpecificRule> TypeSpecificOverrides { get; init; }

    /// <summary>Full names of external attributes (e.g., <c>ContentPropertyAttribute</c>) that mark content properties.</summary>
    public required IReadOnlyList<string> ExternalContentPropertyAttributeFullNames { get; init; }

    /// <summary>Configuration for dependency property detection.</summary>
    public required DependencyPropertyConfig DependencyProperty { get; init; }

    /// <summary>Configuration for attached property detection.</summary>
    public required AttachedPropertyConfig AttachedProperty { get; init; }

    /// <summary>Full name of the attribute that marks a property as a children collection.</summary>
    public required string ChildrenPropertyMarkerAttribute { get; init; }

    /// <summary>Full name of the attribute that marks a property as content (single-assignment).</summary>
    public required string ContentPropertyMarkerAttribute { get; init; }
}

/// <summary>Describes a property that should be treated as children/content, with its resolution mode.</summary>
/// <param name="PropertyName">The property name.</param>
/// <param name="Mode">How child elements are assigned (collection Add or single Assignment).</param>
public sealed record ContentPropertyRule(string PropertyName, ChildrenModes Mode);

/// <summary>A type-specific override for content/children property resolution.</summary>
/// <param name="Mode">How child elements are assigned.</param>
/// <param name="PropertyName">The property name to use for this type.</param>
public sealed record TypeSpecificRule(ChildrenModes Mode, string PropertyName);

/// <summary>Configuration for identifying dependency properties.</summary>
/// <param name="TypeName">Expected short name of the dependency property type (e.g., <c>"DependencyProperty"</c>).</param>
/// <param name="Suffix">Suffix appended to property names (e.g., <c>"Property"</c>).</param>
public sealed record DependencyPropertyConfig(string TypeName, string Suffix);

/// <summary>Configuration for identifying attached properties.</summary>
/// <param name="SetPrefix">Prefix before property names (e.g., <c>"Set"</c>).</param>
public sealed record AttachedPropertyConfig(string SetPrefix);

public sealed partial record FrameworkConfiguration
{
    /// <summary>
    /// The default configuration that mirrors the original hardcoded WinUI/UWP behavior.
    /// Used when no <c>[assembly: QuickMarkupFramework]</c> is present.
    /// </summary>
    public static FrameworkConfiguration Default { get; } = new()
    {
        DefaultContentPropertyNames =
        [
            new("Children", ChildrenModes.Add),
            new("Items", ChildrenModes.Add),
            new("Child", ChildrenModes.Assignment),
            new("Content", ChildrenModes.Assignment),
        ],
        TypeSpecificOverrides = new Dictionary<string, TypeSpecificRule>(),
        ExternalContentPropertyAttributeFullNames =
        [
            "global::Windows.UI.Xaml.Markup.ContentPropertyAttribute",
            "global::Microsoft.UI.Xaml.Markup.ContentPropertyAttribute",
        ],
        DependencyProperty = new("DependencyProperty", "Property"),
        AttachedProperty = new("Set"),
        ChildrenPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupChildrenAttribute",
        ContentPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupContentAttribute",
    };
}
