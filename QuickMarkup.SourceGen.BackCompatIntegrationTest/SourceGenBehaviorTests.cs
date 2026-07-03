namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class SourceGenBehaviorTests
{
    [TestMethod]
    public void BackwardCompatWithExplicitConstructor_CallsInitAndBuildsTree()
    {
        var page = new BackwardCompatChildTest();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual("backward compat", text.Text);
    }
}
