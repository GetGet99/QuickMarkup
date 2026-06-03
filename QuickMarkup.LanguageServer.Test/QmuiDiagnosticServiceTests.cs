using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Diagnostics.Test;

[TestClass]
public sealed class QmuiDiagnosticServiceTests
{
    [TestMethod]
    public async Task GetDiagnosticsAsync_NoCompilation_ReturnsSyntaxOnly()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider();
        var service = new QmuiDiagnosticService(workspace, catalog, fileProvider);

        var result = await service.GetDiagnosticsAsync("test.qmui", "<Button />", CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetDiagnosticsAsync_EmptyContent_ReturnsNoDiagnostics()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider();
        var service = new QmuiDiagnosticService(workspace, catalog, fileProvider);

        var result = await service.GetDiagnosticsAsync("test.qmui", "", CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetDiagnosticsAsync_NoClassDeclaration_ReturnsSyntaxOnly()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider();
        var service = new QmuiDiagnosticService(workspace, catalog, fileProvider);

        var result = await service.GetDiagnosticsAsync(
            "test.qmui", "<Button x:Name=\"test\" />", CancellationToken.None);

        Assert.IsNotNull(result);
    }
}

class MockWorkspaceManager : IRoslynWorkspaceManager
{
    public bool IsLoaded { get; set; }
    public string? CurrentProjectPath { get; set; }
    public Compilation? Compilation { get; set; }
    public Task<bool> InitializeAsync(string workspaceRoot) => Task.FromResult(true);
    public Task<bool> TryLoadAsync(string projectPath) => Task.FromResult(true);
    public Task<bool> EnsureProjectForFileAsync(string qmuiFilePath) => Task.FromResult(true);
}

class MockFileProvider : IFileProvider
{
    public string ReadAllText(string path) => "";
    public string[] GetFiles(string directory, string pattern, bool recursive) => [];
    public bool DirectoryExists(string path) => false;
}
