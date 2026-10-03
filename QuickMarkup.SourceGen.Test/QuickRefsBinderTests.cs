using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using CodeAnalysis = QuickMarkup.CodeAnalysis;
using Helpers = QuickMarkup.CodeAnalysis.Helpers;

namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class QuickRefsBinderTests
{
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
        var source = """
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

    static Helpers.QuickMarkupMergedType Merge(string? markup, params string[] refs)
    {
        QuickMarkupSFC? markupSfc = markup is null ? null : Helpers.QuickMarkupProviderExtension.Parse(markup);
        var refsSfcs = refs.Select(static r => Helpers.QuickMarkupProviderExtension.Parse(r)).ToImmutableArray();
        var target = new Helpers.QuickMarkupTargetContext(
            Namespace: "TestNamespace",
            TypeName: "TestComponent",
            FullTypeName: "TestNamespace.TestComponent",
            FileName: "test.cs",
            AttributeLocation: default,
            AttributeLineSpan: default);
        return new Helpers.QuickMarkupMergedType(target, markupSfc, refsSfcs);
    }

    static CodeAnalysis.QuickMarkupFileAnalysis AnalyzeMerged(Helpers.QuickMarkupMergedType merged)
    {
        return CodeAnalysis.QuickMarkupFileAnalyzer.AnalyzeMerged(
            merged,
            CreateCompilation(),
            CodeAnalysis.QuickMarkupGeneratedMemberTable.Empty,
            failFast: false);
    }

    [TestMethod]
    public void QuickRefsStandalone_RefAndComputed_NoDiagnostics()
    {
        var analysis = AnalyzeMerged(Merge(null, "int Counter = 0;", "int Doubled => `Counter * 2`;"));

        Assert.AreEqual(0, analysis.Diagnostics.Count);
        Assert.AreEqual(2, analysis.RefDeclarations.Count);
    }

    [TestMethod]
    public void QuickRefsStandalone_Provide_ReportsError()
    {
        var analysis = AnalyzeMerged(Merge(null, "provide string Theme = \"dark\";"));

        Assert.IsTrue(
            analysis.Diagnostics.Any(d => d.Message.Contains("requires [QuickMarkup]")),
            $"Expected provide/inject diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void QuickRefsStandalone_Inject_ReportsError()
    {
        var analysis = AnalyzeMerged(Merge(null, "inject string Theme;"));

        Assert.IsTrue(
            analysis.Diagnostics.Any(d => d.Message.Contains("requires [QuickMarkup]")),
            $"Expected provide/inject diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void QuickRefsWithQuickMarkup_Provide_NoProvideInjectDiagnostic()
    {
        var analysis = AnalyzeMerged(Merge("<root />", "public provide string Theme = \"dark\";"));

        Assert.IsFalse(
            analysis.Diagnostics.Any(d => d.Message.Contains("requires [QuickMarkup]")),
            $"Did not expect provide/inject diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void QuickRefsStandalone_Required_ReportsError()
    {
        var analysis = AnalyzeMerged(Merge(null, "required string Title;"));

        Assert.IsTrue(
            analysis.Diagnostics.Any(d => d.Message.Contains("required in [QuickRefs]")),
            $"Expected required diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void QuickRefsWithQuickMarkup_Required_NoRequiredDiagnostic()
    {
        var analysis = AnalyzeMerged(Merge("<root />", "required string Title;"));

        Assert.IsFalse(
            analysis.Diagnostics.Any(d => d.Message.Contains("required in [QuickRefs]")),
            $"Did not expect required diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void QuickRefsFragment_WithMarkupTags_Throws()
    {
        var merged = Merge(null, "<root />");

        try
        {
            AnalyzeMerged(merged);
            Assert.Fail("Expected InvalidOperationException for markup tags in [QuickRefs]");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [TestMethod]
    public void QuickRefsFragment_DuplicateName_ReportsError()
    {
        var analysis = AnalyzeMerged(Merge(null, "int Value = 1;", "int Value = 2;"));

        Assert.IsTrue(
            analysis.Diagnostics.Any(d => d.Message.Contains("Duplicate reference")),
            $"Expected duplicate diagnostic, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
        Assert.AreEqual(2, analysis.RefDeclarations.Count);
    }
}
