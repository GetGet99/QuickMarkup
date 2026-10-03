using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test.Shared;

[TestClass]
public sealed class QuickRefsTests
{
    [TestMethod]
    public void MultipleQuickRefsAttributes_MergeIntoOneType()
    {
        var instance = new QuickRefsMultipleCase();

        Assert.AreEqual("Ada", instance.FirstName);
        Assert.AreEqual("Lovelace", instance.LastName);
        Assert.AreEqual("Ada Lovelace", instance.FullName);
    }

    [TestMethod]
    public void QuickRefsOnly_DoesNotTakeOverConstructors()
    {
        var instance = new QuickRefsMultipleCase("Grace");

        Assert.AreEqual("Grace", instance.FirstName);
        Assert.AreEqual("Grace Lovelace", instance.FullName);
    }

    [TestMethod]
    public void QuickRefsComputed_UpdatesWhenDependencyChanges()
    {
        var instance = new QuickRefsMultipleCase();

        instance.FirstName = "Alan";
        ReactiveScheduler.Tick();

        Assert.AreEqual("Alan Lovelace", instance.FullName);
    }

    [TestMethod]
    public void QuickRefsAsyncComputed_ReturnsExpectedValue()
    {
        var instance = new QuickRefsMultipleCase();

        Assert.AreEqual(AsyncComputedState.Success, instance.GreetingStatus);
        Assert.AreEqual("Hello, Ada Lovelace!", instance.Greeting);
    }

    [TestMethod]
    public void QuickMarkupAndQuickRefs_RefsAreUnioned()
    {
        var page = new QuickRefsUnionCase();

        Assert.AreEqual("Hi", TestTreeAssert.Child<TestText>(page.Children, 0).Text);
        Assert.AreEqual("sub", TestTreeAssert.Child<TestText>(page.Children, 1).Text);

        page.Subtitle = "updated";
        ReactiveScheduler.Tick();

        Assert.AreEqual("updated", TestTreeAssert.Child<TestText>(page.Children, 1).Text);
    }
}
