using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;
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
    public async Task Build_QmuiFileEntry_ProcessesEntry()
    {
        var references = GetReferences();
        var tree = CSharpSyntaxTree.ParseText("namespace TestNs; public class MyComponent { }");
        var compilation = CSharpCompilation.Create("test", [tree], references);
        var qmuiContent = "namespace TestNs;\nclass MyComponent;\n<Button />";
        var fileProvider = new TestFileProvider(qmuiContent);
        var docStore = new MockDocumentStore();
        var catalog = new QuickMarkupWorkspaceCatalog();
        catalog.AddOrUpdateQmuiFile("test.qmui", qmuiContent);

        var catalogService = new MockCatalogService(catalog);
        var service = new MemberTableService(catalogService, docStore, fileProvider);
        await service.RebuildAllAsync(compilation);

        var table = service.GetTable();
        Assert.IsNotNull(table);
    }

    [TestMethod]
    public async Task Build_NoEntries_ReturnsEmptyTable()
    {
        var references = GetReferences();
        var compilation = CSharpCompilation.Create("test", references: references);
        var fileProvider = new TestFileProvider("");
        var docStore = new MockDocumentStore();
        var catalog = new QuickMarkupWorkspaceCatalog();

        var catalogService = new MockCatalogService(catalog);
        var service = new MemberTableService(catalogService, docStore, fileProvider);
        await service.RebuildAllAsync(compilation);

        var typeSymbol = compilation.GetTypeByMetadataName("System.Object");
        Assert.IsNotNull(typeSymbol);
        Assert.AreEqual(0, service.GetTable().GetGeneratedPropertyNames(typeSymbol).Count());
    }

    [TestMethod]
    public async Task Build_CSharpAttributeEntry_ProcessesEntry()
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
        var docStore = new MockDocumentStore();
        var catalog = new QuickMarkupWorkspaceCatalog();

        var catalogService = new MockCatalogService(catalog);
        var service = new MemberTableService(catalogService, docStore, fileProvider);
        await service.RebuildAllAsync(compilation);

        Assert.IsNotNull(service.GetTable());
    }

    class TestFileProvider : IFileProvider
    {
        readonly string _content;
        public TestFileProvider(string content) => _content = content;
        public string ReadAllText(string path) => _content;
        public string[] GetFiles(string directory, string pattern, bool recursive) => [];
        public bool DirectoryExists(string path) => false;
    }

    class MockDocumentStore : IQmuiDocumentStore
    {
        public ValueTask<string?> GetTextAsync(string filePath, CancellationToken ct = default)
            => new((string?)null);
        public ValueTask UpdateTextAsync(string filePath, string content, CancellationToken ct = default)
            => default;
        public ValueTask RemoveAsync(string filePath, CancellationToken ct = default)
            => default;
    }

    class MockCatalogService : ICatalogService
    {
        public QuickMarkupWorkspaceCatalog Catalog { get; }
        public MockCatalogService(QuickMarkupWorkspaceCatalog catalog) => Catalog = catalog;
        public Task InitializeAsync(string workspaceRoot, Project project, IFileProvider fileProvider, IQmuiDocumentStore documentStore) => Task.CompletedTask;
        public void OnContentChanged(string filePath, string newContent) { }
        public void OnFileDeleted(string filePath) { }
    }
}
