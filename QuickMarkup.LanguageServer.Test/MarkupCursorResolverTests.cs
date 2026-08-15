using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Navigation;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class MarkupCursorResolverTests
{
    [TestMethod]
    public async Task ResolveAtPositionAsync_NullContent_ReturnsNull()
    {
        var semanticService = new MockSemanticService();
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveAtPositionAsync(
            "test.qmui", 
            null!, 
            new LspPosition(0, 0));
        
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ResolveAtPositionAsync_EmptyContent_ReturnsNull()
    {
        var semanticService = new MockSemanticService();
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveAtPositionAsync(
            "test.qmui", 
            "", 
            new LspPosition(0, 0));
        
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ResolveAtPositionAsync_NothingAtPosition_ReturnsNull()
    {
        var semanticService = new MockSemanticService();
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveAtPositionAsync(
            "test.qmui", 
            "<Button />", 
            new LspPosition(0, 1));
        
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ResolveAtPositionAsync_TagAtPosition_ReturnsTagResult()
    {
        var expectedTag = new TagResolutionResult(
            TagIdentifierAST: new PositionedIdentifier("Button"),
            RawTagName: "Button",
            ResolvedSymbol: null,
            DisplayString: "Button");
        
        var semanticService = new MockSemanticService 
        { 
            ResolutionToReturn = new CursorResolutionResult(expectedTag, null)
        };
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveAtPositionAsync(
            "test.qmui", 
            "<Button />", 
            new LspPosition(0, 1));
        
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsTag);
        Assert.AreEqual("Button", result.Tag!.RawTagName);
    }

    [TestMethod]
    public async Task ResolveAtPositionAsync_PropertyAtPosition_ReturnsPropertyResult()
    {
        var expectedProperty = new PropertyResolutionResult(
            PropertyAST: new PositionedIdentifier("Content"),
            RawPropertyName: "Content",
            RoslynSymbol: null,
            GeneratedSymbol: null,
            DisplayString: "(reactive) string Content",
            Kind: PropertyResolutionKind.TagAttribute);
        
        var semanticService = new MockSemanticService 
        { 
            ResolutionToReturn = new CursorResolutionResult(null, expectedProperty)
        };
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveAtPositionAsync(
            "test.qmui", 
            "<Button Content=\"Hello\" />", 
            new LspPosition(0, 8));
        
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsProperty);
        Assert.AreEqual("Content", result.Property!.RawPropertyName);
    }

    [TestMethod]
    public async Task ResolveAtPositionAsync_ReactiveProperty_ShowsReactivePrefix()
    {
        var expectedProperty = new PropertyResolutionResult(
            PropertyAST: new PositionedIdentifier("Name"),
            RawPropertyName: "Name",
            RoslynSymbol: null,
            GeneratedSymbol: new QuickMarkupGeneratedPropertySymbol(
                Name: "Name",
                TypeName: "string",
                Accessibility: Language.Symbols.ResolvedAccessibility.Public,
                Kind: QuickMarkupGeneratedPropertyKind.RefValue,
                IsRequired: false,
                IsNullableAware: true
            ),
            DisplayString: "(reactive) string Name",
            Kind: PropertyResolutionKind.RefDeclaration);
        
        var semanticService = new MockSemanticService 
        { 
            ResolutionToReturn = new CursorResolutionResult(null, expectedProperty)
        };
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveAtPositionAsync(
            "test.qmui", 
            "string Name = \"default\";", 
            new LspPosition(0, 7));
        
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsProperty);
        Assert.AreEqual("(reactive) string Name", result.Property!.DisplayString);
    }

    [TestMethod]
    public async Task ResolveAtPositionAsync_ComputedProperty_ShowsComputedPrefix()
    {
        var expectedProperty = new PropertyResolutionResult(
            PropertyAST: new PositionedIdentifier("FullName"),
            RawPropertyName: "FullName",
            RoslynSymbol: null,
            GeneratedSymbol: new QuickMarkupGeneratedPropertySymbol(
                Name: "FullName",
                TypeName: "string",
                Accessibility: Language.Symbols.ResolvedAccessibility.Public,
                Kind: QuickMarkupGeneratedPropertyKind.ComputedValue,
                IsRequired: false,
                IsNullableAware: true
            ),
            DisplayString: "(computed) string FullName",
            Kind: PropertyResolutionKind.RefDeclaration);
        
        var semanticService = new MockSemanticService 
        { 
            ResolutionToReturn = new CursorResolutionResult(null, expectedProperty)
        };
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveAtPositionAsync(
            "test.qmui", 
            "string FullName => `FirstName + LastName`;", 
            new LspPosition(0, 7));
        
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsProperty);
        Assert.AreEqual("(computed) string FullName", result.Property!.DisplayString);
    }
}

internal class MockSemanticService : IQmuiSemanticService
{
    public CursorResolutionResult? ResolutionToReturn { get; set; }
    
    public Task<CursorResolutionResult?> TryResolveAtPositionAsync(
        string filePath,
        string content,
        int line,
        int character,
        CancellationToken ct = default)
    {
        return Task.FromResult(ResolutionToReturn);
    }
}
