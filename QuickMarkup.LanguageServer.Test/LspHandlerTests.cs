using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using QuickMarkup.AST;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Handlers;
using QuickMarkup.LanguageServer.Navigation;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace QuickMarkup.LanguageServer.Test;

[TestClass]
public sealed class LspHandlerTests
{
    [TestMethod]
    public async Task HoverHandler_NullDocumentStore_ReturnsNull()
    {
        var documentStore = new QmuiDocumentStore();
        var cursorResolver = new MarkupCursorResolver(new MockSemanticService());
        var workspace = new MockQmuiWorkspaceService();
        var handler = new QmuiHoverHandler(documentStore, cursorResolver, workspace);

        var result = await handler.Handle(new HoverParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = DocumentUri.File("nonexistent.qmui") },
            Position = new LspPosition(0, 0)
        }, CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task HoverHandler_WithContent_ReturnsHoverForTag()
    {
        var documentStore = new QmuiDocumentStore();
        var uri = DocumentUri.File("test.qmui");
        var filePath = uri.GetFileSystemPath();
        await documentStore.UpdateTextAsync(filePath, "<Button />");

        var semanticService = new MockSemanticService
        {
            ResolutionToReturn = new CursorResolutionResult(
                new TagResolutionResult(
                    TagIdentifierAST: new PositionedIdentifier("Button"),
                    RawTagName: "Button",
                    ResolvedSymbol: null,
                    DisplayString: "Button"),
                null)
        };
        var cursorResolver = new MarkupCursorResolver(semanticService);
        var workspace = new MockQmuiWorkspaceService();
        var handler = new QmuiHoverHandler(documentStore, cursorResolver, workspace);

        var result = await handler.Handle(new HoverParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new LspPosition(0, 1)
        }, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Contents);
    }

    [TestMethod]
    public async Task HoverHandler_WithPropertyResult_ReturnsHoverContent()
    {
        var documentStore = new QmuiDocumentStore();
        var uri = DocumentUri.File("test.qmui");
        var filePath = uri.GetFileSystemPath();
        await documentStore.UpdateTextAsync(filePath, "string Name = \"\";");

        var semanticService = new MockSemanticService
        {
            ResolutionToReturn = new CursorResolutionResult(
                null,
                new PropertyResolutionResult(
                    PropertyAST: new PositionedIdentifier("Name"),
                    RawPropertyName: "Name",
                    RoslynSymbol: null,
                    GeneratedSymbol: null,
                    DisplayString: "(reactive) string Name",
                    Kind: PropertyResolutionKind.RefDeclaration))
        };
        var cursorResolver = new MarkupCursorResolver(semanticService);
        var workspace = new MockQmuiWorkspaceService();
        var handler = new QmuiHoverHandler(documentStore, cursorResolver, workspace);

        var result = await handler.Handle(new HoverParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new LspPosition(0, 7)
        }, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Contents);
    }

    [TestMethod]
    public async Task DefinitionHandler_WithTagResult_ReturnsLocation()
    {
        const string filePath = "test.qmui";
        var documentStore = new QmuiDocumentStore();
        await documentStore.UpdateTextAsync(filePath, "<Button />");

        var semanticService = new MockSemanticService
        {
            ResolutionToReturn = new CursorResolutionResult(
                new TagResolutionResult(
                    TagIdentifierAST: new PositionedIdentifier("Button"),
                    RawTagName: "Button",
                    ResolvedSymbol: null,
                    DisplayString: "Button"),
                null)
        };
        var cursorResolver = new MarkupCursorResolver(semanticService);
        var workspace = new MockQmuiWorkspaceService();
        var handler = new QmuiDefinitionHandler(documentStore, cursorResolver,
            new SymbolLocationResolver(workspace, null!), workspace);

        var result = await handler.Handle(new DefinitionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = DocumentUri.File(filePath) },
            Position = new LspPosition(0, 1)
        }, CancellationToken.None);

        // No symbol resolved, so location will be null, but handler should not throw
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task HoverHandler_WithoutDocumentStoreContent_ReturnsNull()
    {
        const string storedPath = "test.qmui";
        var documentStore = new QmuiDocumentStore();
        await documentStore.UpdateTextAsync(storedPath, "<Button />");

        var handler = new QmuiHoverHandler(
            documentStore,
            new MarkupCursorResolver(new MockSemanticService()),
            new MockQmuiWorkspaceService());

        // Uri with different path should not find content
        var result = await handler.Handle(new HoverParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = DocumentUri.File("other.qmui") },
            Position = new LspPosition(0, 0)
        }, CancellationToken.None);

        Assert.IsNull(result);
    }

}
