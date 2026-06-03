using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.CodeAnalysis.Helpers;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class QuickMarkupWorkspaceCatalogTests
{
    [TestMethod]
    public void Entries_InitiallyEmpty()
    {
        var catalog = new QuickMarkupWorkspaceCatalog();
        Assert.AreEqual(0, catalog.Entries.Length);
    }

    [TestMethod]
    public void TryGetEntry_NonExistent_ReturnsFalse()
    {
        var catalog = new QuickMarkupWorkspaceCatalog();
        var result = catalog.TryGetEntry("NonExistent.Type", out var entry);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetEntriesByShortName_NoMatch_ReturnsEmpty()
    {
        var catalog = new QuickMarkupWorkspaceCatalog();
        var results = catalog.GetEntriesByShortName("NonExistent").ToList();
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Rebuild_EmptyWorkspace_AddsNoEntries()
    {
        var catalog = new QuickMarkupWorkspaceCatalog();
        var compilation = CSharpCompilation.Create("test");
        var fileProvider = new TestMockFileProvider();

        catalog.Rebuild(compilation, "C:\\NonExistentDirectory", fileProvider);

        Assert.AreEqual(0, catalog.Entries.Length);
    }
}
