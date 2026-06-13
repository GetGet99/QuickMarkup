namespace QuickMarkup.Infra;

/// <summary>
/// Applied at the assembly level to declare which framework class defines
/// the UI framework conventions (children, content, dependency, attached properties).
/// The framework class is never instantiated — only its attributes are inspected
/// by the QuickMarkup source generator.
/// </summary>
/// <example>
/// <code>
/// [assembly: QuickMarkupFramework(typeof(WinUIFramework))]
/// </code>
/// </example>
#pragma warning disable CS9113 // Parameter is unread.
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class QuickMarkupFrameworkAttribute(Type frameworkType) : Attribute;
#pragma warning restore CS9113 // Parameter is unread.

/// <summary>
/// Declares which property name(s) should be treated as children collections
/// (where child elements are added via collection <c>Add</c>).
/// Can be used as a default (<c>[QuickMarkupChildrenProperty("Children")]</c>)
/// or as a type-specific override (<c>[QuickMarkupChildrenProperty(typeof(Panel), "Children")]</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class QuickMarkupChildrenPropertyAttribute(string propertyName) : Attribute
{
    /// <summary>
    /// Creates a type-specific override for <paramref name="type"/>.
    /// </summary>
    public QuickMarkupChildrenPropertyAttribute(Type type, string propertyName) : this(propertyName)
    {
        Type = type;
    }

    /// <summary>When non-null, this override applies only to the specified type.</summary>
    public Type? Type;
    /// <summary>The property name to use as the children collection.</summary>
    public readonly string PropertyName = propertyName;
}

/// <summary>
/// Declares which property name(s) should be treated as content properties
/// (where exactly one child is assigned to a single property).
/// Can be used as a default or as a type-specific override.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class QuickMarkupContentPropertyAttribute(string propertyName) : Attribute
{
    /// <summary>
    /// Creates a type-specific override for <paramref name="type"/>.
    /// </summary>
    public QuickMarkupContentPropertyAttribute(Type type, string propertyName) : this(propertyName)
    {
        Type = type;
    }

    /// <summary>When non-null, this override applies only to the specified type.</summary>
    public Type? Type;
    /// <summary>The property name to use as the content property.</summary>
    public readonly string PropertyName = propertyName;
}

/// <summary>
/// Declares an external attribute type (e.g., <c>ContentPropertyAttribute</c>)
/// that the source generator should respect when resolving content properties
/// on framework types that are decorated with it.
/// </summary>
#pragma warning disable CS9113 // Parameter is unread.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class QuickMarkupExternalContentPropertyAttribute(Type externalAttributeType) : Attribute;
#pragma warning restore CS9113 // Parameter is unread.

/// <summary>
/// Configures how dependency properties are identified.
/// The generator looks for a static field/property named <c>{PropertyName}{Suffix}</c>
/// whose type matches <c>TypeName</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class QuickMarkupDependencyPropertyAttribute(Type typeName, string suffix) : Attribute
{
    /// <summary>The expected type of the dependency property member (e.g., <c>typeof(DependencyProperty)</c>).</summary>
    public Type TypeName { get; } = typeName;
    /// <summary>The suffix appended to the property name (e.g., <c>"Property"</c>).</summary>
    public string Suffix { get; } = suffix;
}

/// <summary>
/// Configures how attached properties are identified.
/// The generator looks for a static <c>Set{SetPrefix}{PropertyName}</c> method.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class QuickMarkupAttachedPropertyAttribute(string setPrefix) : Attribute
{
    /// <summary>The prefix before the property name (e.g., <c>"Set"</c>).</summary>
    public string SetPrefix { get; } = setPrefix;
}

/// <summary>
/// Marks a property as a children collection.
/// When placed on a property, the source generator treats it as the
/// children collection for that type, taking priority over default rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class QuickMarkupChildrenAttribute : Attribute;

/// <summary>
/// Marks a property as a content (single-assignment) property.
/// When placed on a property, the source generator treats it as the
/// content property for that type, taking priority over default rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class QuickMarkupContentAttribute : Attribute;
