using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Handlers;

class QmuiDidChangeHandler : IDidChangeTextDocumentHandler
{
    readonly IQmuiDiagnosticService _diagnostics;
    readonly IServiceProvider _serviceProvider;
    readonly ConcurrentDictionary<DocumentUri, CancellationTokenSource> _debounceTokens = new();

    public QmuiDidChangeHandler(IQmuiDiagnosticService diagnostics, IServiceProvider serviceProvider)
    {
        _diagnostics = diagnostics;
        _serviceProvider = serviceProvider;
    }

    public TextDocumentChangeRegistrationOptions GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentChangeRegistrationOptions
        {
            SyncKind = TextDocumentSyncKind.Full
        };
    }

    public Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        if (_debounceTokens.TryRemove(request.TextDocument.Uri, out var previous))
        {
            previous.CancelAsync();
            previous.Dispose();
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _debounceTokens.TryAdd(request.TextDocument.Uri, cts);

        _ = DebounceAndPublishAsync(request, cts);

        return Unit.Task;
    }

    async Task DebounceAndPublishAsync(DidChangeTextDocumentParams request, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(300, cts.Token);

            var server = _serviceProvider.GetRequiredService<ILanguageServer>();
            var results = await _diagnostics.GetDiagnosticsAsync(
                request.TextDocument.Uri.GetFileSystemPath(),
                request.ContentChanges.First().Text,
                cts.Token
            );

            server.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = request.TextDocument.Uri,
                Diagnostics = new Container<Diagnostic>(results)
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
            _debounceTokens.TryRemove(KeyValuePair.Create(request.TextDocument.Uri, cts));
        }
    }
}
