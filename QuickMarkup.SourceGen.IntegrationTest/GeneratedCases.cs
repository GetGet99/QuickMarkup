using System.Collections.ObjectModel;
using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            <TestText Text="A" />
            <TestText Text="B" />
        </TestPanel>
    </root>
    """)]
public partial class StaticTreeCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel Name="children"><TestText Text="children" /></TestPanel>
        <ItemsOnlyElement Name="items"><TestText Text="items" /></ItemsOnlyElement>
        <ChildOnlyElement Name="child"><TestText Text="child" /></ChildOnlyElement>
        <ContentOnlyElement Name="content"><TestText Text="content" /></ContentOnlyElement>
        <AmbiguousElement Name="ambiguous"><TestText Text="ambiguous" /></AmbiguousElement>
    </root>
    """)]
public partial class ContentResolutionCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestButton Name="property-value" Content=<TestText Text="content property" /> />
        <TestPanel Name="value-list" Children=<>
            <TestText Text="value list" />
        </> />
        <TestButton Name="content-tag">
            <.Content>
                <TestText Text="content tag" />
            </.Content>
        </TestButton>
        <TestPanel Name="children-tag">
            <.Children>
                <TestText Text="children tag" />
            </.Children>
        </TestPanel>
    </root>
    """)]
public partial class AlternateChildSyntaxCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string Label = "A";
    <root>
        <TestText Text=`Label` />
    </root>
    """)]
public partial class ReactiveBindingCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <setup>
    var suffix = " setup";
    </setup>
    <root>
        <TestText Text=`"from" + suffix` />
    </root>
    """)]
public partial class SetupScopeCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestText Name="true" Flag />
        <TestText Name="false" !Flag />
        <TestText Name="defaults" Text=null Number=default />
    </root>
    """)]
public partial class PrimitiveValueCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string Text = "";
    TestKind Kind = Default;
    bool Flag = false;
    """)]
public partial class GeneratedPropertyElement : TestElement
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    static string StaticText = "static";
    static int StaticInt = 42;
    """)]
public partial class StaticRefDeclarationCase : TestElement
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    static string StaticComputedText => `"static computed"`;
    """)]
public partial class StaticComputedDeclarationCase : TestElement
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    public string PublicText = "public";
    public int PublicInt = 42;
    """)]
public partial class PublicRefDeclarationCase : TestElement
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    public static string PublicStaticText = "public static";
    """)]
public partial class PublicStaticRefDeclarationCase : TestElement
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    public string PublicComputedText => `"public computed"`;
    """)]
public partial class PublicComputedDeclarationCase : TestElement
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <GeneratedPropertyElement Text="from generated property" Kind=Secondary Flag />
    </root>
    """)]
public partial class GeneratedPropertyConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string Text = "";
    TestKind Kind = Default;
    <root>
        <TestText Text=`$"{Kind}:{Text}"` />
    </root>
    """)]
public partial class StyledTestText : IQuickMarkupComponent<TestText>
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string Text = "";
    TestKind Kind = Default;
    <TestText Text=`$"{Kind}:{Text}"` />
    """)]
public partial class StyledTestTextNoRoot : IQuickMarkupComponent<TestText>
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            <StyledTestText Text="Hello" Kind=Secondary Number=7 MarkElement />
        </TestPanel>
    </root>
    """)]
public partial class SingleComponentConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <TestPanel>
        <StyledTestTextNoRoot Text="Hello" Kind=Secondary Number=7 MarkElement />
    </TestPanel>
    """)]
public partial class StyledTestTextNoRootConsumerCase : IQuickMarkupComponent<TestPanel>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestText Text="fragment A" />
        <TestText Text="fragment B" />
    </root>
    """)]
public partial class TwoTextFragment : IQuickMarkupFragmentComponent<TestElement>
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <TestText Text="fragment A" />
    """)]
public partial class SingleTextFragmentNoRoot : IQuickMarkupFragmentComponent<TestElement>
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <TestText Text="fragment A" />
    <TestText Text="fragment B" />
    <TestText Text="fragment C" />
    """)]
public partial class MultiTextFragmentNoRoot : IQuickMarkupFragmentComponent<TestElement>
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <TestPanel>
        <TestText Text="before" />
        <MultiTextFragmentNoRoot />
        <TestText Text="after" />
    </TestPanel>
    """)]
public partial class MultiTextFragmentNoRootUsage : IQuickMarkupComponent<TestPanel>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            <TestText Text="before" />
            <TwoTextFragment />
            <TestText Text="after" />
        </TestPanel>
    </root>
    """)]
public partial class FragmentComponentConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <TestPanel>
        <TestText Text="before" />
        <SingleTextFragmentNoRoot />
        <TestText Text="after" />
    </TestPanel>
    """)]
public partial class SingleTextFragmentNoRootUsage : IQuickMarkupComponent<TestPanel>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string CallbackText = "";
    <root>
        <TestText Text=`CallbackText` />
    </root>
    """)]
public partial class CallbackComponent : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string CallbackText = "";
    <TestText Text=`CallbackText` />
    """)]
public partial class CallbackComponentNoRoot : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            <CallbackComponent `x => x.CallbackText = "from callback"` />
        </TestPanel>
    </root>
    """)]
public partial class ComponentCallbackTargetsComponentCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <TestPanel>
        <CallbackComponentNoRoot `x => x.CallbackText = "from callback"` />
    </TestPanel>
    """)]
public partial class ComponentCallbackNoRootConsumerCase : IQuickMarkupComponent<TestPanel>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string RefText = "";
    <root>
        <TestText Text=`RefText` />
    </root>
    """)]
public partial class RefPropertyComponent : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            <RefPropertyComponent Name="myComp" RefText="named" />
        </TestPanel>
    </root>
    """)]
public partial class ComponentNamedRefInstanceCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string SimpleText = "";
    <root>
        <TestText Text=`SimpleText` />
    </root>
    """)]
public partial class SimpleTextComponent : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string SimpleText = "";
    <TestText Text=`SimpleText` />
    """)]
public partial class SimpleTextComponentNoRoot : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool Show = true;
    <root>
        <TestPanel>
            if (`Show`) {
                <SimpleTextComponent SimpleText="conditional" />
            }
        </TestPanel>
    </root>
    """)]
public partial class ComponentInConditionalCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool Show = true;
    <TestPanel>
        if (`Show`) {
            <SimpleTextComponentNoRoot SimpleText="conditional" />
        }
    </TestPanel>
    """)]
public partial class ComponentNoRootInConditionalCase : IQuickMarkupComponent<TestPanel>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <AutoNewElement Radius=16 />
    </root>
    """)]
public partial class AutoNewCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel MarkPanel `x => x.CallbackApplied = true` />
    </root>
    """)]
public partial class CallbackCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool UseA = true;
    string AText = "A";
    string BText = "B";
    <root>
        <TestButton>
            if (`UseA`)
                <TestText Text=`AText` />
            else
                <TestText Text=`BText` />
        </TestButton>
    </root>
    """)]
public partial class ConditionalContentCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool ShowPanel = false;
    <root>
        <TestButton>
            if (`ShowPanel`)
                <TestPanel Name="panel" />
            else
                <TestText Text="text" />
        </TestButton>
    </root>
    """)]
public partial class ConditionalContentDifferentTypesCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool ShowPanel = false;
    <root>
        <TestButton>
            <.Content>
                if (`ShowPanel`)
                    <TestPanel Name="panel" />
                else
                    <TestText Text="text" />
            </.Content>
        </TestButton>
    </root>
    """)]
public partial class ConditionalSlotDifferentBranchTypesCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool ShouldShowOriginal;
    <root>
        holdButton=<TestComputedHoldButton IsHolding=>`ShouldShowOriginal` />
    </root>
    """)]
public partial class ComputedBindBackCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool ShouldShowOriginal;
    <root>
        holdButton=<TestDependencyHoldButton IsHolding=>`ShouldShowOriginal` />
    </root>
    """)]
public partial class DependencyPropertyBindBackCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool SharedHolding;
    <root>
        <TestDependencyHoldButton IsHolding<=>`SharedHolding` />
        <TestDependencyHoldButton IsHolding<=>`SharedHolding` />
    </root>
    """)]
public partial class DependencyPropertyTwoWayCase : TestRoot;

[QuickMarkup("""
    using System.Collections.Generic;
    using QuickMarkup.SourceGen.Test;
    NullableRefItem? NullableItem = null;
    `List<int>?` SomeList = null;
    <root>
    </root>
    """)]
public partial class NullableNullRefDeclarationCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool Outer = true;
    bool Inner = false;
    string InnerTrue = "inner true";
    string InnerFalse = "inner false";
    <root>
        <TestButton>
            if (`Outer`) {
                if (`Inner`)
                    <TestText Text=`InnerTrue` />
                else
                    <TestText Text=`InnerFalse` />
            }
            else
                <TestText Text="outer false" />
        </TestButton>
    </root>
    """)]
public partial class NestedConditionalContentCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool Show = true;
    <root>
        <TestPanel>
            <TestText Text="before" />
            if (`Show`) {
                <TestText Text="A" />
                <TestText Text="B" />
            }
            else {
                <TestText Text="C" />
            }
            <TestText Text="after" />
        </TestPanel>
    </root>
    """)]
public partial class CollectionIfCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            {
                <TestText Text="A" />
                <TestText Text="B" />
            }
        </TestPanel>
    </root>
    """)]
public partial class FragmentCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            foreach (var row in ..3) {
                <TestText Text=`$"Row {row + 1}"` />
            }
            foreach (int row in 4..7) {
                <TestText Text=`$"Row {row}"` />
            }
        </TestPanel>
    </root>
    """)]
public partial class RangeForeachCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            foreach (var item in `Items`) {
                <TestText Text=`item.Text` />
            }
        </TestPanel>
    </root>
    """)]
public partial class ForeachCase : TestRoot
{
    public ObservableCollection<TestItem> Items { get; } =
    [
        new(1, "one"),
        new(2, "two")
    ];
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            foreach (EventItem item in `Items`; `item.Id`) {
                <TestButton Clicked+=`item.Clicked` />
            }
        </TestPanel>
    </root>
    """)]
public partial class ForeachEventCaptureCase : TestRoot
{
    public static int FirstClickCount { get; set; }
    public static int SecondClickCount { get; set; }
    public ObservableCollection<EventItem> Items { get; } =
    [
        new(1, "one", (_, _) => FirstClickCount++)
    ];
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            foreach (index; TestItem item in `Items`; `item.Id`) {
                <TestText Text=`$"{index}:{item.Text}"` />
            }
        </TestPanel>
    </root>
    """)]
public partial class ForeachIndexKeyCase : TestRoot
{
    public ObservableCollection<TestItem> Items { get; } =
    [
        new(1, "one"),
        new(2, "two"),
        new(3, "three")
    ];
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    int RowIndex = 42;
    <root>
        <TestText Grid.Row=1 />
        <TestText Grid.Row=`RowIndex` />
    </root>
    """)]
public partial class AttachedPropertyAssignCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    int StoredRow;
    <root>
        <TestDependencyHoldButton Grid.Row=>`StoredRow` />
    </root>
    """)]
public partial class AttachedPropertyBindBackCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestText>
            <Grid.Row>1</Grid.Row>
        </TestText>
    </root>
    """)]
public partial class AttachedPropertyChildTagAssignCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    int RowIndex = 42;
    <root>
        <TestText>
            <Grid.Row>`RowIndex`</Grid.Row>
        </TestText>
    </root>
    """)]
public partial class AttachedPropertyChildTagReactiveCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        ref TB = <TestText Text="ref named tag" />
    </root>
    """)]
public partial class RefNamedTagCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string Text = "ref binding";
    <root>
        ref TB = <TestText Text=`Text` />
    </root>
    """)]
public partial class RefNamedTagBindingCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestText `Nested.Text`="from foreign key" />
    </root>
    """)]
public partial class ForeignDottedKeyCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    NullableRefItem? NullItem = null;
    <root>
    </root>
    """)]
public partial class NullRefDeclarationCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
        </TestPanel>
    </root>
    """)]
public partial class EmptyPanelCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    // 你好, this is a line comment with Unicode
    <root>
        <TestText Text="café" />
        <TestText Text="你好，世界" />
        <TestText Text="🌟✨🌍" />
    </root>
    """)]
public partial class UnicodeStringCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    // コメント with Unicode
    /* 这是一个块注释
       with Unicode: ñoño */
    <root>
        <TestText Text="Unicode in string: 文字列テスト" />
        <TestText Text="Accented: résumé méil Çç ñ" />
    </root>
    """)]
public partial class UnicodeCommentCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string InjectedText = "";
    <TestText Text=`InjectedText` />
    """)]
public partial class ActionConstructorTarget : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <ActionConstructorTarget InjectedText="from consumer" />
    </root>
    """)]
public partial class ActionConstructorConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestText Text="ctor called" />
    </root>
    """)]
public partial class QuickMarkupConstructorNoParamCase : TestRoot
{
    public static bool ConstructorCalled { get; set; }

    [QuickMarkupConstructor]
    private void MyInit()
    {
        ConstructorCalled = true;
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestText Text="ctor with params" />
    </root>
    """)]
public partial class ConstructorWithParamsCase : TestRoot
{
    public static int StoredValue { get; set; }
    public static string StoredText { get; set; }

    [QuickMarkupConstructor]
    private void MyInit(int value, string text)
    {
        StoredValue = value;
        StoredText = text;
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
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
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string DeferredPreInitValue = "";
    <TestText Text=`DeferredPreInitValue` />
    """)]
public partial class DeferredPreInitTarget : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <DeferredPreInitTarget DeferredPreInitValue="set before init" />
    </root>
    """)]
public partial class DeferredPreInitConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    required string RequiredText = "";
    <TestText Text=`RequiredText` />
    """)]
public partial class RequiredRefsTarget : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <RequiredRefsTarget RequiredText="required value" />
    </root>
    """)]
public partial class RequiredRefsConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string Label = "";
    string Extra = "";
    <TestText Text=`$"{Label}: {Extra}"` />
    """)]
public partial class CtorArgWithRefsTarget : IQuickMarkupComponent<TestText>
{
    [QuickMarkupConstructor]
    private void Init(string label)
    {
        Label = label;
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <CtorArgWithRefsTarget("hello") Extra="world" />
    </root>
    """)]
public partial class CtorArgWithRefsConsumerCase : TestRoot;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string Label = "";
    required int RequiredCount = 0;
    <TestText Text=`$"{Label}: {RequiredCount}"` />
    """)]
public partial class CtorArgWithRequiredTarget : IQuickMarkupComponent<TestText>
{
    [QuickMarkupConstructor]
    private void Init(string label)
    {
        Label = label;
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        namedBtn = <DeferredPreInitTarget DeferredPreInitValue="test" `x => DeferredInitNamedAssignmentCase.NamedResult = x == namedBtn` />
    </root>
    """)]
public partial class DeferredInitNamedAssignmentCase : TestRoot
{
    public static bool NamedResult { get; set; }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        ref refBtn = <DeferredPreInitTarget DeferredPreInitValue="test" `x => DeferredInitRefAssignmentCase.RefResult = x == refBtn` />
    </root>
    """)]
public partial class DeferredInitRefAssignmentCase : TestRoot
{
    public static bool RefResult { get; set; }
}


