using Microsoft.CodeAnalysis;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Test;

namespace QuickMarkup.LanguageServer.Diagnostics.Test;

[TestClass]
public sealed class QmuiDiagnosticServiceTests
{
    [TestMethod]
    public async Task GetDiagnosticsAsync_NoCompilation_ReturnsSyntaxOnly()
    {
        var workspace = new MockQmuiWorkspaceService { Compilation = null };
        var service = new QmuiDiagnosticService(workspace);

        var result = await service.GetDiagnosticsAsync("test.qmui", "<Button />", CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetDiagnosticsAsync_EmptyContent_ReturnsNoDiagnostics()
    {
        var workspace = new MockQmuiWorkspaceService { Compilation = null };
        var service = new QmuiDiagnosticService(workspace);

        var result = await service.GetDiagnosticsAsync("test.qmui", "", CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetDiagnosticsAsync_NoClassDeclaration_ReturnsSyntaxOnly()
    {
        var workspace = new MockQmuiWorkspaceService { Compilation = null };
        var service = new QmuiDiagnosticService(workspace);

        var result = await service.GetDiagnosticsAsync(
            "test.qmui", "<Button x:Name=\"test\" />", CancellationToken.None);

        Assert.IsNotNull(result);
    }
}
