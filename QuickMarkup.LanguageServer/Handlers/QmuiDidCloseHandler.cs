using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiDidCloseHandler : IDidCloseTextDocumentHandler
{
    readonly ILanguageServer _server;

    public QmuiDidCloseHandler(ILanguageServer server)
    {
        _server = server;
    }

    public TextDocumentCloseRegistrationOptions GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentCloseRegistrationOptions();
    }

    public Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        _server.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Task;
    }
}
