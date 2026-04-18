using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test;

[TestClass]
public sealed class SourceGenBehaviorTests
{
    [TestMethod]
    public void StaticTreeCreatesNestedChildren()
    {
        var page = new StaticTreeCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);

        TestTreeAssert.Texts(panel.Children, "A", "B");
    }

    [TestMethod]
    public void ContentPropertyResolverUsesExpectedOrder()
    {
        var page = new ContentResolutionCase();

        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        var items = TestTreeAssert.Child<ItemsOnlyElement>(page.Children, 1);
        var child = TestTreeAssert.Child<ChildOnlyElement>(page.Children, 2);
        var content = TestTreeAssert.Child<ContentOnlyElement>(page.Children, 3);
        var ambiguous = TestTreeAssert.Child<AmbiguousElement>(page.Children, 4);

        TestTreeAssert.Texts(panel.Children, "children");
        TestTreeAssert.Texts(items.Items, "items");
        Assert.AreEqual("child", ((TestText)child.Child!).Text);
        Assert.AreEqual("content", ((TestText)content.Content!).Text);
        TestTreeAssert.Texts(ambiguous.Children, "ambiguous");
        Assert.IsNull(ambiguous.Content);
    }

    [TestMethod]
    public void AlternateChildSyntaxesAssignToTheSameTree()
    {
        var page = new AlternateChildSyntaxCase();

        var propertyValue = TestTreeAssert.Child<TestButton>(page.Children, 0);
        var valueList = TestTreeAssert.Child<TestPanel>(page.Children, 1);
        var contentTag = TestTreeAssert.Child<TestButton>(page.Children, 2);
        var childrenTag = TestTreeAssert.Child<TestPanel>(page.Children, 3);

        Assert.AreEqual("content property", TestTreeAssert.Content<TestText>(propertyValue).Text);
        TestTreeAssert.Texts(valueList.Children, "value list");
        Assert.AreEqual("content tag", TestTreeAssert.Content<TestText>(contentTag).Text);
        TestTreeAssert.Texts(childrenTag.Children, "children tag");
    }

    [TestMethod]
    public void SourceToTargetBindingUpdatesOnTick()
    {
        var page = new ReactiveBindingCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual("A", text.Text);

        page.Label = "B";
        ReactiveScheduler.Tick();

        Assert.AreEqual("B", text.Text);
    }

    [TestMethod]
    public void ConditionalSingleChildReplacesContentAndUpdatesActiveBranch()
    {
        var page = new ConditionalContentCase();
        var button = TestTreeAssert.Child<TestButton>(page.Children, 0);

        Assert.AreEqual("A", TestTreeAssert.Content<TestText>(button).Text);

        page.AText = "A2";
        ReactiveScheduler.Tick();

        Assert.AreEqual("A2", TestTreeAssert.Content<TestText>(button).Text);

        page.UseA = false;
        ReactiveScheduler.Tick();

        var b = TestTreeAssert.Content<TestText>(button);
        Assert.AreEqual("B", b.Text);

        page.AText = "A3";
        page.BText = "B2";
        ReactiveScheduler.Tick();

        Assert.AreEqual("B2", b.Text);
    }

    [TestMethod]
    public void NestedConditionalSingleChildUsesNearestElseAndDisposesInactiveBranch()
    {
        var page = new NestedConditionalContentCase();
        var button = TestTreeAssert.Child<TestButton>(page.Children, 0);

        Assert.AreEqual("inner false", TestTreeAssert.Content<TestText>(button).Text);

        page.Inner = true;
        ReactiveScheduler.Tick();

        var innerTrue = TestTreeAssert.Content<TestText>(button);
        Assert.AreEqual("inner true", innerTrue.Text);

        page.Outer = false;
        ReactiveScheduler.Tick();

        Assert.AreEqual("outer false", TestTreeAssert.Content<TestText>(button).Text);

        page.InnerTrue = "stale";
        ReactiveScheduler.Tick();

        Assert.AreEqual("outer false", TestTreeAssert.Content<TestText>(button).Text);
    }

    [TestMethod]
    public void ConditionalCollectionBlockPreservesSiblingOrder()
    {
        var page = new CollectionIfCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);

        TestTreeAssert.Texts(panel.Children, "before", "A", "B", "after");

        page.Show = false;
        ReactiveScheduler.Tick();

        TestTreeAssert.Texts(panel.Children, "before", "C", "after");
    }

    [TestMethod]
    public void FragmentBlockAddsMultipleChildren()
    {
        var page = new FragmentCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);

        TestTreeAssert.Texts(panel.Children, "A", "B");
    }

    [TestMethod]
    public void ForeachCollectionReconcilesChildren()
    {
        var page = new ForeachCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);

        TestTreeAssert.Texts(panel.Children, "one", "two");

        page.Items.Add(new(3, "three"));
        ReactiveScheduler.Tick();

        TestTreeAssert.Texts(panel.Children, "one", "two", "three");

        page.Items.RemoveAt(1);
        ReactiveScheduler.Tick();

        TestTreeAssert.Texts(panel.Children, "one", "three");
    }

    [TestMethod]
    public void ForeachCapturedEventHandlerKeepsDelegateType()
    {
        ForeachEventCaptureCase.ClickCount = 0;
        var page = new ForeachEventCaptureCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        var button = TestTreeAssert.Child<TestButton>(panel.Children, 0);

        button.RaiseClicked();

        Assert.AreEqual(1, ForeachEventCaptureCase.ClickCount);
    }

    [TestMethod]
    public void ForeachIndexAndKeyMoveReusesElementAndUpdatesIndex()
    {
        var page = new ForeachIndexKeyCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        var first = TestTreeAssert.Child<TestText>(panel.Children, 0);

        TestTreeAssert.Texts(panel.Children, "0:one", "1:two", "2:three");

        page.Items.Move(0, 2);
        ReactiveScheduler.Tick();

        TestTreeAssert.Texts(panel.Children, "0:two", "1:three", "2:one");
        Assert.AreSame(first, panel.Children[2]);
    }
}
