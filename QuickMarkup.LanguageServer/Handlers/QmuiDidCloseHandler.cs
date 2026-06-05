using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiDidCloseHandler : IDidCloseTextDocumentHandler
{
    readonly IQmuiDocumentStore _documentStore;
    readonly ILanguageServer _languageServer;

    public QmuiDidCloseHandler(IQmuiDocumentStore documentStore, ILanguageServer languageServer)
    {
        _documentStore = documentStore;
        _languageServer = languageServer;
    }

    public TextDocumentCloseRegistrationOptions GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentCloseRegistrationOptions();
    }

    public async Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        await _documentStore.RemoveAsync(filePath, cancellationToken).ConfigureAwait(false);

        _languageServer.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Value;
    }
}
