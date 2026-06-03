using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Diagnostics;
using QuickMarkup.LanguageServer.Diagnostics.Test;
using QuickMarkup.LanguageServer.Handlers;
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
    }

    [TestMethod]
    public void ServiceTypes_ExistInAssembly()
    {
        Assert.IsNotNull(typeof(QmuiDiagnosticService));
        Assert.IsNotNull(typeof(RoslynWorkspaceManager));
        Assert.IsNotNull(typeof(AdhocWorkspaceManager));
        Assert.IsNotNull(typeof(ProjectFinder));
    }

    [TestMethod]
    public void ContractInterfaces_Available()
    {
        Assert.IsNotNull(typeof(IRoslynWorkspaceManager));
        Assert.IsNotNull(typeof(IQmuiDiagnosticService));
    }

    [TestMethod]
    public void ServiceTypes_ImplementContracts()
    {
        Assert.IsTrue(typeof(QmuiDiagnosticService).IsAssignableTo(typeof(IQmuiDiagnosticService)));
        Assert.IsTrue(typeof(RoslynWorkspaceManager).IsAssignableTo(typeof(IRoslynWorkspaceManager)));
        Assert.IsTrue(typeof(AdhocWorkspaceManager).IsAssignableTo(typeof(IRoslynWorkspaceManager)));
    }

    [TestMethod]
    public void DiagnosticService_CanBeConstructedWithMockWorkspace()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var service = new QmuiDiagnosticService(workspace);
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void ProjectFinder_NoRoot_ReturnsNull()
    {
        Assert.IsNull(ProjectFinder.FindCsproj(null));
        Assert.IsNull(ProjectFinder.FindCsproj(""));
        Assert.IsNull(ProjectFinder.FindCsproj("C:\\NonExistentDirectory_QuickMarkup_Test"));
    }
}
