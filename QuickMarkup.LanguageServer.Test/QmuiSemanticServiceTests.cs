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
    public async Task TryResolveAtPositionAsync_EmptyContent_ReturnsNull()
    {
        var workspace = new MockWorkspaceManager2 { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider2();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        var result = await service.TryResolveAtPositionAsync(
            "test.qmui",
            "",
            0,
            0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TryResolveAtPositionAsync_NullCompilation_ReturnsNull()
    {
        var workspace = new MockWorkspaceManager2 { Compilation = null };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider2();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        var result = await service.TryResolveAtPositionAsync(
            "test.qmui",
            "<Button />",
            0,
            1);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TryResolveAtPositionAsync_InvalidMarkup_ReturnsNull()
    {
        var compilation = CSharpCompilation.Create("test");
        var workspace = new MockWorkspaceManager2 { Compilation = compilation };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider2();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        var result = await service.TryResolveAtPositionAsync(
            "test.qmui",
            "<<<invalid>>>",
            0,
            0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TryResolveAtPositionAsync_NothingAtPosition_ReturnsNull()
    {
        var compilation = CSharpCompilation.Create("test");
        var workspace = new MockWorkspaceManager2 { Compilation = compilation };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider2();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        // Position 0 is at the very start of the line, before any content
        var result = await service.TryResolveAtPositionAsync(
            "test.qmui",
            "  <Button />",
            0,
            0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TryResolveAtPositionAsync_RefDeclaration_ReturnsPropertyResult()
    {
        var compilation = CSharpCompilation.Create("test");
        var workspace = new MockWorkspaceManager2 { Compilation = compilation };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider2();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        // "class TestComponent;\nstring Name = \"default\";"
        // Position on line 1, char 7 is where "Name" starts (after "string ")
        var content = "class TestComponent;\nstring Name = \"default\";";
        var result = await service.TryResolveAtPositionAsync(
            "test.qmui",
            content,
            1,
            7);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsProperty);
        Assert.AreEqual("Name", result.Property!.RawPropertyName);
        Assert.AreEqual(PropertyResolutionKind.RefDeclaration, result.Property.Kind);
        Assert.IsTrue(result.Property.DisplayString.Contains("(reactive)"));
    }

    [TestMethod]
    public async Task TryResolveAtPositionAsync_ComputedDeclaration_ReturnsPropertyResult()
    {
        var compilation = CSharpCompilation.Create("test");
        var workspace = new MockWorkspaceManager2 { Compilation = compilation };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider2();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        // "class TestComponent;\nstring FullName => `FirstName + LastName`;"
        // Position on line 1, char 7 is where "FullName" starts (after "string ")
        var content = "class TestComponent;\nstring FullName => `FirstName + LastName`;";
        var result = await service.TryResolveAtPositionAsync(
            "test.qmui",
            content,
            1,
            7);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsProperty);
        Assert.AreEqual("FullName", result.Property!.RawPropertyName);
        Assert.AreEqual(PropertyResolutionKind.RefDeclaration, result.Property.Kind);
        Assert.IsTrue(result.Property.DisplayString.Contains("(computed)"));
    }

    [TestMethod]
    public async Task TryResolveAtPositionAsync_NestedTag_ReturnsTagResult()
    {
        // Add Button type to compilation
        var buttonSource = "namespace System.Windows.Controls; public class Button : System.Windows.FrameworkElement { }";
        var buttonSyntaxTree = CSharpSyntaxTree.ParseText(buttonSource);
        var compilation = CSharpCompilation.Create("test", [buttonSyntaxTree]);

        var workspace = new MockWorkspaceManager2 { Compilation = compilation };
        var catalog = new QuickMarkupWorkspaceCatalog();
        var fileProvider = new MockFileProvider2();
        var service = new QmuiSemanticService(workspace, catalog, fileProvider);

        // <Grid><Button /></Grid>
        // Position on line 0, char 7 is on "Button" (after "<Grid><")
        var content = "<Grid><Button /></Grid>";
        var result = await service.TryResolveAtPositionAsync(
            "test.qmui",
            content,
            0,
            7);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsTag);
        Assert.AreEqual("Button", result.Tag!.RawTagName);
    }
}

internal class MockWorkspaceManager2 : IRoslynWorkspaceManager
{
    public bool IsLoaded { get; set; }
    public string? CurrentProjectPath { get; set; }
    public Compilation? Compilation { get; set; }
    public Task<bool> InitializeAsync(string workspaceRoot) => Task.FromResult(true);
    public Task<bool> TryLoadAsync(string projectPath) => Task.FromResult(true);
    public Task<bool> EnsureProjectForFileAsync(string qmuiFilePath) => Task.FromResult(true);
}

internal class MockFileProvider2 : IFileProvider
{
    public string ReadAllText(string path) => "";
    public string[] GetFiles(string directory, string pattern, bool recursive) => [];
    public bool DirectoryExists(string path) => false;
}
