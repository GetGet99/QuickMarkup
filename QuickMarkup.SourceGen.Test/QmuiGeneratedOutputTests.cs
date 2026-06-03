using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class QmuiGeneratedOutputTests
{
    [TestMethod]
    public void StaticTreeCaseQmui_GeneratesCorrectTree()
    {
        var page = new StaticTreeCaseQmui();

        Assert.IsNotNull(page.Children);
        Assert.HasCount(1, page.Children);

        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        Assert.HasCount(2, panel.Children);

        TestTreeAssert.Texts(panel.Children, "A", "B");
    }

    [TestMethod]
    public void ComponentCaseQmui_GeneratesCorrectComponent()
    {
        var comp = new ComponentCaseQmui();
        var component = (IQuickMarkupComponent<TestPanel>)comp;
        var panel = component.MarkupNode;

        TestTreeAssert.Texts(panel.Children, "A", "B");
        Assert.IsTrue(typeof(ComponentCaseQmui).IsSealed);
    }

    [TestMethod]
    public void FragmentComponentCaseQmui_GeneratesCorrectFragment()
    {
        var frag = new FragmentComponentCaseQmui();
        var fragment = (IQuickMarkupFragmentComponent<TestElement>)frag;

        Assert.IsNotNull(fragment.MarkupNode);
        Assert.IsTrue(typeof(FragmentComponentCaseQmui).IsSealed);
    }
}
