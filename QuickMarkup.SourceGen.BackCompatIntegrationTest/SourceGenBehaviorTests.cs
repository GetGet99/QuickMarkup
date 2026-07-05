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

    [TestMethod]
    public void BackwardCompatNamedVariable_IsNotReadonly()
    {
        // Regression test: named variables in backward compatible mode should not be
        // declared as readonly, because they are assigned in Init() not the constructor.
        var page = new BackwardCompatNamedVariableCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual("named in compat", text.Text);
    }

    [TestMethod]
    public void BackwardCompatForeachWithComponent_CapturesLoopVariable()
    {
        var page = new BackwardCompatForeachComponentCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);

        var first = TestTreeAssert.Child<TestText>(panel.Children, 0);
        var second = TestTreeAssert.Child<TestText>(panel.Children, 1);

        Assert.AreEqual("one", first.Text);
        Assert.AreEqual("two", second.Text);
    }
}
