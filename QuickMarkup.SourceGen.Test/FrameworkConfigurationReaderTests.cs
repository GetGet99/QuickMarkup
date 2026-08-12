using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using CodeAnalysis = QuickMarkup.CodeAnalysis;
using Symbols = QuickMarkup.Language.Symbols;

namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class FrameworkConfigurationReaderTests
{
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

        return [.. refs];
    }

    /// <summary>
    /// Creates an in-memory <see cref="CSharpCompilation"/> for testing.
    /// Assembly attributes come first (before all other code).
    /// Uses traditional constructors for Roslyn 4.6.0 compatibility.
    /// </summary>
    static CSharpCompilation CreateCompilation(
        string source,
        bool addAssemblyAttribute = false,
        string? frameworkClassSource = null)
    {
        // Assembly attribute MUST come first in the file, before any declarations.
        var assemblyAttrCode = addAssemblyAttribute
            ? "[assembly: QuickMarkup.Infra.QuickMarkupFramework(typeof(MyFramework))]"
            : "";

        var frameworkCode = frameworkClassSource ?? "";

        // Build the full source. The assembly attribute is at the very top.
        // Attribute definitions use traditional constructors for Roslyn 4.6.0 compat.
        var attrsUsingSystem = @"using System;
";
        var attrDefs = @"
namespace QuickMarkup.Infra
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class QuickMarkupFrameworkAttribute : Attribute
    {
        public QuickMarkupFrameworkAttribute(Type frameworkType) { FrameworkType = frameworkType; }
        public Type FrameworkType { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class QuickMarkupChildrenPropertyAttribute : Attribute
    {
        public QuickMarkupChildrenPropertyAttribute(string propertyName) { PropertyName = propertyName; }
        public QuickMarkupChildrenPropertyAttribute(Type type, string propertyName) : this(propertyName) { Type = type; }
        public Type? Type;
        public string PropertyName { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class QuickMarkupContentPropertyAttribute : Attribute
    {
        public QuickMarkupContentPropertyAttribute(string propertyName) { PropertyName = propertyName; }
        public QuickMarkupContentPropertyAttribute(Type type, string propertyName) : this(propertyName) { Type = type; }
        public Type? Type;
        public string PropertyName { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class QuickMarkupExternalContentPropertyAttribute : Attribute
    {
        public QuickMarkupExternalContentPropertyAttribute(Type externalAttributeType) { ExternalAttributeType = externalAttributeType; }
        public Type ExternalAttributeType { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class QuickMarkupDependencyPropertyAttribute : Attribute
    {
        public QuickMarkupDependencyPropertyAttribute(Type typeName, string suffix) { TypeName = typeName; Suffix = suffix; }
        public Type TypeName { get; }
        public string Suffix { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class QuickMarkupAttachedPropertyAttribute : Attribute
    {
        public QuickMarkupAttachedPropertyAttribute(string setPrefix) { SetPrefix = setPrefix; }
        public string SetPrefix { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class QuickMarkupDataTemplateFactoryAttribute : Attribute
    {
        public QuickMarkupDataTemplateFactoryAttribute(Type factoryType) { FactoryType = factoryType; }
        public Type FactoryType { get; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class QuickMarkupChildrenAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public class QuickMarkupContentAttribute : Attribute { }
}";

        // Build final source: assembly attr at top, then namespaces, then code
        var allSource = attrsUsingSystem + assemblyAttrCode + "\n" + attrDefs + "\n" + frameworkCode + "\n" + source;

        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(allSource, options);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            GetCoreReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation;
    }

    // ---------------------------------------------------------------
    // Reader Tests
    // ---------------------------------------------------------------

    [TestMethod]
    public void ReadFromCompilation_NoAssemblyAttribute_ReturnsNull()
    {
        var compilation = CreateCompilation("class Foo { }", addAssemblyAttribute: false);
        var result = CodeAnalysis.FrameworkConfigurationReader.ReadFromCompilation(compilation);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ReadFromCompilation_WithChildrenPropertyDefaults_ReturnsCorrectConfig()
    {
        var frameworkClass = @"
[QuickMarkup.Infra.QuickMarkupChildrenProperty(""Children"")]
[QuickMarkup.Infra.QuickMarkupContentProperty(""Content"")]
public class MyFramework
{
    public static readonly int Marker;
}";

        var compilation = CreateCompilation(
            "class Foo { }",
            addAssemblyAttribute: true,
            frameworkClassSource: frameworkClass);

        var config = CodeAnalysis.FrameworkConfigurationReader.ReadFromCompilation(compilation);
        Assert.IsNotNull(config);

        Assert.HasCount(2, config.DefaultContentPropertyNames);

        var childrenRule = config.DefaultContentPropertyNames[0];
        Assert.AreEqual("Children", childrenRule.PropertyName);
        Assert.AreEqual(Symbols.ChildrenModes.Add, childrenRule.Mode);

        var contentRule = config.DefaultContentPropertyNames[1];
        Assert.AreEqual("Content", contentRule.PropertyName);
        Assert.AreEqual(Symbols.ChildrenModes.Assignment, contentRule.Mode);
    }

    [TestMethod]
    public void ReadFromCompilation_WithTypeSpecificOverrides_ReturnsOverrides()
    {
        var frameworkClass = @"
[QuickMarkup.Infra.QuickMarkupChildrenProperty(typeof(SomeType), ""CustomChildren"")]
public class MyFramework
{
    public static readonly int Marker;
}

public class SomeType { }
";

        var compilation = CreateCompilation(
            "",
            addAssemblyAttribute: true,
            frameworkClassSource: frameworkClass);

        var config = CodeAnalysis.FrameworkConfigurationReader.ReadFromCompilation(compilation);
        Assert.IsNotNull(config);

        Assert.HasCount(1, config.TypeSpecificOverrides);

        var someTypeFullName = "global::SomeType";
        Assert.IsTrue(config.TypeSpecificOverrides.ContainsKey(someTypeFullName),
            $"Expected override for '{someTypeFullName}'");

        var overrideRule = config.TypeSpecificOverrides[someTypeFullName];
        Assert.AreEqual("CustomChildren", overrideRule.PropertyName);
        Assert.AreEqual(Symbols.ChildrenModes.Add, overrideRule.Mode);
    }

    [TestMethod]
    public void ReadFromCompilation_WithExternalContentProperty_ReturnsConfig()
    {
        var frameworkClass = @"
[QuickMarkup.Infra.QuickMarkupExternalContentProperty(typeof(SomeExternalAttribute))]
public class MyFramework
{
    public static readonly int Marker;
}

public class SomeExternalAttribute : Attribute { }
";

        var compilation = CreateCompilation(
            "",
            addAssemblyAttribute: true,
            frameworkClassSource: frameworkClass);

        var config = CodeAnalysis.FrameworkConfigurationReader.ReadFromCompilation(compilation);
        Assert.IsNotNull(config);

        Assert.HasCount(1, config.ExternalContentPropertyAttributeFullNames);
        Assert.AreEqual("global::SomeExternalAttribute", config.ExternalContentPropertyAttributeFullNames[0]);
    }

    [TestMethod]
    public void ReadFromCompilation_WithDependencyPropertyConfig_ReturnsCorrectConfig()
    {
        var frameworkClass = @"
[QuickMarkup.Infra.QuickMarkupDependencyProperty(typeof(CustomDependencyProperty), ""Prop"")]
public class MyFramework
{
    public static readonly int Marker;
}

public class CustomDependencyProperty { }
";

        var compilation = CreateCompilation(
            "",
            addAssemblyAttribute: true,
            frameworkClassSource: frameworkClass);

        var config = CodeAnalysis.FrameworkConfigurationReader.ReadFromCompilation(compilation);
        Assert.IsNotNull(config);

        Assert.AreEqual("CustomDependencyProperty", config.DependencyProperty.TypeName);
        Assert.AreEqual("Prop", config.DependencyProperty.Suffix);
    }

    [TestMethod]
    public void ReadFromCompilation_WithAttachedPropertyConfig_ReturnsCorrectConfig()
    {
        var frameworkClass = @"
[QuickMarkup.Infra.QuickMarkupAttachedProperty(""Attach"")]
public class MyFramework
{
    public static readonly int Marker;
}
";

        var compilation = CreateCompilation(
            "",
            addAssemblyAttribute: true,
            frameworkClassSource: frameworkClass);

        var config = CodeAnalysis.FrameworkConfigurationReader.ReadFromCompilation(compilation);
        Assert.IsNotNull(config);

        Assert.AreEqual("Attach", config.AttachedProperty.SetPrefix);
    }

    [TestMethod]
    public void ReadFromCompilation_NoAttributeOnFrameworkType_UsesDefaults()
    {
        var frameworkClass = @"
public class MyFramework
{
    public static readonly int Marker;
}
";

        var compilation = CreateCompilation(
            "",
            addAssemblyAttribute: true,
            frameworkClassSource: frameworkClass);

        var config = CodeAnalysis.FrameworkConfigurationReader.ReadFromCompilation(compilation);
        Assert.IsNotNull(config);

        Assert.HasCount(2, config.DefaultContentPropertyNames);
        Assert.AreEqual("Children", config.DefaultContentPropertyNames[0].PropertyName);
        Assert.AreEqual(Symbols.ChildrenModes.Add, config.DefaultContentPropertyNames[0].Mode);
        Assert.AreEqual("Content", config.DefaultContentPropertyNames[1].PropertyName);
        Assert.AreEqual(Symbols.ChildrenModes.Assignment, config.DefaultContentPropertyNames[1].Mode);

        Assert.AreEqual("DependencyProperty", config.DependencyProperty.TypeName);
        Assert.AreEqual("Property", config.DependencyProperty.Suffix);

        Assert.AreEqual("Set", config.AttachedProperty.SetPrefix);

        Assert.IsNull(config.DataTemplateFactoryFullName);
    }

    [TestMethod]
    public void ReadFromCompilation_WithDataTemplateFactoryConfig_ReturnsCorrectConfig()
    {
        var frameworkClass = @"
[QuickMarkup.Infra.QuickMarkupDataTemplateFactory(typeof(MyTemplateFactory))]
public class MyFramework
{
    public static readonly int Marker;
}

public class MyTemplateFactory
{
    public static DataTemplate CreateDataTemplate<T>(Action<T> postprocess) where T : object => null;
}
";

        var compilation = CreateCompilation(
            "",
            addAssemblyAttribute: true,
            frameworkClassSource: frameworkClass);

        var config = CodeAnalysis.FrameworkConfigurationReader.ReadFromCompilation(compilation);
        Assert.IsNotNull(config);

        Assert.AreEqual("global::MyTemplateFactory", config.DataTemplateFactoryFullName);
    }
}
