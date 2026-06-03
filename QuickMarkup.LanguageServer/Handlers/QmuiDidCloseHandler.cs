using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiDidCloseHandler : IDidCloseTextDocumentHandler
{
    readonly IServiceProvider _serviceProvider;

    public QmuiDidCloseHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public TextDocumentCloseRegistrationOptions GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentCloseRegistrationOptions();
    }

    public Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        var server = _serviceProvider.GetRequiredService<ILanguageServer>();
        server.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Task;
    }
}
