using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.LanguageServer.Navigation;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiDefinitionHandler : IDefinitionHandler
{
    readonly IQmuiDocumentStore _documentStore;
    readonly MarkupCursorResolver _cursorResolver;
    readonly SymbolLocationResolver _locationResolver;
    readonly IServiceProvider _serviceProvider;

    public QmuiDefinitionHandler(
        IQmuiDocumentStore documentStore,
        MarkupCursorResolver cursorResolver,
        SymbolLocationResolver locationResolver,
        IServiceProvider serviceProvider)
    {
        _documentStore = documentStore;
        _cursorResolver = cursorResolver;
        _locationResolver = locationResolver;
        _serviceProvider = serviceProvider;
    }

    public DefinitionRegistrationOptions GetRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DefinitionRegistrationOptions();
    }

    public async Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
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

        var location = _locationResolver.GetDefinitionLocation(tagResult, filePath);
        if (location is null)
            return null;

        return new LocationOrLocationLinks(location);
    }
}
