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
