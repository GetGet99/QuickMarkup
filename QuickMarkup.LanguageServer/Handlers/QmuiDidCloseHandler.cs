using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiDidCloseHandler : IDidCloseTextDocumentHandler
{
    readonly IQmuiDocumentStore _documentStore;
    readonly IServiceProvider _serviceProvider;

    public QmuiDidCloseHandler(IQmuiDocumentStore documentStore, IServiceProvider serviceProvider)
    {
        _documentStore = documentStore;
        _serviceProvider = serviceProvider;
    }

    public TextDocumentCloseRegistrationOptions GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentCloseRegistrationOptions();
    }

    public async Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        await _documentStore.RemoveAsync(filePath, cancellationToken).ConfigureAwait(false);

        var server = _serviceProvider.GetRequiredService<ILanguageServer>();
        server.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Value;
    }
}
