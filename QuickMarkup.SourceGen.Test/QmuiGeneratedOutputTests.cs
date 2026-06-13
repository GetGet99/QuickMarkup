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

    [TestMethod]
    public void RefDeclarationCaseQmui_RefsAreAccessible()
    {
        var page = new RefDeclarationCaseQmui();

        Assert.IsNotNull(page.Children);
        Assert.HasCount(1, page.Children);

        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        TestTreeAssert.Texts(panel.Children, "hello", "0");
    }

    [TestMethod]
    public void SetupBlockCaseQmui_SetupVariableAccessible()
    {
        var page = new SetupBlockCaseQmui();

        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        TestTreeAssert.Texts(panel.Children, "Hi");
    }

    [TestMethod]
    public void ConditionalForeachCaseQmui_ConditionalRendersCorrectly()
    {
        var page = new ConditionalForeachCaseQmui();

        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        TestTreeAssert.Texts(panel.Children, "A", "B");

        page.ShowItems = false;
        ReactiveScheduler.Tick();

        Assert.IsEmpty(panel.Children);
    }

    [TestMethod]
    public void UsingStatementCaseQmui_GeneratesCorrectTree()
    {
        var page = new UsingStatementCaseQmui();

        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        TestTreeAssert.Texts(panel.Children, "using test");
    }
}
