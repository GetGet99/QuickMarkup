using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test.DeferredInit;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    string InjectedText = "";
    <TestText Text=`InjectedText` />
    """)]
public partial class ActionConstructorTarget : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <root>
        <ActionConstructorTarget InjectedText="from consumer" />
    </root>
    """)]
public partial class ActionConstructorConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <setup>
    ConstructorCallOrderCase.SetupValue = ConstructorCallOrderCase.SharedValue;
    </setup>
    <root>
        <TestText Text="call order" />
    </root>
    """)]
public partial class ConstructorCallOrderCase : TestRoot
{
    public static string SharedValue { get; set; } = "";
    public static string ConstructorValue { get; set; } = "";
    public static string SetupValue { get; set; } = "";
    public string? InstanceProp { get; set; }

    [QuickMarkupConstructor]
    private void MyInit()
    {
        ConstructorValue = SharedValue;
        Init();
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    string DeferredPreInitValue = "";
    <TestText Text=`DeferredPreInitValue` />
    """)]
public partial class DeferredPreInitTarget : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <root>
        <DeferredPreInitTarget DeferredPreInitValue="set before init" />
    </root>
    """)]
public partial class DeferredPreInitConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    required string RequiredText = "";
    <TestText Text=`RequiredText` />
    """)]
public partial class RequiredRefsTarget : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <root>
        <RequiredRefsTarget RequiredText="required value" />
    </root>
    """)]
public partial class RequiredRefsConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    string Label = "";
    string Extra = "";
    <TestText Text=`$"{Label}: {Extra}"` />
    """)]
public partial class CtorArgWithRefsTarget : IQuickMarkupComponent<TestText>
{
    [QuickMarkupConstructor]
    private void MyInit(string label)
    {
        Label = label;
        Init(label);
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <root>
        <CtorArgWithRefsTarget("hello") Extra="world" />
    </root>
    """)]
public partial class CtorArgWithRefsConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    string Label = "";
    required int RequiredCount = 0;
    <TestText Text=`$"{Label}: {RequiredCount}"` />
    """)]
public partial class CtorArgWithRequiredTarget : IQuickMarkupComponent<TestText>
{
    [QuickMarkupConstructor]
    private void MyInit(string label)
    {
        Label = label;
        Init(label);
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <root>
        namedBtn = <DeferredPreInitTarget DeferredPreInitValue="test" `x => DeferredInitNamedAssignmentCase.NamedResult = x == namedBtn` />
    </root>
    """)]
public partial class DeferredInitNamedAssignmentCase : TestRoot
{
    public static bool NamedResult { get; set; }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <root>
        ref refBtn = <DeferredPreInitTarget DeferredPreInitValue="test" `x => DeferredInitRefAssignmentCase.RefResult = x == refBtn` />
    </root>
    """)]
public partial class DeferredInitRefAssignmentCase : TestRoot
{
    public static bool RefResult { get; set; }
}

// --- Provide/Inject tests ---

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    inject string Label;
    <TestText Text=`Label` />
    """)]
public partial class ProvideInjectBasicTarget : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    public provide string Label = "hello";
    <root>
        <ProvideInjectBasicTarget />
    </root>
    """)]
public partial class ProvideInjectBasicCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    inject? string Label;
    <TestText Text=`Label` />
    """)]
public partial class ProvideInjectOptionalTarget : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <root>
        <ProvideInjectOptionalTarget />
    </root>
    """)]
public partial class ProvideInjectOptionalMissingCase : TestRoot;

// --- Provide/Inject with 'as' keyword tests ---

// Provider: backing ref is MyRefProp, but exposed to context as MyCtx
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    public provide string MyRef as MyCtx = "from-provider";
    <root>
        <ProvideInjectAsTarget />
    </root>
    """)]
public partial class ProvideInjectAsProviderCase : TestRoot;

// Consumer: inject from context key MyCtx into local backing ref MyRefProp
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    inject string MyCtx as MyRef;
    <TestText Text=`MyRef` />
    """)]
public partial class ProvideInjectAsTarget : IQuickMarkupComponent<TestText>;

// Provider without 'as' to verify basic provide/inject still works
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    public provide string Label = "basic-hello";
    <root>
        <ProvideInjectBasicTarget />
    </root>
    """)]
public partial class ProvideInjectBasicNoAsCase : TestRoot;

// Optional inject with 'as' keyword
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    inject? string MyCtx as MyRef;
    <TestText Text=`MyRef` />
    """)]
public partial class ProvideInjectOptionalAsTarget : IQuickMarkupComponent<TestText>;

// No provider for optional as target - should return default
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <root>
        <ProvideInjectOptionalAsTarget />
    </root>
    """)]
public partial class ProvideInjectOptionalAsMissingCase : TestRoot;

// --- Provide/Inject timing tests (runs before QuickMarkupConstructor) ---

// Target: inject is available inside QuickMarkupConstructor
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    inject string Label;
    <TestText Text=`Label` />
    """)]
public partial class ProvideInjectTimingTarget : IQuickMarkupComponent<TestText>
{
    public static string? CapturedLabelInCtor { get; set; }

    [QuickMarkupConstructor]
    private void MyInit()
    {
        // Label (injected) should be available here because provide/inject runs before ctor
        CapturedLabelInCtor = Label;
        Init();
    }
}

// Parent that provides and uses the timing target
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    public provide string Label = "injected-before-ctor";
    <root>
        <ProvideInjectTimingTarget />
    </root>
    """)]
public partial class ProvideInjectTimingCase : TestRoot;

// --- Provide/Inject with ctor args tests ---

// Child with inject + ctor param
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    inject string Label;
    string Extra = "";
    <TestText Text=`$"{Label}-{Extra}"` />
    """)]
public partial class ProvideInjectCtorArgsTarget : IQuickMarkupComponent<TestText>
{
    [QuickMarkupConstructor]
    private void MyInit(string extra)
    {
        Extra = extra;
        Init(extra);
    }
}

// Parent with provide + ctor arg syntax
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    public provide string Label = "hello";
    <root>
        <ProvideInjectCtorArgsTarget("world") />
    </root>
    """)]
public partial class ProvideInjectCtorArgsCase : TestRoot;

// --- Provide/Inject with ctor args + required refs tests ---

// Child with inject + ctor param + required ref
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    inject string Label;
    string Extra = "";
    required int RequiredCount = 0;
    <TestText Text=`$"{Label}-{Extra}:{RequiredCount}"` />
    """)]
public partial class ProvideInjectCtorArgsRequiredTarget : IQuickMarkupComponent<TestText>
{
    [QuickMarkupConstructor]
    private void MyInit(string extra)
    {
        Extra = extra;
        Init(extra);
    }
}

// Parent with provide + ctor arg syntax + required ref set via attribute
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    public provide string Label = "hello";
    <root>
        <ProvideInjectCtorArgsRequiredTarget("world") RequiredCount=42 />
    </root>
    """)]
public partial class ProvideInjectCtorArgsRequiredCase : TestRoot;

// --- Primary constructor context propagation tests ---

// Component that exposes its context for testing
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    string Value = "";
    <TestText Text=`Value` />
    """)]
public partial class ContextExposingTarget : IQuickMarkupComponent<TestText>;

// --- Action constructor context behavior tests ---

// Target that captures context in QuickMarkupConstructor
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    string Value = "";
    <TestText Text=`Value` />
    """)]
public partial class ContextCaptureTarget : IQuickMarkupComponent<TestText>
{
    public QuickMarkupContext? CapturedContext { get; set; }

    [QuickMarkupConstructor]
    private void MyInit()
    {
        CapturedContext = Context;
        Init();
    }
}

// --- Context hierarchy tests (parent -> child with provide/inject) ---

// Child that injects
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    inject string DeepValue;
    <TestText Text=`DeepValue` />
    """)]
public partial class ProvideInjectHierarchyChildTarget : IQuickMarkupComponent<TestText>;

// Parent that provides and creates child
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    public provide string DeepValue = "from-parent";
    <root>
        <ProvideInjectHierarchyChildTarget />
    </root>
    """)]
public partial class ProvideInjectHierarchyCase : TestRoot;

// --- Provide in QuickMarkupConstructor (provide runs before ctor method) ---
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    public provide string Label = "default";
    <root>
        <ProvideInjectBasicTarget />
    </root>
    """)]
public partial class ProvideInCtorCase : TestRoot
{
    [QuickMarkupConstructor]
    private void MyInit()
    {
        // Change the provided value after provide has run
        Label = "changed-in-ctor";
        Init();
    }
}
