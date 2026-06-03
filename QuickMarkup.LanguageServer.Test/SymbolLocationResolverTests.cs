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
        var catalog = new QuickMarkupWorkspaceCatalog();
        var resolver = new SymbolLocationResolver(catalog);

        var result = resolver.GetDefinitionLocation((TagResolutionResult?)null, "test.qmui");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetDefinitionLocation_NullSymbol_ReturnsNull()
    {
        var catalog = new QuickMarkupWorkspaceCatalog();
        var resolver = new SymbolLocationResolver(catalog);

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

        var catalog = new QuickMarkupWorkspaceCatalog();
        var resolver = new SymbolLocationResolver(catalog);

        var result = resolver.GetDefinitionLocation(buttonSymbol, "test.qmui");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void GetDefinitionLocation_QmuiFileEntry_ReturnsCatalogLocation()
    {
        var catalog = new QuickMarkupWorkspaceCatalog();
        // Add a mock entry
        var entry = new QuickMarkupTypeEntry(
            FullTypeName: "Test.Component",
            ShortName: "Component",
            Namespace: "Test",
            Usings: "",
            Kind: QuickMarkupDefinitionKind.QmuiFile,
            FilePath: @"C:\path\to\component.qmui",
            NameSpan: null);

        // Use reflection to add entry (since _entries is private)
        var entriesField = typeof(QuickMarkupWorkspaceCatalog)
            .GetField("_entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        entriesField?.SetValue(catalog, ImmutableArray.Create(entry));

        var resolver = new SymbolLocationResolver(catalog);

        // Create a symbol with matching display string
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
