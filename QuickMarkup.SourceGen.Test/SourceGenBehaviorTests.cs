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
    public void SetupVariablesAreAvailableToTemplateExpressions()
    {
        var page = new SetupScopeCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual("from setup", text.Text);
    }

    [TestMethod]
    public void PrimitiveValuesAndBooleanShorthandAssignProperties()
    {
        var page = new PrimitiveValueCase();

        var trueText = TestTreeAssert.Child<TestText>(page.Children, 0);
        var falseText = TestTreeAssert.Child<TestText>(page.Children, 1);
        var defaults = TestTreeAssert.Child<TestText>(page.Children, 2);

        Assert.IsTrue(trueText.Flag);
        Assert.IsFalse(falseText.Flag);
        Assert.IsNull(defaults.Text);
        Assert.AreEqual(0, defaults.Number);
    }

    [TestMethod]
    public void GeneratedQuickMarkupPropertiesAreVisibleToOtherMarkup()
    {
        var page = new GeneratedPropertyConsumerCase();
        var element = TestTreeAssert.Child<GeneratedPropertyElement>(page.Children, 0);

        Assert.AreEqual("from generated property", element.Text);
        Assert.AreEqual(TestKind.Secondary, element.Kind);
        Assert.IsTrue(element.Flag);
    }

    [TestMethod]
    public void SingleComponentUnwrapsMarkupNodeAndAppliesOutputMembers()
    {
        var page = new SingleComponentConsumerCase();
        ReactiveScheduler.Tick();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        var text = TestTreeAssert.Child<TestText>(panel.Children, 0);

        Assert.AreEqual("Secondary:Hello", text.Text);
        Assert.AreEqual(7, text.Number);
        Assert.IsTrue(text.ElementExtensionApplied);
    }

    [TestMethod]
    public void SingleComponentWithoutRootUnwrapsMarkupNodeAndAppliesOutputMembers()
    {
        var page = new StyledTestTextNoRootConsumerCase();
        ReactiveScheduler.Tick();
        var text = TestTreeAssert.Child<TestText>(page.MarkupNode.Children, 0);

        Assert.AreEqual("Secondary:Hello", text.Text);
        Assert.AreEqual(7, text.Number);
        Assert.IsTrue(text.ElementExtensionApplied);
    }

    [TestMethod]
    public void FragmentComponentExpandsIntoParentCollection()
    {
        var page = new FragmentComponentConsumerCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);

        TestTreeAssert.Texts(panel.Children, "before", "fragment A", "fragment B", "after");
    }

    [TestMethod]
    public void FragmentComponentWithoutRootExpandsIntoParentCollection()
    {
        var page = new SingleTextFragmentNoRootUsage();
        var panel = page.MarkupNode;

        TestTreeAssert.Texts(panel.Children, "before", "fragment A", "after");
    }

    [TestMethod]
    public void MultipleTextFragmentWithoutRootExpandsIntoParentCollection()
    {
        var page = new MultiTextFragmentNoRootUsage();
        var panel = page.MarkupNode;

        TestTreeAssert.Texts(panel.Children, "before", "fragment A", "fragment B", "fragment C", "after");
    }

    [TestMethod]
    public void ComponentIsSealedInGeneratedPartial()
    {
        Assert.IsTrue(typeof(StyledTestText).IsSealed);
    }

    [TestMethod]
    public void ComponentWithoutRootIsSealedInGeneratedPartial()
    {
        Assert.IsTrue(typeof(StyledTestTextNoRoot).IsSealed);
    }

    [TestMethod]
    public void ComponentCallbackTargetsComponentInstance()
    {
        var page = new ComponentCallbackTargetsComponentCase();
        ReactiveScheduler.Tick();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        var text = TestTreeAssert.Child<TestText>(panel.Children, 0);

        Assert.AreEqual("from callback", text.Text);
    }

    [TestMethod]
    public void ComponentCallbackWithoutRootTargetsComponentInstance()
    {
        var page = new ComponentCallbackNoRootConsumerCase();
        ReactiveScheduler.Tick();
        var text = TestTreeAssert.Child<TestText>(page.MarkupNode.Children, 0);

        Assert.AreEqual("from callback", text.Text);
    }

    [TestMethod]
    public void ComponentRefPropertiesForwardToOutput()
    {
        var page = new ComponentNamedRefInstanceCase();
        ReactiveScheduler.Tick();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        var text = TestTreeAssert.Child<TestText>(panel.Children, 0);

        Assert.AreEqual("named", text.Text);
    }

    [TestMethod]
    public void ComponentInConditionalCollectionRendersConditionally()
    {
        var page = new ComponentInConditionalCase();
        ReactiveScheduler.Tick();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);

        TestTreeAssert.Texts(panel.Children, "conditional");

        page.Show = false;
        ReactiveScheduler.Tick();

        Assert.IsEmpty(panel.Children);
    }

    [TestMethod]
    public void ComponentWithoutRootInConditionalCollectionRendersConditionally()
    {
        var page = new ComponentNoRootInConditionalCase();
        ReactiveScheduler.Tick();
        var panel = page.MarkupNode;

        TestTreeAssert.Texts(panel.Children, "conditional");

        page.Show = false;
        ReactiveScheduler.Tick();

        Assert.IsEmpty(panel.Children);
    }

    [TestMethod]
    public void NumericLiteralAutoNewsOneParameterTargetType()
    {
        var page = new AutoNewCase();
        var element = TestTreeAssert.Child<AutoNewElement>(page.Children, 0);

        Assert.AreEqual(16, element.Radius.Value);
    }

    [TestMethod]
    public void ExtensionAndForeignCallbacksRunDuringInitialization()
    {
        var page = new CallbackCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);

        Assert.IsTrue(panel.ExtensionApplied);
        Assert.IsTrue(panel.CallbackApplied);
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
    public void BindBackTracksComputedTargetReference()
    {
        var page = new ComputedBindBackCase();
        var holdButton = TestTreeAssert.Child<TestComputedHoldButton>(page.Children, 0);

        Assert.IsFalse(page.ShouldShowOriginal);

        holdButton.IsHoldingInput = true;
        ReactiveScheduler.Tick();

        Assert.IsTrue(page.ShouldShowOriginal);
    }

    [TestMethod]
    public void BindBackTracksDependencyPropertyTarget()
    {
        var page = new DependencyPropertyBindBackCase();
        var holdButton = TestTreeAssert.Child<TestDependencyHoldButton>(page.Children, 0);

        Assert.IsFalse(page.ShouldShowOriginal);

        holdButton.IsHolding = true;

        Assert.IsTrue(page.ShouldShowOriginal);
    }

    [TestMethod]
    public void TwoWayDependencyPropertyBindingSynchronizesBothDirections()
    {
        var page = new DependencyPropertyTwoWayCase();
        var first = TestTreeAssert.Child<TestDependencyHoldButton>(page.Children, 0);
        var second = TestTreeAssert.Child<TestDependencyHoldButton>(page.Children, 1);

        page.SharedHolding = true;
        ReactiveScheduler.Tick();

        Assert.IsTrue(first.IsHolding);
        Assert.IsTrue(second.IsHolding);

        first.IsHolding = false;
        ReactiveScheduler.Tick();

        Assert.IsFalse(page.SharedHolding);
        Assert.IsFalse(second.IsHolding);
    }

    [TestMethod]
    public void NullableReferenceDeclarationsCanDefaultToNull()
    {
        var page = new NullableNullRefDeclarationCase();

        Assert.IsNull(page.NullableItem);
        Assert.IsNull(page.SomeList);
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
    public void ConditionalSingleChildWithDifferentBranchTypesCompilesAndRenders()
    {
        var page = new ConditionalContentDifferentTypesCase();
        var button = TestTreeAssert.Child<TestButton>(page.Children, 0);

        // ShowPanel is false by default, so TestText should be the content
        Assert.IsNotNull(button.Content);
        Assert.IsInstanceOfType<TestText>(button.Content);
        Assert.AreEqual("text", ((TestText)button.Content).Text);

        page.ShowPanel = true;
        ReactiveScheduler.Tick();

        // Now TestPanel should be the content
        Assert.IsNotNull(button.Content);
        Assert.IsInstanceOfType<TestPanel>(button.Content);
    }

    [TestMethod]
    public void ConditionalSlotWithDifferentBranchTypesViaExplicitContentTag()
    {
        var page = new ConditionalSlotDifferentBranchTypesCase();
        var button = TestTreeAssert.Child<TestButton>(page.Children, 0);

        // ShowPanel is false by default, so TestText should be the content
        Assert.IsNotNull(button.Content);
        Assert.IsInstanceOfType<TestText>(button.Content);
        Assert.AreEqual("text", ((TestText)button.Content).Text);

        page.ShowPanel = true;
        ReactiveScheduler.Tick();

        // Now TestPanel should be the content
        Assert.IsNotNull(button.Content);
        Assert.IsInstanceOfType<TestPanel>(button.Content);
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
    public void RangeForeachAddsStaticRangeChildren()
    {
        var page = new RangeForeachCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);

        TestTreeAssert.Texts(panel.Children, "Row 1", "Row 2", "Row 3", "Row 4", "Row 5", "Row 6");
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
        ForeachEventCaptureCase.FirstClickCount = 0;
        ForeachEventCaptureCase.SecondClickCount = 0;
        var page = new ForeachEventCaptureCase();
        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        var button = TestTreeAssert.Child<TestButton>(panel.Children, 0);

        button.RaiseClicked();

        Assert.AreEqual(1, ForeachEventCaptureCase.FirstClickCount);
        Assert.AreEqual(0, ForeachEventCaptureCase.SecondClickCount);

        page.Items[0] = new(1, "updated", (_, _) => ForeachEventCaptureCase.SecondClickCount++);
        ReactiveScheduler.Tick();

        Assert.AreSame(button, panel.Children[0]);

        button.RaiseClicked();

        Assert.AreEqual(1, ForeachEventCaptureCase.FirstClickCount);
        Assert.AreEqual(1, ForeachEventCaptureCase.SecondClickCount);
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

    [TestMethod]
    public void AttachedPropertySetRowAssignsNumericValue()
    {
        var page = new AttachedPropertyAssignCase();
        var first = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual(1, Grid.GetRow(first));
    }

    [TestMethod]
    public void AttachedPropertyReactiveBindingUpdatesOnTick()
    {
        var page = new AttachedPropertyAssignCase();
        var second = TestTreeAssert.Child<TestText>(page.Children, 1);

        Assert.AreEqual(42, Grid.GetRow(second));

        page.RowIndex = 99;
        ReactiveScheduler.Tick();

        Assert.AreEqual(99, Grid.GetRow(second));
    }

    [TestMethod]
    public void AttachedPropertyBindBackTracksDependencyProperty()
    {
        var page = new AttachedPropertyBindBackCase();
        var button = TestTreeAssert.Child<TestDependencyHoldButton>(page.Children, 0);

        Assert.AreEqual(0, Grid.GetRow(button));

        Grid.SetRow(button, 7);
        button.IsHolding = true;
        ReactiveScheduler.Tick();

        Assert.AreEqual(7, page.StoredRow);
    }

    [TestMethod]
    public void AttachedPropertyChildTagSetRowAssignsNumericValue()
    {
        var page = new AttachedPropertyChildTagAssignCase();
        var child = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual(1, Grid.GetRow(child));
    }

    [TestMethod]
    public void StaticRefDeclarationHasExpectedDefaultValue()
    {
        _ = new StaticRefDeclarationCase();

        Assert.AreEqual("static", StaticRefDeclarationCase.StaticText);
        Assert.AreEqual(42, StaticRefDeclarationCase.StaticInt);
    }

    [TestMethod]
    public void StaticComputedDeclarationReturnsExpectedValue()
    {
        _ = new StaticComputedDeclarationCase();

        Assert.AreEqual("static computed", StaticComputedDeclarationCase.StaticComputedText);
    }

    [TestMethod]
    public void StaticRefValueIsSharedAcrossInstances()
    {
        _ = new StaticRefDeclarationCase();
        _ = new StaticRefDeclarationCase();

        StaticRefDeclarationCase.StaticInt = 99;

        Assert.AreEqual(99, StaticRefDeclarationCase.StaticInt);
    }

    [TestMethod]
    public void PublicRefDeclarationHasExpectedDefaultValue()
    {
        var instance = new PublicRefDeclarationCase();

        Assert.AreEqual("public", instance.PublicText);
        Assert.AreEqual(42, instance.PublicInt);
    }

    [TestMethod]
    public void PublicRefCanSetAndGetValue()
    {
        var instance = new PublicRefDeclarationCase();

        instance.PublicText = "changed";
        instance.PublicInt = 99;

        Assert.AreEqual("changed", instance.PublicText);
        Assert.AreEqual(99, instance.PublicInt);
    }

    [TestMethod]
    public void PublicStaticRefDeclarationHasExpectedDefaultValue()
    {
        _ = new PublicStaticRefDeclarationCase();

        Assert.AreEqual("public static", PublicStaticRefDeclarationCase.PublicStaticText);
    }

    [TestMethod]
    public void PublicComputedDeclarationReturnsExpectedValue()
    {
        var instance = new PublicComputedDeclarationCase();

        Assert.AreEqual("public computed", instance.PublicComputedText);
    }

    [TestMethod]
    public void AttachedPropertyChildTagReactiveBindingUpdatesOnTick()
    {
        var page = new AttachedPropertyChildTagReactiveCase();
        var child = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual(42, Grid.GetRow(child));

        page.RowIndex = 99;
        ReactiveScheduler.Tick();

        Assert.AreEqual(99, Grid.GetRow(child));
    }

    [TestMethod]
    public void RefNamedTagCreatesElementInTree()
    {
        var page = new RefNamedTagCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual("ref named tag", text.Text);
    }

    [TestMethod]
    public void RefNamedTagElementSupportsPropertyBindings()
    {
        var page = new RefNamedTagBindingCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual("ref binding", text.Text);

        page.Text = "ref updated";
        ReactiveScheduler.Tick();

        Assert.AreEqual("ref updated", text.Text);
    }

    [TestMethod]
    public void ForeignExpressionWithDotAsKeyIsNotTreatedAsAttachedProperty()
    {
        var page = new ForeignDottedKeyCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual("from foreign key", text.Nested.Text);
    }

    [TestMethod]
    public void NullRefDeclaration_DefaultsToNull()
    {
        var page = new NullRefDeclarationCase();

        Assert.IsNull(page.NullItem);
    }

    [TestMethod]
    public void EmptyPanel_HasNoChildren()
    {
        var page = new EmptyPanelCase();

        Assert.IsNotNull(page.Children);
        Assert.HasCount(1, page.Children);

        var panel = TestTreeAssert.Child<TestPanel>(page.Children, 0);
        Assert.IsEmpty(panel.Children);
    }

    [TestMethod]
    public void AttachedPropertyColumnDefaultsToZero()
    {
        var page = new AttachedPropertyAssignCase();
        var first = TestTreeAssert.Child<TestText>(page.Children, 0);

        Assert.AreEqual(0, Grid.GetColumn(first));
    }
}
