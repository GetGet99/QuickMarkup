using Get.Lexer;
using QuickMarkup.AST;
using QuickMarkup.Parser;

namespace QuickMarkup.Syntax.Test;

[TestClass]
public sealed class QuickMarkupFileParserTests
{
    [TestMethod]
    public void Parse_NamespaceClassAndBody_ReturnsSfcWithHeader()
    {
        var result = ParseAndLex("""
            namespace MyApp.Pages;
            class MyPage : Page;
            <root>
                <TextBlock Text="Hello" />
            </root>
            """);

        Assert.AreEqual("MyApp.Pages", result.Namespace?.Name);
        Assert.AreEqual("MyPage", result.ClassDeclaration?.Name);
        Assert.AreEqual(ClassKind.Subclass, result.ClassDeclaration?.Kind);
        Assert.AreEqual("Page", result.ClassDeclaration?.BaseTypes);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void Parse_ClassOnlyNoNamespace_ReturnsNullNamespace()
    {
        var result = ParseAndLex("""
            class MyComponent : TestRoot;
            <root>
                <TextBlock Text="Hello" />
            </root>
            """);

        Assert.IsNull(result.Namespace);
        Assert.AreEqual("MyComponent", result.ClassDeclaration?.Name);
        Assert.AreEqual(ClassKind.Subclass, result.ClassDeclaration?.Kind);
        Assert.AreEqual("TestRoot", result.ClassDeclaration?.BaseTypes);
    }

    [TestMethod]
    public void Parse_ComponentKind_ReturnsComponent()
    {
        var result = ParseAndLex("""
            component Card : Border;
            <root>
                <TextBlock Text="Card" />
            </root>
            """);

        Assert.AreEqual("Card", result.ClassDeclaration?.Name);
        Assert.AreEqual(ClassKind.Component, result.ClassDeclaration?.Kind);
        Assert.AreEqual("Border", result.ClassDeclaration?.BaseTypes);
    }

    [TestMethod]
    public void Parse_FragmentComponentKind_ReturnsFragmentComponent()
    {
        var result = ParseAndLex("""
            fragment component ItemList : UIElement;
            <TextBlock Text="A" />
            <TextBlock Text="B" />
            """);

        Assert.AreEqual("ItemList", result.ClassDeclaration?.Name);
        Assert.AreEqual(ClassKind.FragmentComponent, result.ClassDeclaration?.Kind);
        Assert.AreEqual("UIElement", result.ClassDeclaration?.BaseTypes);
    }

    [TestMethod]
    public void Parse_NoBaseTypes_ReturnsNullBaseTypes()
    {
        var result = ParseAndLex("""
            class MyPage;
            <root>
                <TextBlock Text="Hello" />
            </root>
            """);

        Assert.AreEqual("MyPage", result.ClassDeclaration?.Name);
        Assert.IsNull(result.ClassDeclaration?.BaseTypes);
    }

    [TestMethod]
    public void Parse_WithRefDeclarations_IncludesRefs()
    {
        var result = ParseAndLex("""
            class CounterPage : Page;
            int Counter = 0;
            <root>
                <TextBlock Text=`Counter.ToString()` />
            </root>
            """);

        Assert.AreEqual("CounterPage", result.ClassDeclaration?.Name);
        Assert.AreEqual(1, result.Refs.Count);
        Assert.AreEqual("Counter", result.Refs[0].Name.Name);
    }

    [TestMethod]
    public void Parse_WithSetupBlock_IncludesScript()
    {
        var result = ParseAndLex("""
            class MyPage : Page;
            <setup>
                var theme = UseThemeBrushes(this);
            </setup>
            <root Background=`theme.SolidBackground`>
                <TextBlock Text="Hello" />
            </root>
            """);

        Assert.AreEqual("MyPage", result.ClassDeclaration?.Name);
        Assert.IsNotNull(result.Scirpt);
        Assert.Contains("UseThemeBrushes", result.Scirpt.RawScript);
    }

    [TestMethod]
    public void Parse_MultipleBaseTypes_CapturesAll()
    {
        var result = ParseAndLex("""
            class MyPage : Page, ISomeInterface;
            <root>
                <TextBlock Text="Hello" />
            </root>
            """);

        Assert.AreEqual("MyPage", result.ClassDeclaration?.Name);
        Assert.AreEqual("Page, ISomeInterface", result.ClassDeclaration?.BaseTypes);
    }

    [TestMethod]
    public void Parse_OnlyQmContentWithoutHeader_ReturnsNullHeader()
    {
        var result = ParseAndLex("""
            <root>
                <TextBlock Text="Hello" />
            </root>
            """);

        Assert.IsNull(result.Namespace);
        Assert.IsNull(result.ClassDeclaration);
    }

    [TestMethod]
    public void Parse_UsingsBeforeHeader_IncludesUsings()
    {
        var result = ParseAndLex("""
            using System.Linq;
            namespace MyApp;
            class MyPage : Page;
            <root>
                <TextBlock Text="Hello" />
            </root>
            """);

        Assert.AreEqual("MyApp", result.Namespace?.Name);
        Assert.AreEqual("MyPage", result.ClassDeclaration?.Name);
        Assert.Contains("using System.Linq;", result.Usings);
    }

    static QuickMarkupSFC ParseAndLex(string content)
    {
        var lexer = new QuickMarkupLexer(new StringTextSeeker(content));
        var tokens = lexer.GetTokens();
        var parser = new QuickMarkupParser();
        return parser.Parse(tokens, out _);
    }
}
