using QuickMarkup.AST;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Navigation;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class MarkupCursorResolverTests
{
    [TestMethod]
    public async Task ResolveTagAtPositionAsync_NullContent_ReturnsNull()
    {
        var semanticService = new MockSemanticService();
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveTagAtPositionAsync(
            "test.qmui", 
            null!, 
            new LspPosition(0, 0));
        
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ResolveTagAtPositionAsync_EmptyContent_ReturnsNull()
    {
        var semanticService = new MockSemanticService();
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveTagAtPositionAsync(
            "test.qmui", 
            "", 
            new LspPosition(0, 0));
        
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ResolveTagAtPositionAsync_ValidMarkup_DelegatesToSemanticService()
    {
        var expectedTag = new TagResolutionResult(
            TagIdentifierAST: new PositionedIdentifier("Button"),
            RawTagName: "Button",
            ResolvedSymbol: null,
            DisplayString: "Button");
        
        var semanticService = new MockSemanticService { TagToReturn = expectedTag };
        var resolver = new MarkupCursorResolver(semanticService);
        
        var result = await resolver.ResolveTagAtPositionAsync(
            "test.qmui", 
            "<Button />", 
            new LspPosition(0, 1));
        
        Assert.IsNotNull(result);
        Assert.AreEqual("Button", result.RawTagName);
    }
}

internal class MockSemanticService : IQmuiSemanticService
{
    public TagResolutionResult? TagToReturn { get; set; }
    
    public Task<TagResolutionResult?> TryResolveTagAtPositionAsync(
        string filePath, 
        string content, 
        int line, 
        int character, 
        CancellationToken ct = default)
    {
        return Task.FromResult(TagToReturn);
    }
}
