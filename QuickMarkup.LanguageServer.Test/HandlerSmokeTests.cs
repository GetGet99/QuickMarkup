using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Diagnostics;
using QuickMarkup.LanguageServer.Handlers;
using QuickMarkup.LanguageServer.Navigation;
using QuickMarkup.LanguageServer.SemanticService;
using QuickMarkup.LanguageServer.Workspace;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class HandlerSmokeTests
{
    [TestMethod]
    public void HandlerTypes_ExistInAssembly()
    {
        Assert.IsNotNull(typeof(QmuiDidOpenHandler));
        Assert.IsNotNull(typeof(QmuiDidChangeHandler));
        Assert.IsNotNull(typeof(QmuiDidCloseHandler));
        Assert.IsNotNull(typeof(QmuiHoverHandler));
        Assert.IsNotNull(typeof(QmuiDefinitionHandler));
    }

    [TestMethod]
    public void ServiceTypes_ExistInAssembly()
    {
        Assert.IsNotNull(typeof(QmuiDiagnosticService));
        Assert.IsNotNull(typeof(QmuiSemanticService));
        Assert.IsNotNull(typeof(RoslynWorkspaceManager));
        Assert.IsNotNull(typeof(AdhocWorkspaceManager));
        Assert.IsNotNull(typeof(QmuiDocumentStore));
        Assert.IsNotNull(typeof(QuickMarkupWorkspaceCatalog));
        Assert.IsNotNull(typeof(MarkupCursorResolver));
        Assert.IsNotNull(typeof(SymbolLocationResolver));
    }

    [TestMethod]
    public void ContractInterfaces_Available()
    {
        Assert.IsNotNull(typeof(IRoslynWorkspaceManager));
        Assert.IsNotNull(typeof(IQmuiDiagnosticService));
        Assert.IsNotNull(typeof(IQmuiDocumentStore));
        Assert.IsNotNull(typeof(IQmuiSemanticService));
    }

    [TestMethod]
    public void ServiceTypes_ImplementContracts()
    {
        Assert.IsTrue(typeof(QmuiDiagnosticService).IsAssignableTo(typeof(IQmuiDiagnosticService)));
        Assert.IsTrue(typeof(QmuiSemanticService).IsAssignableTo(typeof(IQmuiSemanticService)));
        Assert.IsTrue(typeof(QmuiDocumentStore).IsAssignableTo(typeof(IQmuiDocumentStore)));
        Assert.IsTrue(typeof(RoslynWorkspaceManager).IsAssignableTo(typeof(IRoslynWorkspaceManager)));
        Assert.IsTrue(typeof(AdhocWorkspaceManager).IsAssignableTo(typeof(IRoslynWorkspaceManager)));
    }

    [TestMethod]
    public void DiagnosticService_CanBeConstructedWithMockWorkspace()
    {
        var workspace = new TestMockWorkspaceManager { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new TestMockFileProvider();
        var service = new QmuiDiagnosticService(workspace, catalog, fileProvider);
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void SemanticService_CanBeConstructed()
    {
        var workspace = new TestMockWorkspaceManager { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new TestMockFileProvider();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void DocumentStore_CanBeConstructed()
    {
        var store = new QmuiDocumentStore();
        Assert.IsNotNull(store);
    }

    [TestMethod]
    public void CursorResolver_CanBeConstructed()
    {
        var workspace = new TestMockWorkspaceManager { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new TestMockFileProvider();
        var semanticService = new QmuiSemanticService(workspace, catalog, fileProvider);
        var resolver = new MarkupCursorResolver(semanticService);
        Assert.IsNotNull(resolver);
    }

    [TestMethod]
    public void LocationResolver_CanBeConstructed()
    {
        var catalog = new QuickMarkupWorkspaceCatalog();
        var resolver = new SymbolLocationResolver(catalog);
        Assert.IsNotNull(resolver);
    }

    [TestMethod]
    public void ProjectFinder_NoRoot_ReturnsNull()
    {
        Assert.IsNull(ProjectFinder.FindDefaultProject(null));
        Assert.IsNull(ProjectFinder.FindDefaultProject(""));
        Assert.IsNull(ProjectFinder.FindDefaultProject("C:\\NonExistentDirectory_QuickMarkup_Test"));
    }
}

internal class TestMockWorkspaceManager : IRoslynWorkspaceManager
{
    public bool IsLoaded { get; set; }
    public string? CurrentProjectPath { get; set; }
    public Compilation? Compilation { get; set; }
    public Task<bool> InitializeAsync(string workspaceRoot) => Task.FromResult(true);
    public Task<bool> TryLoadAsync(string projectPath) => Task.FromResult(true);
    public Task<bool> EnsureProjectForFileAsync(string qmuiFilePath) => Task.FromResult(true);
}

internal class TestMockFileProvider : IFileProvider
{
    public string ReadAllText(string path) => "";
    public string[] GetFiles(string directory, string pattern, bool recursive) => [];
    public bool DirectoryExists(string path) => false;
}
