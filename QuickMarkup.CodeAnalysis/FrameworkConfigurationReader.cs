using Microsoft.CodeAnalysis;
using QuickMarkup.Language.Symbols;
using System.Collections.Generic;
using System.Linq;

namespace QuickMarkup.CodeAnalysis;

/// <summary>
/// Reads <see cref="FrameworkConfiguration"/> from assembly-level
/// <c>[assembly: QuickMarkupFramework(typeof(T))]</c> and attributes on the referenced
/// framework type <c>T</c>.
/// </summary>
public static class FrameworkConfigurationReader
{
    /// <summary>
    /// Reads the framework configuration from the compilation's assembly attributes.
    /// Returns <c>null</c> if no <c>[assembly: QuickMarkupFramework(...)]</c> is present;
    /// the caller should fall back to <see cref="FrameworkConfiguration.Default"/>.
    /// </summary>
    public static FrameworkConfiguration? ReadFromCompilation(Compilation compilation)
    {
        // 1. Find the assembly attribute
        var frameworkAttr = compilation.Assembly.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "QuickMarkupFrameworkAttribute"
                && a.AttributeClass.ContainingNamespace?.ToDisplayString() == "QuickMarkup.Infra");

        if (frameworkAttr is null)
            return null;

        // 2. Extract the framework type from the constructor argument
        if (frameworkAttr.ConstructorArguments.Length != 1)
            return null;

        var typeArg = frameworkAttr.ConstructorArguments[0];
        if (typeArg.Kind != TypedConstantKind.Type)
            return null;

        var frameworkType = typeArg.Value as INamedTypeSymbol;
        if (frameworkType is null)
            return null;

        // 3. Enumerate attributes on the framework type
        var defaultContentNames = new List<ContentPropertyRule>();
        var typeOverrides = new Dictionary<string, TypeSpecificRule>();
        var extAttrNames = new List<string>();
        var depPropConfig = new DependencyPropertyConfig("DependencyProperty", "Property"); // defaults
        var attPropConfig = new AttachedPropertyConfig("Set"); // defaults
        string? dataTemplateFactoryFullName = null;

        foreach (var attr in frameworkType.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null)
                continue;

            switch (attrClass.Name)
            {
                case "QuickMarkupChildrenPropertyAttribute":
                    ParsePropertyAttribute(attr, ChildrenModes.Add, defaultContentNames, typeOverrides);
                    break;

                case "QuickMarkupContentPropertyAttribute":
                    ParsePropertyAttribute(attr, ChildrenModes.Assignment, defaultContentNames, typeOverrides);
                    break;

                case "QuickMarkupExternalContentPropertyAttribute":
                    // Takes a single Type argument
                    if (attr.ConstructorArguments.Length == 1
                        && attr.ConstructorArguments[0].Kind == TypedConstantKind.Type)
                    {
                        var extType = attr.ConstructorArguments[0].Value as ITypeSymbol;
                        if (extType is not null && extType is not IErrorTypeSymbol)
                        {
                            // FullyQualifiedFormat already includes global:: prefix
                            var display = extType.ToDisplayString(
                                SymbolDisplayFormat.FullyQualifiedFormat);
                            if (!display.StartsWith("global::"))
                                display = "global::" + display;
                            extAttrNames.Add(display);
                        }
                    }
                    break;

                case "QuickMarkupDataTemplateFactoryAttribute":
                    // Takes a single Type argument
                    if (attr.ConstructorArguments.Length == 1
                        && attr.ConstructorArguments[0].Kind == TypedConstantKind.Type)
                    {
                        var factoryType = attr.ConstructorArguments[0].Value as ITypeSymbol;
                        if (factoryType is not null && factoryType is not IErrorTypeSymbol)
                        {
                            var display = factoryType.ToDisplayString(
                                SymbolDisplayFormat.FullyQualifiedFormat);
                            if (!display.StartsWith("global::"))
                                display = "global::" + display;
                            dataTemplateFactoryFullName = display;
                        }
                    }
                    break;

                case "QuickMarkupDependencyPropertyAttribute":
                    // Constructor: (Type typeName, string suffix)
                    if (attr.ConstructorArguments.Length == 2)
                    {
                        var typeName = "";
                        if (attr.ConstructorArguments[0].Kind == TypedConstantKind.Type)
                        {
                            var dpType = attr.ConstructorArguments[0].Value as ITypeSymbol;
                            if (dpType is not null)
                                typeName = dpType.Name; // Just the short name, e.g. "DependencyProperty"
                        }
                        var suffix = attr.ConstructorArguments[1].Value as string ?? "Property";
                        depPropConfig = new DependencyPropertyConfig(typeName, suffix);
                    }
                    break;

                case "QuickMarkupAttachedPropertyAttribute":
                    // Constructor: (string setPrefix)
                    if (attr.ConstructorArguments.Length == 1)
                    {
                        var prefix = attr.ConstructorArguments[0].Value as string ?? "Set";
                        attPropConfig = new AttachedPropertyConfig(prefix);
                    }
                    break;
            }
        }

        return new FrameworkConfiguration
        {
            DefaultContentPropertyNames = defaultContentNames.Count > 0
                ? defaultContentNames
                : [new("Children", ChildrenModes.Add), new("Content", ChildrenModes.Assignment)],
            TypeSpecificOverrides = typeOverrides,
            ExternalContentPropertyAttributeFullNames = extAttrNames,
            DependencyProperty = depPropConfig,
            AttachedProperty = attPropConfig,
            ChildrenPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupChildrenAttribute",
            ContentPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupContentAttribute",
            DataTemplateFactoryFullName = dataTemplateFactoryFullName,
        };
    }

    /// <summary>
    /// Parses a <see cref="QuickMarkupChildrenPropertyAttribute"/> or
    /// <see cref="QuickMarkupContentPropertyAttribute"/> which can be written as either
    /// <c>(string propertyName)</c> (default for all types) or
    /// <c>(Type type, string propertyName)</c> (type-specific override).
    /// </summary>
    static void ParsePropertyAttribute(
        AttributeData attr,
        ChildrenModes mode,
        List<ContentPropertyRule> defaultNames,
        Dictionary<string, TypeSpecificRule> typeOverrides)
    {
        if (attr.ConstructorArguments.Length == 1
            && attr.ConstructorArguments[0].Kind == TypedConstantKind.Primitive)
        {
            // (string propertyName) — applies to all types
            var name = attr.ConstructorArguments[0].Value as string;
            if (name is not null)
                defaultNames.Add(new ContentPropertyRule(name, mode));
        }
        else if (attr.ConstructorArguments.Length == 2
            && attr.ConstructorArguments[0].Kind == TypedConstantKind.Type
            && attr.ConstructorArguments[1].Kind == TypedConstantKind.Primitive)
        {
            // (Type type, string propertyName) — type-specific override
            var typeSym = attr.ConstructorArguments[0].Value as ITypeSymbol;
            var name = attr.ConstructorArguments[1].Value as string;
            if (typeSym is not null && name is not null && typeSym is not IErrorTypeSymbol)
            {
                var fullName = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                typeOverrides[fullName] = new TypeSpecificRule(mode, name);
            }
        }
    }
}
