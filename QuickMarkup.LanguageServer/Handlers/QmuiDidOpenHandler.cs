using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiDidOpenHandler : IDidOpenTextDocumentHandler
{
    readonly IQmuiDiagnosticService _diagnostics;
    readonly ILanguageServer _server;

    public QmuiDidOpenHandler(IQmuiDiagnosticService diagnostics, ILanguageServer server)
    {
        _diagnostics = diagnostics;
        _server = server;
    }

    public TextDocumentOpenRegistrationOptions GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentOpenRegistrationOptions();
    }

    public async Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var results = await _diagnostics.GetDiagnosticsAsync(
            request.TextDocument.Uri.GetFileSystemPath(),
            request.TextDocument.Text,
            cancellationToken
        );

        _server.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>(results)
        });

        return Unit.Value;
    }
}
