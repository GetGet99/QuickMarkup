using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using QuickMarkup.LanguageServer.Contracts;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiHoverHandler : IHoverHandler
{
    readonly IQmuiDocumentStore _documentStore;
    readonly IMarkupCursorResolver _cursorResolver;
    readonly IQmuiWorkspaceService _workspace;

    public QmuiHoverHandler(
        IQmuiDocumentStore documentStore,
        IMarkupCursorResolver cursorResolver,
        IQmuiWorkspaceService workspace)
    {
        _documentStore = documentStore;
        _cursorResolver = cursorResolver;
        _workspace = workspace;
    }

    public HoverRegistrationOptions GetRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions();
    }

    public async Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        await _workspace.EnsureProjectForFileAsync(request.TextDocument.Uri.GetFileSystemPath());

        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var content = await _documentStore.GetTextAsync(filePath, cancellationToken);
        if (content is null)
            return null;

        // Single traversal to find either tag or property
        var result = await _cursorResolver.ResolveAtPositionAsync(
            filePath,
            content,
            request.Position,
            cancellationToken);

        if (result is null)
            return null;

        if (result.Tag is { } tagResult)
        {
            var tagNode = tagResult.TagIdentifierAST as QuickMarkup.AST.PositionedIdentifier;
            LspRange? range = null;
            if (tagNode is not null)
            {
                range = new LspRange(
                    new LspPosition(tagNode.Start.Line, tagNode.Start.Char),
                    new LspPosition(tagNode.End.Line, tagNode.End.Char));
            }

            return new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(
                    new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = $"```csharp\n{tagResult.DisplayString}\n```"
                    }),
                Range = range
            };
        }

        if (result.Property is { } propertyResult)
        {
            LspRange? propertyRange = CalculatePropertyRange(propertyResult);

            return new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(
                    new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = $"```csharp\n{propertyResult.DisplayString}\n```"
                    }),
                Range = propertyRange
            };
        }

        return null;
    }

    private static LspRange? CalculatePropertyRange(PropertyResolutionResult propertyResult)
    {
        var ast = propertyResult.PropertyAST;
        if (ast is QuickMarkup.AST.QuickMarkupParsedProperty property)
        {
            var keyLength = propertyResult.RawPropertyName.Length;
            var keyStart = property.Start;
            var keyEnd = new Get.PLShared.Position(keyStart.Line, keyStart.Char + keyLength);
            return new LspRange(
                new LspPosition(keyStart.Line, keyStart.Char),
                new LspPosition(keyEnd.Line, keyEnd.Char));
        }
        else if (ast is QuickMarkup.AST.PositionedIdentifier identifier)
        {
            return new LspRange(
                new LspPosition(identifier.Start.Line, identifier.Start.Char),
                new LspPosition(identifier.End.Line, identifier.End.Char));
        }
        else if (ast is QuickMarkup.AST.QuickMarkupPropertyTagStart propertyTagStart)
        {
            return new LspRange(
                new LspPosition(propertyTagStart.Start.Line, propertyTagStart.Start.Char),
                new LspPosition(propertyTagStart.End.Line, propertyTagStart.End.Char));
        }
        else if (ast is QuickMarkup.AST.QuickMarkupAttachedPropertyTagStart attachedPropertyTagStart)
        {
            return new LspRange(
                new LspPosition(attachedPropertyTagStart.Start.Line, attachedPropertyTagStart.Start.Char),
                new LspPosition(attachedPropertyTagStart.End.Line, attachedPropertyTagStart.End.Char));
        }

        return null;
    }
}
