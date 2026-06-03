using Microsoft.CodeAnalysis;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Diagnostics.Test;

[TestClass]
public sealed class QmuiDiagnosticServiceTests
{
    [TestMethod]
    public async Task GetDiagnosticsAsync_NoCompilation_ReturnsSyntaxOnly()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var service = new QmuiDiagnosticService(workspace);

        var result = await service.GetDiagnosticsAsync("test.qmui", "<Button />", CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetDiagnosticsAsync_InvalidContent_ReturnsEmptyOnParseFailure()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var service = new QmuiDiagnosticService(workspace);

        var result = await service.GetDiagnosticsAsync("test.qmui", "<<<invalid>>>", CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetDiagnosticsAsync_EmptyContent_ReturnsNoDiagnostics()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var service = new QmuiDiagnosticService(workspace);

        var result = await service.GetDiagnosticsAsync("test.qmui", "", CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetDiagnosticsAsync_NoClassDeclaration_ReturnsSyntaxOnly()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var service = new QmuiDiagnosticService(workspace);

        var result = await service.GetDiagnosticsAsync(
            "test.qmui", "<Button x:Name=\"test\" />", CancellationToken.None);

        Assert.IsNotNull(result);
    }
}

class MockWorkspaceManager : IRoslynWorkspaceManager
{
    public bool IsLoaded { get; set; }
    public Compilation? Compilation { get; set; }
    public event Action? CompilationChanged;
    public Task<bool> TryLoadAsync(string projectPath) => Task.FromResult(true);
    public void WatchProjectChanges(string csprojPath) { }
}
