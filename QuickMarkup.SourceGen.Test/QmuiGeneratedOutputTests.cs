using QuickMarkup.SourceGen.Test;

namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class QmuiGeneratedOutputTests
{
    [TestMethod]
    public void StaticTreeCaseQmui_GeneratesCorrectTree()
    {
        var page = new StaticTreeCaseQmui();

        Assert.IsNotNull(page.Children);
        Assert.AreEqual(1, page.Children.Count);

        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        Assert.AreEqual(2, panel.Children.Count);

        TestTreeAssert.Texts(panel.Children, "A", "B");
    }
}
