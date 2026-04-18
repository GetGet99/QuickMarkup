using System.Collections.ObjectModel;

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
public partial class StaticTreeCase : TestRoot
{
}

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
public partial class ContentResolutionCase : TestRoot
{
}

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
public partial class AlternateChildSyntaxCase : TestRoot
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    string Label = "A";
    <root>
        <TestText Text=`Label` />
    </root>
    """)]
public partial class ReactiveBindingCase : TestRoot
{
}

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
public partial class ConditionalContentCase : TestRoot
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool ShouldShowOriginal;
    <root>
        holdButton=<TestComputedHoldButton IsHolding=>`ShouldShowOriginal` />
    </root>
    """)]
public partial class ComputedBindBackCase : TestRoot
{
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    bool ShouldShowOriginal;
    <root>
        holdButton=<TestDependencyHoldButton IsHolding=>`ShouldShowOriginal` />
    </root>
    """)]
public partial class DependencyPropertyBindBackCase : TestRoot
{
}

[QuickMarkup("""
    using System.Collections.Generic;
    using QuickMarkup.SourceGen.Test;
    NullableRefItem? NullableItem = null;
    `List<int>?` SomeList = null;
    <root>
    </root>
    """)]
public partial class NullableNullRefDeclarationCase : TestRoot
{
}

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
public partial class NestedConditionalContentCase : TestRoot
{
}

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
public partial class CollectionIfCase : TestRoot
{
}

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
public partial class FragmentCase : TestRoot
{
}

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
