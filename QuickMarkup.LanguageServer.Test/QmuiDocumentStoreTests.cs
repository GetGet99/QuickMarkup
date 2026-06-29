using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class QmuiDocumentStoreTests
{
    [TestMethod]
    public async Task GetTextAsync_EmptyStore_ReturnsNull()
    {
        var store = new QmuiDocumentStore();
        var result = await store.GetTextAsync("test.qmui");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateTextAsync_ThenGetTextAsync_ReturnsContent()
    {
        var store = new QmuiDocumentStore();
        await store.UpdateTextAsync("test.qmui", "<Button />");
        
        var result = await store.GetTextAsync("test.qmui");
        Assert.AreEqual("<Button />", result);
    }

    [TestMethod]
    public async Task UpdateTextAsync_OverwritesExistingContent()
    {
        var store = new QmuiDocumentStore();
        await store.UpdateTextAsync("test.qmui", "old content");
        await store.UpdateTextAsync("test.qmui", "new content");
        
        var result = await store.GetTextAsync("test.qmui");
        Assert.AreEqual("new content", result);
    }

    [TestMethod]
    public async Task RemoveAsync_ThenGetTextAsync_ReturnsNull()
    {
        var store = new QmuiDocumentStore();
        await store.UpdateTextAsync("test.qmui", "<Button />");
        await store.RemoveAsync("test.qmui");
        
        var result = await store.GetTextAsync("test.qmui");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task RemoveAsync_NonExistentKey_DoesNotThrow()
    {
        var store = new QmuiDocumentStore();
        await store.RemoveAsync("nonexistent.qmui");
        // Should not throw
    }

    [TestMethod]
    public async Task MultipleFiles_IndependentStorage()
    {
        var store = new QmuiDocumentStore();
        await store.UpdateTextAsync("file1.qmui", "content1");
        await store.UpdateTextAsync("file2.qmui", "content2");
        
        Assert.AreEqual("content1", await store.GetTextAsync("file1.qmui"));
        Assert.AreEqual("content2", await store.GetTextAsync("file2.qmui"));
    }
}
