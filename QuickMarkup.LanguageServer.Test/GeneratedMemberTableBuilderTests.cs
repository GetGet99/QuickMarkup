using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Workspace;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class GeneratedMemberTableBuilderTests
{
    static MetadataReference[] GetReferences()
    {
        var serverAssembly = typeof(QmuiWorkspaceService).Assembly;
        return
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            MetadataReference.CreateFromFile(serverAssembly.Location),
        ];
    }

    [TestMethod]
    public void Build_QmuiFileEntry_ProcessesEntry()
    {
        var references = GetReferences();
        var tree = CSharpSyntaxTree.ParseText("namespace TestNs; public class MyComponent { }");
        var compilation = CSharpCompilation.Create("test", [tree], references);
        var qmuiContent = "namespace TestNs;\nclass MyComponent;\n<Button />";
        var fileProvider = new TestFileProvider(qmuiContent);
        var entries = new[]
        {
            new QuickMarkupTypeEntry(
                FullTypeName: "TestNs.MyComponent",
                ShortName: "MyComponent",
                Namespace: "TestNs",
                Usings: "",
                Kind: QuickMarkupDefinitionKind.QmuiFile,
                FilePath: "test.qmui",
                NameSpan: null)
        };

        // This should not throw - the entry should be processed
        var table = GeneratedMemberTableBuilder.Build(entries, fileProvider, compilation);
        Assert.IsNotNull(table);
    }

    [TestMethod]
    public void Build_NoEntries_ReturnsEmptyTable()
    {
        var references = GetReferences();
        var compilation = CSharpCompilation.Create("test", references: references);
        var fileProvider = new TestFileProvider("");

        var table = GeneratedMemberTableBuilder.Build([], fileProvider, compilation);

        var typeSymbol = compilation.GetTypeByMetadataName("System.Object");
        Assert.IsNotNull(typeSymbol);
        Assert.AreEqual(0, table.GetGeneratedPropertyNames(typeSymbol).Count());
    }

    [TestMethod]
    public void Build_CSharpAttributeEntry_ProcessesEntry()
    {
        var references = GetReferences();
        var source = """
            using QuickMarkup.Infra;
            namespace TestNs;
            [QuickMarkup("<root><Button Content=\"Hello\" /></root>")]
            public class MyComponent { }
            """;
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("test", [tree], references);
        var fileProvider = new TestFileProvider("");

        // This should not throw - C# attribute entries should be scanned from compilation
        var table = GeneratedMemberTableBuilder.Build([], fileProvider, compilation);
        Assert.IsNotNull(table);
    }

    class TestFileProvider : IFileProvider
    {
        readonly string _content;
        public TestFileProvider(string content) => _content = content;
        public string ReadAllText(string path) => _content;
        public string[] GetFiles(string directory, string pattern, bool recursive) => [];
        public bool DirectoryExists(string path) => false;
    }
}
