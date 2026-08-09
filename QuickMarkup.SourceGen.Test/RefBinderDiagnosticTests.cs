using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CodeAnalysis = QuickMarkup.CodeAnalysis;

namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class RefBinderDiagnosticTests
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

    static CodeAnalysis.QuickMarkupFileAnalysis Analyze(string refs)
    {
        var compilation = CreateCompilation();
        var qmui = $"""
            namespace TestNamespace;
            class TestComponent;
            {refs}
            <root />
            """;
        return CodeAnalysis.QuickMarkupFileAnalyzer.Analyze(
            qmui,
            "test.qmui",
            "TestNamespace",
            compilation,
            CodeAnalysis.QuickMarkupGeneratedMemberTable.Empty,
            failFast: false);
    }

    static bool HasMissingDefaultWarning(CodeAnalysis.QuickMarkupFileAnalysis analysis)
        => analysis.Diagnostics.Any(d => d.Message.Contains("no default value"));

    [TestMethod]
    public void Ref_NonNullableReferenceTypeNoDefault_ReportsWarning()
    {
        var analysis = Analyze("string Text;");

        Assert.IsTrue(
            HasMissingDefaultWarning(analysis),
            $"Expected 'no default value' warning, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void Ref_NullableReferenceTypeNoDefault_NoWarning()
    {
        var analysis = Analyze("string? Placeholder;");

        Assert.IsFalse(
            HasMissingDefaultWarning(analysis),
            $"Did not expect 'no default value' warning, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void Ref_NonNullableReferenceTypeWithDefault_NoWarning()
    {
        var analysis = Analyze("string Header = \"Default Header\";");

        Assert.IsFalse(
            HasMissingDefaultWarning(analysis),
            $"Did not expect 'no default value' warning, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void Ref_ValueTypeNoDefault_NoWarning()
    {
        var analysis = Analyze("int Counter;");

        Assert.IsFalse(
            HasMissingDefaultWarning(analysis),
            $"Did not expect 'no default value' warning, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void Ref_NullableValueTypeNoDefault_NoWarning()
    {
        var analysis = Analyze("int? Counter;");

        Assert.IsFalse(
            HasMissingDefaultWarning(analysis),
            $"Did not expect 'no default value' warning, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void ComputedRef_NonNullableReferenceType_NoWarning()
    {
        var analysis = Analyze("string FullName => `Foo`;");

        Assert.IsFalse(
            HasMissingDefaultWarning(analysis),
            $"Did not expect 'no default value' warning, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }
}
