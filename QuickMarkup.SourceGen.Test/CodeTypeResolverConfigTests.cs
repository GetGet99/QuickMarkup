using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using CodeAnalysis = QuickMarkup.CodeAnalysis;
using Symbols = QuickMarkup.Language.Symbols;

namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class CodeTypeResolverConfigTests
{
    /// <summary>
    /// Common metadata references for in-memory compilations.
    /// </summary>
    private static MetadataReference[] GetCoreReferences()
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };

        try
        {
            var runtimeAsm = System.Reflection.Assembly.Load("System.Runtime");
            if (runtimeAsm is not null)
                refs.Add(MetadataReference.CreateFromFile(runtimeAsm.Location));
        }
        catch { }

        try
        {
            var linqAsm = System.Reflection.Assembly.Load("System.Linq");
            if (linqAsm is not null)
                refs.Add(MetadataReference.CreateFromFile(linqAsm.Location));
        }
        catch { }

        return [.. refs];
    }

    /// <summary>
    /// Source for framework attributes + marker attributes.
    /// </summary>
    private const string AttributeDefinitions = @"
using System;
namespace QuickMarkup.Infra
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class QuickMarkupFrameworkAttribute(Type frameworkType) : Attribute;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class QuickMarkupChildrenPropertyAttribute(string propertyName) : Attribute
    {
        public QuickMarkupChildrenPropertyAttribute(Type type, string propertyName) : this(propertyName) { Type = type; }
        public Type? Type;
        public readonly string PropertyName = propertyName;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class QuickMarkupContentPropertyAttribute(string propertyName) : Attribute
    {
        public QuickMarkupContentPropertyAttribute(Type type, string propertyName) : this(propertyName) { Type = type; }
        public Type? Type;
        public readonly string PropertyName = propertyName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class QuickMarkupChildrenAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Property)]
    public class QuickMarkupContentAttribute : Attribute;
}";

    /// <summary>
    /// Minimal test type sources that mirror TestControls.cs.
    /// </summary>
    private const string TestTypeSources = @"
using System.Collections.ObjectModel;

namespace QuickMarkup.SourceGen.Test
{
    public class TestElement
    {
    }

    public class TestElementCollection : Collection<TestElement>
    {
    }

    public sealed class TestPanel : TestElement
    {
        public TestElementCollection Children { get; } = new TestElementCollection();
    }

    public sealed class TestButton : TestElement
    {
        public TestElement? Content { get; set; }
    }

    public sealed class ItemsOnlyElement : TestElement
    {
        public TestElementCollection Items { get; } = new TestElementCollection();
    }

    public sealed class ChildOnlyElement : TestElement
    {
        public TestElement? Child { get; set; }
    }

    public sealed class ContentOnlyElement : TestElement
    {
        public TestElement? Content { get; set; }
    }

    public class DependencyProperty { }

    public sealed class TestDependencyHoldButton : TestElement
    {
        public static readonly DependencyProperty IsHoldingProperty = new DependencyProperty();
        public bool IsHolding { get; set; }
    }

    public sealed class Grid
    {
        public static readonly DependencyProperty RowProperty = new DependencyProperty();
        public static void SetRow(TestElement element, int value) { }
        public static int GetRow(TestElement element) => 0;
    }

    // Additional types for marker attribute tests
    public sealed class MarkedChildPanel : TestElement
    {
        [QuickMarkup.Infra.QuickMarkupChildren]
        public TestElementCollection CustomChildren { get; } = new TestElementCollection();
    }

    public sealed class MarkedContentButton : TestElement
    {
        [QuickMarkup.Infra.QuickMarkupContent]
        public TestElement? CustomContent { get; set; }
    }
}";

    /// <summary>
    /// Creates an in-memory compilation with framework attributes, test types,
    /// and optional additional sources.
    /// </summary>
    static CSharpCompilation CreateCompilation(string? extraSource = null)
    {
        var allSource = AttributeDefinitions + "\n" + TestTypeSources;
        if (extraSource is not null)
            allSource += "\n" + extraSource;

        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(allSource, options);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            GetCoreReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation;
    }

    /// <summary>
    /// Creates a <see cref="CodeAnalysis.CodeTypeResolver"/> from an in-memory compilation.
    /// </summary>
    static CodeAnalysis.CodeTypeResolver CreateResolver(
        Compilation compilation,
        CodeAnalysis.FrameworkConfiguration? config = null,
        string? ns = null)
    {
        return new CodeAnalysis.CodeTypeResolver(
            compilation,
            usings: "using QuickMarkup.SourceGen.Test;",
            @namespace: ns ?? "TestNamespace",
            frameworkConfiguration: config);
    }

    // ---------------------------------------------------------------
    // ResolverConfigTests
    // ---------------------------------------------------------------

    [TestMethod]
    public void TryGetContentProperty_WithCustomConfig_UsesCustomPropertyNames()
    {
        var customConfig = new CodeAnalysis.FrameworkConfiguration
        {
            DefaultContentPropertyNames =
            [
                new CodeAnalysis.ContentPropertyRule("CustomItems", Symbols.ChildrenModes.Add),
                new CodeAnalysis.ContentPropertyRule("CustomChild", Symbols.ChildrenModes.Assignment),
            ],
            TypeSpecificOverrides = new Dictionary<string, CodeAnalysis.TypeSpecificRule>(),
            ExternalContentPropertyAttributeFullNames = [],
            DependencyProperty = new CodeAnalysis.DependencyPropertyConfig("DependencyProperty", "Property"),
            AttachedProperty = new CodeAnalysis.AttachedPropertyConfig("Set"),
            ChildrenPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupChildrenAttribute",
            ContentPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupContentAttribute",
            DataTemplateFactoryFullName = null,
        };

        var extraSource = @"
namespace QuickMarkup.SourceGen.Test
{
    public sealed class CustomItemsElement : TestElement
    {
        public TestElementCollection CustomItems { get; } = new TestElementCollection();
    }

    public sealed class CustomChildElement : TestElement
    {
        public TestElement? CustomChild { get; set; }
    }
}";

        var extCompilation = CreateCompilation(extraSource);
        var extResolver = CreateResolver(extCompilation, customConfig);

        var customItemsType = extResolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.CustomItemsElement");
        Assert.IsNotNull(customItemsType, "CustomItemsElement type should be resolved");

        var found = extResolver.TryGetContentProperty(customItemsType, out var property, out var mode);
        Assert.IsTrue(found, "Should find content property with custom config");
        Assert.IsNotNull(property);
        Assert.AreEqual("CustomItems", property.Value.Name);
        Assert.AreEqual(Symbols.ChildrenModes.Add, mode);

        var customChildType = extResolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.CustomChildElement");
        Assert.IsNotNull(customChildType, "CustomChildElement type should be resolved");

        found = extResolver.TryGetContentProperty(customChildType, out property, out mode);
        Assert.IsTrue(found, "Should find content property with custom config");
        Assert.IsNotNull(property);
        Assert.AreEqual("CustomChild", property.Value.Name);
        Assert.AreEqual(Symbols.ChildrenModes.Assignment, mode);
    }

    [TestMethod]
    public void TryGetContentProperty_PropertyLevelMarker_HasPriority()
    {
        var customConfig = new CodeAnalysis.FrameworkConfiguration
        {
            DefaultContentPropertyNames =
            [
                new CodeAnalysis.ContentPropertyRule("Children", Symbols.ChildrenModes.Add),
            ],
            TypeSpecificOverrides = new Dictionary<string, CodeAnalysis.TypeSpecificRule>(),
            ExternalContentPropertyAttributeFullNames = [],
            DependencyProperty = new CodeAnalysis.DependencyPropertyConfig("DependencyProperty", "Property"),
            AttachedProperty = new CodeAnalysis.AttachedPropertyConfig("Set"),
            ChildrenPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupChildrenAttribute",
            ContentPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupContentAttribute",
            DataTemplateFactoryFullName = null,
        };

        var compilation = CreateCompilation();
        var resolver = CreateResolver(compilation, customConfig);

        // MarkedChildPanel has a property marked [QuickMarkupChildren] on CustomChildren
        // This should take priority over the default config
        var markedType = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.MarkedChildPanel");
        Assert.IsNotNull(markedType, "MarkedChildPanel type should be resolved");

        var found = resolver.TryGetContentProperty(markedType, out var property, out var mode);
        Assert.IsTrue(found, "Should find content property via marker attribute");
        Assert.IsNotNull(property);
        Assert.AreEqual("CustomChildren", property.Value.Name);
        Assert.AreEqual(Symbols.ChildrenModes.Add, mode);
    }

    [TestMethod]
    public void TryGetContentProperty_ContentMarker_HasPriority()
    {
        var compilation = CreateCompilation();
        var resolver = CreateResolver(compilation);

        var markedType = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.MarkedContentButton");
        Assert.IsNotNull(markedType, "MarkedContentButton type should be resolved");

        var found = resolver.TryGetContentProperty(markedType, out var property, out var mode);
        Assert.IsTrue(found, "Should find content property via content marker attribute");
        Assert.IsNotNull(property);
        Assert.AreEqual("CustomContent", property.Value.Name);
        Assert.AreEqual(Symbols.ChildrenModes.Assignment, mode);
    }

    [TestMethod]
    public void TryGetDependencyProperty_WithCustomConfig_UsesCustomSuffix()
    {
        var customConfig = new CodeAnalysis.FrameworkConfiguration
        {
            DefaultContentPropertyNames =
            [
                new CodeAnalysis.ContentPropertyRule("Children", Symbols.ChildrenModes.Add),
                new CodeAnalysis.ContentPropertyRule("Content", Symbols.ChildrenModes.Assignment),
            ],
            TypeSpecificOverrides = new Dictionary<string, CodeAnalysis.TypeSpecificRule>(),
            ExternalContentPropertyAttributeFullNames = [],
            DependencyProperty = new CodeAnalysis.DependencyPropertyConfig("DependencyProperty", "DepProperty"),
            AttachedProperty = new CodeAnalysis.AttachedPropertyConfig("Set"),
            ChildrenPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupChildrenAttribute",
            ContentPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupContentAttribute",
            DataTemplateFactoryFullName = null,
        };

        var extraSource = @"
namespace QuickMarkup.SourceGen.Test
{
    public sealed class CustomDependencyButton : TestElement
    {
        public static readonly DependencyProperty IsHoldingDepProperty = new DependencyProperty();
        public bool IsHolding { get; set; }
    }
}";

        var compilation = CreateCompilation(extraSource);
        var resolver = CreateResolver(compilation, customConfig);

        var type = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.CustomDependencyButton");
        Assert.IsNotNull(type);

        var found = resolver.TryGetDependencyProperty(type, "IsHolding", out var depPropertyName);
        Assert.IsTrue(found, "Should detect dependency property with custom suffix");
        Assert.IsNotNull(depPropertyName);
        Assert.Contains("IsHoldingDepProperty",
depPropertyName, $"Expected dependency property name to contain 'IsHoldingDepProperty' but got '{depPropertyName}'");
    }

    [TestMethod]
    public void TryGetAttachedPropertyInfo_WithCustomConfig_UsesCustomPrefix()
    {
        var customConfig = new CodeAnalysis.FrameworkConfiguration
        {
            DefaultContentPropertyNames =
            [
                new CodeAnalysis.ContentPropertyRule("Children", Symbols.ChildrenModes.Add),
                new CodeAnalysis.ContentPropertyRule("Content", Symbols.ChildrenModes.Assignment),
            ],
            TypeSpecificOverrides = new Dictionary<string, CodeAnalysis.TypeSpecificRule>(),
            ExternalContentPropertyAttributeFullNames = [],
            DependencyProperty = new CodeAnalysis.DependencyPropertyConfig("DependencyProperty", "Property"),
            AttachedProperty = new CodeAnalysis.AttachedPropertyConfig("Register"),
            ChildrenPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupChildrenAttribute",
            ContentPropertyMarkerAttribute = "global::QuickMarkup.Infra.QuickMarkupContentAttribute",
            DataTemplateFactoryFullName = null,
        };

        var extraSource = @"
namespace QuickMarkup.SourceGen.Test
{
    public sealed class CustomGrid
    {
        public static void RegisterRow(TestElement element, int value) { }
    }
}";

        var compilation = CreateCompilation(extraSource);
        var resolver = CreateResolver(compilation, customConfig);

        var type = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.CustomGrid");
        Assert.IsNotNull(type);

        var found = resolver.TryGetAttachedPropertyInfo(type, "Row", out var valueType, out var isDep, out var depName);
        Assert.IsTrue(found, "Should detect attached property with custom prefix");
        Assert.IsNotNull(valueType);
    }

    // ---------------------------------------------------------------
    // BackwardCompatTests
    // ---------------------------------------------------------------

    [TestMethod]
    public void TryGetContentProperty_WithDefaultConfig_ReturnsExpectedProperties()
    {
        var compilation = CreateCompilation();
        var resolver = CreateResolver(compilation);

        // TestPanel -> Children (Add)
        var panelType = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.TestPanel");
        Assert.IsNotNull(panelType);
        var found = resolver.TryGetContentProperty(panelType, out var property, out var mode);
        Assert.IsTrue(found, "TestPanel should resolve content property");
        Assert.IsNotNull(property);
        Assert.AreEqual("Children", property.Value.Name);
        Assert.AreEqual(Symbols.ChildrenModes.Add, mode);

        // TestButton -> Content (Assignment)
        var buttonType = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.TestButton");
        Assert.IsNotNull(buttonType);
        found = resolver.TryGetContentProperty(buttonType, out property, out mode);
        Assert.IsTrue(found, "TestButton should resolve content property");
        Assert.IsNotNull(property);
        Assert.AreEqual("Content", property.Value.Name);
        Assert.AreEqual(Symbols.ChildrenModes.Assignment, mode);

        // ItemsOnlyElement -> Items (Add)
        var itemsType = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.ItemsOnlyElement");
        Assert.IsNotNull(itemsType);
        found = resolver.TryGetContentProperty(itemsType, out property, out mode);
        Assert.IsTrue(found, "ItemsOnlyElement should resolve content property");
        Assert.IsNotNull(property);
        Assert.AreEqual("Items", property.Value.Name);
        Assert.AreEqual(Symbols.ChildrenModes.Add, mode);

        // ChildOnlyElement -> Child (Assignment)
        var childType = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.ChildOnlyElement");
        Assert.IsNotNull(childType);
        found = resolver.TryGetContentProperty(childType, out property, out mode);
        Assert.IsTrue(found, "ChildOnlyElement should resolve content property");
        Assert.IsNotNull(property);
        Assert.AreEqual("Child", property.Value.Name);
        Assert.AreEqual(Symbols.ChildrenModes.Assignment, mode);

        // ContentOnlyElement -> Content (Assignment)
        var contentOnlyType = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.ContentOnlyElement");
        Assert.IsNotNull(contentOnlyType);
        found = resolver.TryGetContentProperty(contentOnlyType, out property, out mode);
        Assert.IsTrue(found, "ContentOnlyElement should resolve content property");
        Assert.IsNotNull(property);
        Assert.AreEqual("Content", property.Value.Name);
        Assert.AreEqual(Symbols.ChildrenModes.Assignment, mode);
    }

    [TestMethod]
    public void TryGetDependencyProperty_WithDefaultConfig_DetectsProperty()
    {
        var compilation = CreateCompilation();
        var resolver = CreateResolver(compilation);

        var type = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.TestDependencyHoldButton");
        Assert.IsNotNull(type);

        var found = resolver.TryGetDependencyProperty(type, "IsHolding", out var depPropertyName);
        Assert.IsTrue(found, "Should detect IsHoldingProperty as dependency property");
        Assert.IsNotNull(depPropertyName);
        Assert.Contains("IsHoldingProperty",
depPropertyName, $"Expected dependency property name to contain 'IsHoldingProperty' but got '{depPropertyName}'");
    }

    [TestMethod]
    public void TryGetAttachedPropertyInfo_WithDefaultConfig_DetectsAttachedProperty()
    {
        var compilation = CreateCompilation();
        var resolver = CreateResolver(compilation);

        var type = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.Grid");
        Assert.IsNotNull(type);

        var found = resolver.TryGetAttachedPropertyInfo(type, "Row", out var valueType, out var isDep, out var depName);
        Assert.IsTrue(found, "Grid.Row should be detected as attached property");
        Assert.IsNotNull(valueType);
        Assert.IsTrue(isDep, "Grid.Row should also be a dependency property");
        Assert.Contains("RowProperty",
depName, $"Expected dependency property name to contain 'RowProperty' but got '{depName}'");
    }

    [TestMethod]
    public void TryGetContentProperty_WithDefaultConfig_TypeWithoutContentProperty_ReturnsFalse()
    {
        var extraSource = @"
namespace QuickMarkup.SourceGen.Test
{
    public sealed class NoContentElement : TestElement
    {
        public int SomeValue { get; set; }
    }
}";

        var extCompilation = CreateCompilation(extraSource);
        var extResolver = CreateResolver(extCompilation);

        var type = extResolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.NoContentElement");
        Assert.IsNotNull(type);

        var found = extResolver.TryGetContentProperty(type, out var property, out var mode);
        Assert.IsFalse(found, "Type without content property should return false");
        Assert.AreEqual(Symbols.ChildrenModes.None, mode);
    }

    [TestMethod]
    public void TryGetDependencyProperty_WithDefaultConfig_NonMatchingPattern_ReturnsFalse()
    {
        var compilation = CreateCompilation();
        var resolver = CreateResolver(compilation);

        // TestButton has no dependency property field for "Content"
        var type = resolver.GetTypeSymbol("global::QuickMarkup.SourceGen.Test.TestButton");
        Assert.IsNotNull(type);

        var found = resolver.TryGetDependencyProperty(type, "Content", out var _);
        Assert.IsFalse(found, "TestButton.Content should not be a dependency property");
    }
}
