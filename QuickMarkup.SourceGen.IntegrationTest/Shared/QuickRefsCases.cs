using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test.Shared;

[QuickRefs("""
    using QuickMarkup.SourceGen.Test.Shared;
    string FirstName = "Ada";
    """)]
[QuickRefs("""
    using QuickMarkup.SourceGen.Test.Shared;
    string LastName = "Lovelace";
    string FullName => `$"{FirstName} {LastName}"`;
    string Greeting => async `Task.FromResult($"Hello, {FullName}!")`;
    """)]
public partial class QuickRefsMultipleCase
{
    public QuickRefsMultipleCase() { }
    public QuickRefsMultipleCase(string firstName) { FirstName = firstName; }
}

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.Shared;
    string Title = "Hi";
    <root>
        <TestText Text=`Title` />
        <TestText Text=`Subtitle` />
    </root>
    """)]
[QuickRefs("""
    using QuickMarkup.SourceGen.Test.Shared;
    string Subtitle = "sub";
    """)]
public partial class QuickRefsUnionCase : TestRoot;

[QuickRefs("""
    using QuickMarkup.SourceGen.Test.Shared;
    int Counter = 0;
    """)]
public partial class QuickRefsLeafCase : TestElement;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.Shared;
    <root>
        <TestPanel>
            <QuickRefsLeafCase Counter=5 />
            <QuickRefsLeafCase />
        </TestPanel>
    </root>
    """)]
public partial class QuickRefsConsumeCase : TestRoot;
