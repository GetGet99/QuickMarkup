using System.Collections.ObjectModel;

namespace QuickMarkup.SourceGen.Test;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestText Text="backward compat" />
    </root>
    """)]
public partial class BackwardCompatChildTest : TestRoot
{
    public BackwardCompatChildTest()
    {
        Init();
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        mtb = <TestText Text="named in compat" />
    </root>
    """)]
public partial class BackwardCompatNamedVariableCase : TestRoot
{
    public BackwardCompatNamedVariableCase()
    {
        Init();
    }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            foreach (var item in `Items`) {
                <SimpleTextComponent SimpleText=`item.Text` />
            }
        </TestPanel>
    </root>
    """)]
public partial class BackwardCompatForeachComponentCase : TestRoot
{
    public ObservableCollection<TestItem> Items { get; } =
    [
        new(1, "one"),
        new(2, "two")
    ];

    public BackwardCompatForeachComponentCase()
    {
        Init();
    }
}
