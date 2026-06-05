using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiDidOpenHandler : IDidOpenTextDocumentHandler
{
    readonly IQmuiDiagnosticService _diagnostics;
    readonly IQmuiDocumentStore _documentStore;
    readonly IQmuiWorkspaceService _workspace;
    readonly IServiceProvider _serviceProvider;
    ILanguageServer LanguageServer => field ??= _serviceProvider.GetRequiredService<ILanguageServer>();

    public QmuiDidOpenHandler(IQmuiDiagnosticService diagnostics, IQmuiDocumentStore documentStore, IQmuiWorkspaceService workspace, IServiceProvider serviceProvider)
    {
        _diagnostics = diagnostics;
        _documentStore = documentStore;
        _workspace = workspace;
        _serviceProvider = serviceProvider;
    }

    public TextDocumentOpenRegistrationOptions GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentOpenRegistrationOptions();
    }

    public async Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        
        await _documentStore.UpdateTextAsync(filePath, request.TextDocument.Text, cancellationToken).ConfigureAwait(false);

        await _workspace.EnsureProjectForFileAsync(filePath);

        var results = await _diagnostics.GetDiagnosticsAsync(
            request.TextDocument.Uri.GetFileSystemPath(),
            request.TextDocument.Text,
            cancellationToken
        );

        LanguageServer.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>(results)
        });

        return Unit.Value;
    }
}
