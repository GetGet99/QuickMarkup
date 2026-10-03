using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test.DeferredInit;

[TestClass]
public sealed class QuickRefsProvideTests
{
    [TestMethod]
    public void ProvideInQuickRefs_WithQuickMarkup_InjectsIntoChild()
    {
        var page = new QuickRefsProvideCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual("from-quickrefs", text.Text);
    }

    [TestMethod]
    public void ProvideInQuickRefs_ProviderAndInjectorShareSameReference()
    {
        var page = new QuickRefsProvideCase();

        page.Label = "updated";
        ReactiveScheduler.Tick();

        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("updated", text.Text);
    }
}
