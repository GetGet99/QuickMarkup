using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.SemanticService;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class QmuiSemanticServiceTests
{
    [TestMethod]
    public async Task TryResolveAtPositionAsync_EmptyContent_ReturnsNull()
    {
        var workspace = new MockQmuiWorkspaceService { Compilation = null };
        var service = new QmuiSemanticService(workspace);

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
        var workspace = new MockQmuiWorkspaceService { Compilation = null };
        var service = new QmuiSemanticService(workspace);

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
        var workspace = new MockQmuiWorkspaceService { Compilation = compilation };
        var service = new QmuiSemanticService(workspace);

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
        var workspace = new MockQmuiWorkspaceService { Compilation = compilation };
        var service = new QmuiSemanticService(workspace);

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
        var workspace = new MockQmuiWorkspaceService { Compilation = compilation };
        var service = new QmuiSemanticService(workspace);

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
        Assert.Contains("(reactive)", result.Property.DisplayString);
    }

    [TestMethod]
    public async Task TryResolveAtPositionAsync_ComputedDeclaration_ReturnsPropertyResult()
    {
        var compilation = CSharpCompilation.Create("test");
        var workspace = new MockQmuiWorkspaceService { Compilation = compilation };
        var service = new QmuiSemanticService(workspace);

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
        Assert.Contains("(computed)", result.Property.DisplayString);
    }

    [TestMethod]
    public async Task TryResolveAtPositionAsync_NestedTag_ReturnsTagResult()
    {
        var buttonSource = "namespace System.Windows.Controls; public class Button : System.Windows.FrameworkElement { }";
        var buttonSyntaxTree = CSharpSyntaxTree.ParseText(buttonSource);
        var compilation = CSharpCompilation.Create("test", [buttonSyntaxTree]);

        var workspace = new MockQmuiWorkspaceService { Compilation = compilation };
        var service = new QmuiSemanticService(workspace);

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
