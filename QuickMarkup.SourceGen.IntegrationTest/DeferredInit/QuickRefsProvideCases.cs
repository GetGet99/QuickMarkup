using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test.DeferredInit;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    inject string Label;
    <TestText Text=`Label` />
    """)]
public partial class QuickRefsProvideTarget : IQuickMarkupComponent<TestText>;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    <root>
        <QuickRefsProvideTarget />
    </root>
    """)]
[QuickRefs("""
    using QuickMarkup.SourceGen.Test.DeferredInit;
    public provide string Label = "from-quickrefs";
    """)]
public partial class QuickRefsProvideCase : TestRoot;
