using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Navigation;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiHoverHandler : IHoverHandler
{
    readonly IQmuiDocumentStore _documentStore;
    readonly MarkupCursorResolver _cursorResolver;
    readonly IServiceProvider _serviceProvider;

    public QmuiHoverHandler(
        IQmuiDocumentStore documentStore,
        MarkupCursorResolver cursorResolver,
        IServiceProvider serviceProvider)
    {
        _documentStore = documentStore;
        _cursorResolver = cursorResolver;
        _serviceProvider = serviceProvider;
    }

    public HoverRegistrationOptions GetRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions();
    }

    public async Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var workspace = _serviceProvider.GetRequiredService<IRoslynWorkspaceManager>();
        await workspace.EnsureProjectForFileAsync(request.TextDocument.Uri.GetFileSystemPath());

        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var content = await _documentStore.GetTextAsync(filePath, cancellationToken);
        if (content is null)
            return null;

        var tagResult = await _cursorResolver.ResolveTagAtPositionAsync(
            filePath,
            content,
            request.Position,
            cancellationToken);

        if (tagResult is null)
            return null;

        // Calculate the range for the tag name
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
}
