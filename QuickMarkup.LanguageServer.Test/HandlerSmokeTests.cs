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
        Assert.IsNotNull(typeof(QmuiWorkspaceService));
        Assert.IsNotNull(typeof(QmuiDocumentStore));
        Assert.IsNotNull(typeof(MarkupCursorResolver));
        Assert.IsNotNull(typeof(SymbolLocationResolver));
    }

    [TestMethod]
    public void ContractInterfaces_Available()
    {
        Assert.IsNotNull(typeof(IQmuiWorkspaceService));
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
        Assert.IsTrue(typeof(QmuiWorkspaceService).IsAssignableTo(typeof(IQmuiWorkspaceService)));
    }

    [TestMethod]
    public void DiagnosticService_CanBeConstructedWithMockWorkspace()
    {
        var workspace = new MockQmuiWorkspaceService { Compilation = null };
        var service = new QmuiDiagnosticService(workspace);
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void SemanticService_CanBeConstructed()
    {
        var workspace = new MockQmuiWorkspaceService { Compilation = null };
        var service = new QmuiSemanticService(workspace);
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
        var workspace = new MockQmuiWorkspaceService { Compilation = null };
        var semanticService = new QmuiSemanticService(workspace);
        var resolver = new MarkupCursorResolver(semanticService);
        Assert.IsNotNull(resolver);
    }

    [TestMethod]
    public void LocationResolver_CanBeConstructed()
    {
        var workspace = new MockQmuiWorkspaceService();
        var resolver = new SymbolLocationResolver(workspace, null!);
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
