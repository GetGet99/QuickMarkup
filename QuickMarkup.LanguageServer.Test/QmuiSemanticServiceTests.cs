using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.SemanticService;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class QmuiSemanticServiceTests
{
    [TestMethod]
    public async Task TryResolveTagAtPositionAsync_EmptyContent_ReturnsNull()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        var result = await service.TryResolveTagAtPositionAsync(
            "test.qmui",
            "",
            0,
            0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TryResolveTagAtPositionAsync_NullCompilation_ReturnsNull()
    {
        var workspace = new MockWorkspaceManager { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        var result = await service.TryResolveTagAtPositionAsync(
            "test.qmui",
            "<Button />",
            0,
            1);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TryResolveTagAtPositionAsync_InvalidMarkup_ReturnsNull()
    {
        var compilation = CSharpCompilation.Create("test");
        var workspace = new MockWorkspaceManager { Compilation = compilation };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        var result = await service.TryResolveTagAtPositionAsync(
            "test.qmui",
            "<<<invalid>>>",
            0,
            0);

        Assert.IsNull(result);
    }
}

internal class MockWorkspaceManager : IRoslynWorkspaceManager
{
    public bool IsLoaded { get; set; }
    public string? CurrentProjectPath { get; set; }
    public Compilation? Compilation { get; set; }
    public event Action? CompilationChanged;
    public Task<bool> InitializeAsync(string workspaceRoot) => Task.FromResult(true);
    public Task<bool> TryLoadAsync(string projectPath) => Task.FromResult(true);
    public Task<bool> EnsureProjectForFileAsync(string qmuiFilePath) => Task.FromResult(true);
}

internal class MockFileProvider : IFileProvider
{
    public string ReadAllText(string path) => "";
    public string[] GetFiles(string directory, string pattern, bool recursive) => [];
    public bool DirectoryExists(string path) => false;
}
