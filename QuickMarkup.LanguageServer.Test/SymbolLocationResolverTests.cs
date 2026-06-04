using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Navigation;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class SymbolLocationResolverTests
{
    [TestMethod]
    public void GetDefinitionLocation_NullTagResult_ReturnsNull()
    {
        var workspace = new MockQmuiWorkspaceService();
        var resolver = new SymbolLocationResolver(workspace);

        var result = resolver.GetDefinitionLocation((TagResolutionResult?)null, "test.qmui");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefinitionLocation_NullSymbol_ReturnsNull()
    {
        var workspace = new MockQmuiWorkspaceService();
        var resolver = new SymbolLocationResolver(workspace);

        var tagResult = new TagResolutionResult(
            TagIdentifierAST: new AST.PositionedIdentifier("Button"),
            RawTagName: "Button",
            ResolvedSymbol: null,
            DisplayString: "Button");

        var result = resolver.GetDefinitionLocation(tagResult, "test.qmui");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefinitionLocation_CSharpType_ReturnsLocation()
    {
        var compilation = CSharpCompilation.Create("test",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("public class Button { }") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var buttonSymbol = compilation.GetTypeByMetadataName("Button");
        Assert.IsNotNull(buttonSymbol);

        var workspace = new MockQmuiWorkspaceService();
        var resolver = new SymbolLocationResolver(workspace);

        var result = resolver.GetDefinitionLocation(buttonSymbol, "test.qmui");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void GetDefinitionLocation_QmuiFileEntry_ReturnsCatalogLocation()
    {
        var entry = new QuickMarkupTypeEntry(
            FullTypeName: "Test.Component",
            ShortName: "Component",
            Namespace: "Test",
            Usings: "",
            Kind: QuickMarkupDefinitionKind.QmuiFile,
            FilePath: @"C:\path\to\component.qmui",
            NameSpan: null);

        var workspace = new MockQmuiWorkspaceService { QmuiEntries = [entry] };
        var resolver = new SymbolLocationResolver(workspace);

        var compilation = CSharpCompilation.Create("test",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("namespace Test { public class Component { } }") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var componentSymbol = compilation.GetTypeByMetadataName("Test.Component");
        Assert.IsNotNull(componentSymbol);

        var result = resolver.GetDefinitionLocation(componentSymbol, "test.qmui");
        Assert.IsNotNull(result);
        Assert.Contains("component.qmui", result.Uri.ToString());
    }
}
