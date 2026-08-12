using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CodeAnalysis = QuickMarkup.CodeAnalysis;

namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class TemplateBinderDiagnosticTests
{
    const string AttributeDefinitions = """
        using System;
        namespace QuickMarkup.Infra
        {
            [AttributeUsage(AttributeTargets.Property)]
            public class QuickMarkupChildrenAttribute : Attribute;

            [AttributeUsage(AttributeTargets.Property)]
            public class QuickMarkupContentAttribute : Attribute;
        }
        """;

    const string TestTypeSources = """
        namespace QuickMarkup.SourceGen.Test
        {
            public class TestElement { }

            public class TestText : TestElement
            {
                public string? Text { get; set; }
            }

            public class TestItem
            {
                public string? Text { get; set; }
            }

            public class TestDataTemplate { }

            public class TestItemsControl : TestElement
            {
                public TestDataTemplate? ItemTemplate { get; set; }
            }
        }
        """;

    static MetadataReference[] GetCoreReferences()
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

    static CSharpCompilation CreateCompilation()
    {
        var source = $"""
            {AttributeDefinitions}
            {TestTypeSources}
            namespace TestNamespace;
            public class TestComponent;
            """;

        var tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            GetCoreReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    static CodeAnalysis.QuickMarkupFileAnalysis Analyze(string qmuiBody)
    {
        var compilation = CreateCompilation();
        var qmui = $"""
            using QuickMarkup.SourceGen.Test;
            namespace TestNamespace;
            class TestComponent;
            <root>
                {qmuiBody}
            </root>
            """;
        var frameworkConfig = CodeAnalysis.FrameworkConfiguration.Default with
        {
            DataTemplateFactoryFullName = "global::QuickMarkup.SourceGen.Test.TestDataTemplateFactory",
        };
        return CodeAnalysis.QuickMarkupFileAnalyzer.Analyze(
            qmui,
            "test.qmui",
            "TestNamespace",
            compilation,
            CodeAnalysis.QuickMarkupGeneratedMemberTable.Empty,
            frameworkConfig,
            failFast: false);
    }

    [TestMethod]
    public void TemplateBody_IfBlock_ReportsDiagnostic()
    {
        var analysis = Analyze("""
            <TestItemsControl ItemTemplate=template (TestItem? item) { if (`true`) { <TestText /> } } />
            """);

        Assert.IsTrue(
            analysis.Diagnostics.Any(d => d.Message.Contains("if is not allowed here")),
            $"Expected 'if is not allowed here' diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void TemplateBody_PlainElement_NoIfDiagnostic()
    {
        var analysis = Analyze("""
            <TestItemsControl ItemTemplate=template (TestItem? item) { <TestText Text=`item?.Text` /> } />
            """);

        Assert.IsFalse(
            analysis.Diagnostics.Any(d => d.Message.Contains("if is not allowed here")),
            $"Did not expect 'if is not allowed here' diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void TemplateBody_NestedFragmentsWithIf_ReportsDiagnostic()
    {
        var analysis = Analyze("""
            <TestItemsControl ItemTemplate=template (TestItem? item) { { { if (`true`) { <TestText /> } } } } />
            """);

        Assert.IsTrue(
            analysis.Diagnostics.Any(d => d.Message.Contains("if is not allowed here")),
            $"Expected 'if is not allowed here' diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void TemplateBody_NestedFragmentsWithPlainElement_NoDiagnostic()
    {
        var analysis = Analyze("""
            <TestItemsControl ItemTemplate=template (TestItem? item) { { <TestText Text=`item?.Text` /> } } />
            """);

        Assert.IsFalse(
            analysis.Diagnostics.Any(d => d.Message.Contains("single fixed element")),
            $"Did not expect single-fixed-element diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void TemplateBody_BareElementWithoutFragment_NoDiagnostic()
    {
        var analysis = Analyze("""
            <TestItemsControl ItemTemplate=template (TestItem? item) <TestText Text=`item?.Text` /> />
            """);

        Assert.IsFalse(
            analysis.Diagnostics.Any(d => d.Message.Contains("single fixed element")),
            $"Did not expect single-fixed-element diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void Template_NoFactoryConfigured_ReportsDiagnostic()
    {
        var compilation = CreateCompilation();
        var qmui = """
            using QuickMarkup.SourceGen.Test;
            namespace TestNamespace;
            class TestComponent;
            <root>
                <TestItemsControl ItemTemplate=template (TestItem? item) { <TestText /> } />
            </root>
            """;
        var analysis = CodeAnalysis.QuickMarkupFileAnalyzer.Analyze(
            qmui,
            "test.qmui",
            "TestNamespace",
            compilation,
            CodeAnalysis.QuickMarkupGeneratedMemberTable.Empty,
            frameworkConfiguration: CodeAnalysis.FrameworkConfiguration.Default,
            failFast: false);

        Assert.IsTrue(
            analysis.Diagnostics.Any(d => d.Message.Contains("no data template factory")),
            $"Expected 'no data template factory' diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void TemplateBody_ElementWithConstructorArgs_ReportsDiagnostic()
    {
        var analysis = Analyze("""
            <TestItemsControl ItemTemplate=template (TestItem? item) { <TestText(1) /> } />
            """);

        Assert.IsTrue(
            analysis.Diagnostics.Any(d => d.Message.Contains("cannot have constructor arguments")),
            $"Expected 'cannot have constructor arguments' diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }
}
