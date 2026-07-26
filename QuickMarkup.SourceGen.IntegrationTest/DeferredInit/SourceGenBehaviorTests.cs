using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test.DeferredInit;

[TestClass]
public sealed class SourceGenBehaviorTests
{
    [TestMethod]
    public void ActionConstructor_SetsPropertiesBeforeInit()
    {
        var comp = new ActionConstructorTarget(x =>
        {
            x.InjectedText = "injected value";
        });

        // The Action lambda runs before Init(), so InjectedText is available
        // when the markup evaluates Text=`InjectedText`.
        Assert.AreEqual("injected value", comp.MarkupNode.Text);
    }

    [TestMethod]
    public void ActionConstructorConsumer_UsesActionPatternFromGeneratedCode()
    {
        var page = new ActionConstructorConsumerCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        // The consumer markup sets InjectedText on ActionConstructorTarget via the
        // Action<T> constructor pattern (DeferredInit consumer codegen).
        // The Text binding references InjectedText, which was set before Init().
        Assert.AreEqual("from consumer", text.Text);
    }

    [TestMethod]
    public void ConstructorRunsBeforeAction_SetupRunsAfterAction()
    {
        ConstructorCallOrderCase.SharedValue = "";
        ConstructorCallOrderCase.ConstructorValue = "not set";
        ConstructorCallOrderCase.SetupValue = "not set";

        var comp = new ConstructorCallOrderCase(x =>
        {
            x.InstanceProp = "set by action";
            ConstructorCallOrderCase.SharedValue = "set by action";
        });

        Assert.AreEqual("set by action", ConstructorCallOrderCase.ConstructorValue,
            "Constructor should run after action — SharedValue should be set");
        Assert.AreEqual("set by action", ConstructorCallOrderCase.SetupValue,
            "Setup should run after action — SharedValue should be set");
        Assert.AreEqual("set by action", comp.InstanceProp,
            "Action should have set InstanceProp");
    }

    [TestMethod]
    public void RequiredRefsTarget_ConstructorParameterPassesValue()
    {
        var comp = new RequiredRefsTarget("explicit value");
        Assert.AreEqual("explicit value", comp.MarkupNode.Text);
    }

    [TestMethod]
    public void RequiredRefsConsumer_SetsRequiredPropertyInMarkup()
    {
        var page = new RequiredRefsConsumerCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("required value", text.Text);
    }

    [TestMethod]
    public void DeferredPreInitConsumer_PropertiesSetBeforeInit()
    {
        var page = new DeferredPreInitConsumerCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        // The consumer sets DeferredPreInitValue="set before init" via the Action<T>
        // constructor. The component's own markup binds Text to DeferredPreInitValue,
        // which was set before Init() ran, so it should be visible.
        Assert.AreEqual("set before init", text.Text);
    }

    [TestMethod]
    public void CtorArgWithRefs_ConstructorArgAndPropertySetBeforeInit()
    {
        var page = new CtorArgWithRefsConsumerCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        // The consumer markup passes "hello" as constructor arg and Extra="world" as property.
        // CGenDeferredInit must preserve the constructor arg when generating the Action<T> pattern.
        // Result: Text=$"{Label}: {Extra}" → "hello: world"
        Assert.AreEqual("hello: world", text.Text);
    }

    [TestMethod]
    public void CtorArgWithRequired_PrimaryConstructor()
    {
        var comp = new CtorArgWithRequiredTarget("hello", 42);

        // Primary constructor: Init("hello") sets Label, this.RequiredCount = 42
        Assert.AreEqual("hello: 42", comp.MarkupNode.Text);
    }

    [TestMethod]
    public void CtorArgWithRequired_ActionConstructor()
    {
        var comp = new CtorArgWithRequiredTarget("hello", x =>
        {
            x.RequiredCount = 42;
        });

        // Action constructor: Init("hello") sets Label, action sets RequiredCount, then Init evaluates template
        Assert.AreEqual("hello: 42", comp.MarkupNode.Text);
    }

    [TestMethod]
    public void DeferredInit_NamedVariableAssignedBeforeCallback()
    {
        DeferredInitNamedAssignmentCase.NamedResult = false;
        var page = new DeferredInitNamedAssignmentCase();

        // The named variable assignment should execute before the callback inside
        // the DeferredInit lambda, so x == namedBtn evaluates to true.
        Assert.IsTrue(DeferredInitNamedAssignmentCase.NamedResult);
    }

    [TestMethod]
    public void DeferredInit_RefVariableAssignedBeforeCallback()
    {
        DeferredInitRefAssignmentCase.RefResult = false;
        var page = new DeferredInitRefAssignmentCase();

        // For ref captures, btnProp.Value should be set before the callback.
        Assert.IsTrue(DeferredInitRefAssignmentCase.RefResult);
    }

    [TestMethod]
    public void ProvideInjectBasic_ParentProvidesChildInjects()
    {
        var page = new ProvideInjectBasicCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("hello", text.Text);
    }

    [TestMethod]
    public void ProvideInjectCtorArgs_BothAvailableInCtor()
    {
        var page = new ProvideInjectCtorArgsCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("hello-world", text.Text);
    }

    [TestMethod]
    public void ProvideInjectCtorArgsRequired_AllAvailable()
    {
        var page = new ProvideInjectCtorArgsRequiredCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("hello-world:42", text.Text);
    }

    [TestMethod]
    public void ProvideInjectOptional_NoProvider_ReturnsDefault()
    {
        var page = new ProvideInjectOptionalMissingCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.IsNull(text.Text);
    }

    [TestMethod]
    public void ProvideInjectBasic_ProviderAndInjectorShareSameReference()
    {
        var page = new ProvideInjectBasicCase();

        // The parent's LabelProp and the child's LabelProp should be the same Reference<string> object.
        // Verify by changing the parent's value and checking the child sees it.
        page.Label = "world";
        ReactiveScheduler.Tick();

        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("world", text.Text);
    }

    [TestMethod]
    public void ProvideInjectAs_ProviderProvidesChildInjectsWithDifferentNames()
    {
        var page = new ProvideInjectAsProviderCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("from-provider", text.Text);
    }

    [TestMethod]
    public void ProvideInjectAs_ProviderAndInjectorShareSameReference()
    {
        var page = new ProvideInjectAsProviderCase();

        // Changing provider's MyRef should propagate to child's MyRef
        page.MyRef = "updated";
        ReactiveScheduler.Tick();

        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("updated", text.Text);
    }

    [TestMethod]
    public void ProvideInjectBasicNoAs_WorksCorrectly()
    {
        var page = new ProvideInjectBasicNoAsCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("basic-hello", text.Text);
    }

    [TestMethod]
    public void ProvideInjectOptionalAs_NoProvider_ReturnsDefault()
    {
        var page = new ProvideInjectOptionalAsMissingCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.IsNull(text.Text);
    }

    // --- Provide/Inject timing tests (runs before QuickMarkupConstructor) ---

    [TestMethod]
    public void ProvideInjectTiming_InjectedValueAvailableInCtor()
    {
        ProvideInjectTimingTarget.CapturedLabelInCtor = null;
        var page = new ProvideInjectTimingCase();

        // Provide/inject runs before QuickMarkupConstructor, so Label should be available
        Assert.AreEqual("injected-before-ctor", ProvideInjectTimingTarget.CapturedLabelInCtor,
            "Injected value should be available inside QuickMarkupConstructor");
    }

    [TestMethod]
    public void ProvideInjectTiming_ChildRendersCorrectly()
    {
        var page = new ProvideInjectTimingCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("injected-before-ctor", text.Text);
    }

    // --- Context propagation tests ---

    [TestMethod]
    public void PrimaryConstructor_CreatesContextWhenNull()
    {
        var comp = new ContextExposingTarget();
        Assert.IsNotNull(comp.Context, "Primary constructor should create a context");
    }

    [TestMethod]
    public void ActionConstructor_CreatesContextWhenNotSet()
    {
        ContextCaptureTarget.CapturedContext = null;
        var comp = new ContextCaptureTarget(x => { });
        Assert.IsNotNull(ContextCaptureTarget.CapturedContext,
            "Action constructor should create a context when not set by initializer");
    }

    [TestMethod]
    public void ActionConstructor_UsesContextFromInitializer()
    {
        ContextCaptureTarget.CapturedContext = null;
        var sharedContext = new QuickMarkupContext();
        var comp = new ContextCaptureTarget(x =>
        {
            x.Context = sharedContext;
        });

        Assert.AreSame(sharedContext, ContextCaptureTarget.CapturedContext,
            "Action constructor should use context set by initializer (??= semantics)");
    }

    [TestMethod]
    public void PrimaryConstructor_UsesPassedContext()
    {
        var sharedContext = new QuickMarkupContext();
        var comp = new ContextExposingTarget(QUICKMARKUP_CONTEXT: sharedContext);
        Assert.AreSame(sharedContext, comp.Context,
            "Primary constructor should use the passed QUICKMARKUP_CONTEXT");
    }

    // --- Context hierarchy tests ---

    [TestMethod]
    public void ProvideInjectHierarchy_ParentValueReachesChild()
    {
        var page = new ProvideInjectHierarchyCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("from-parent", text.Text);
    }

    [TestMethod]
    public void ProvideInjectHierarchy_ReactiveChangePropagates()
    {
        var page = new ProvideInjectHierarchyCase();
        page.DeepValue = "updated";
        ReactiveScheduler.Tick();

        var text = TestTreeAssert.Child<TestText>(page.Children, 0);
        Assert.AreEqual("updated", text.Text);
    }

    // --- Provide in QuickMarkupConstructor ---

    [TestMethod]
    public void ProvideInCtor_ChangeAfterProvideStillWorks()
    {
        var page = new ProvideInCtorCase();
        var text = TestTreeAssert.Child<TestText>(page.Children, 0);

        // The ctor changes Label after provide runs, but since they share the same Reference,
        // the child should see the updated value
        Assert.AreEqual("changed-in-ctor", text.Text);
    }
}
