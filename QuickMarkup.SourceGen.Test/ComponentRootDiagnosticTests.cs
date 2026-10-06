using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CodeAnalysis = QuickMarkup.CodeAnalysis;

namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class ComponentRootDiagnosticTests
{
    const string InfraSources = """
        namespace QuickMarkup.Infra
        {
            public interface IQuickMarkupComponent<T>
            {
                T MarkupNode { get; }
            }

            public interface IQuickMarkupFragmentComponent<T>
            {
            }
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

    static CSharpCompilation CreateCompilation(string componentDecl)
    {
        var source = $"""
            {InfraSources}
            {TestTypeSources}
            namespace TestNamespace;
            {componentDecl}
            """;

        var tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            GetCoreReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    static CodeAnalysis.QuickMarkupFileAnalysis Analyze(string componentDecl, string componentName, string body)
    {
        var compilation = CreateCompilation(componentDecl);
        var qmui = $"""
            using QuickMarkup.SourceGen.Test;
            namespace TestNamespace;
            class {componentName};
            bool Flag = true;
            <root>
                {body}
            </root>
            """;
        return CodeAnalysis.QuickMarkupFileAnalyzer.Analyze(
            qmui,
            "test.qmui",
            "TestNamespace",
            compilation,
            CodeAnalysis.QuickMarkupGeneratedMemberTable.Empty,
            failFast: false);
    }

    const string SingleComponentDecl = """
        public class TestComponent : QuickMarkup.Infra.IQuickMarkupComponent<QuickMarkup.SourceGen.Test.TestText>
        {
            public QuickMarkup.SourceGen.Test.TestText MarkupNode => throw new System.NotImplementedException();
        }
        """;

    const string FragmentComponentDecl = """
        public class TestFragmentComponent : QuickMarkup.Infra.IQuickMarkupFragmentComponent<QuickMarkup.SourceGen.Test.TestText>
        {
        }
        """;

    static bool HasDiagnostic(CodeAnalysis.QuickMarkupFileAnalysis analysis, string message)
        => analysis.Diagnostics.Any(d => d.Message.Contains(message));

    [TestMethod]
    public void SingleRoot_IfElse_ReportsError()
    {
        var analysis = Analyze(
            SingleComponentDecl,
            "TestComponent",
            """if (`Flag`) { <TestText /> } else { <TestText /> }""");

        Assert.IsTrue(
            HasDiagnostic(analysis, "if is not allowed here"),
            $"Expected 'if is not allowed here' error, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void SingleRoot_Await_ReportsError()
    {
        var analysis = Analyze(
            SingleComponentDecl,
            "TestComponent",
            """await `System.Threading.Tasks.Task.FromResult(1)` with { <TestText /> } then (x) { <TestText /> }""");

        Assert.IsTrue(
            HasDiagnostic(analysis, "await is not allowed here"),
            $"Expected 'await is not allowed here' error, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void SingleRoot_NestedFragmentsIfElse_ReportsError()
    {
        var analysis = Analyze(
            SingleComponentDecl,
            "TestComponent",
            """{ { { if (`Flag`) { <TestText /> } else { <TestText /> } } } }""");

        Assert.IsTrue(
            HasDiagnostic(analysis, "if is not allowed here"),
            $"Expected 'if is not allowed here' error, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void SingleRoot_PlainElement_NoDynamicRootError()
    {
        var analysis = Analyze(
            SingleComponentDecl,
            "TestComponent",
            """<TestText />""");

        Assert.IsFalse(
            HasDiagnostic(analysis, "is not allowed here"),
            $"Did not expect fixed-element error, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }

    [TestMethod]
    public void FragmentRoot_IfElse_NoDynamicRootError()
    {
        var analysis = Analyze(
            FragmentComponentDecl,
            "TestFragmentComponent",
            """if (`Flag`) { <TestText /> } else { <TestText /> }""");

        Assert.IsFalse(
            HasDiagnostic(analysis, "is not allowed here"),
            $"Did not expect fixed-element error, got: {string.Join("; ", analysis.Diagnostics.Select(d => d.Message))}");
    }
}
