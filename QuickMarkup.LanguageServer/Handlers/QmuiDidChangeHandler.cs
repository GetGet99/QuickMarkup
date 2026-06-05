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
    readonly IQmuiDocumentStore _documentStore;
    readonly IServiceProvider _serviceProvider;
    readonly ConcurrentDictionary<DocumentUri, CancellationTokenSource> _debounceTokens = new();

    public QmuiDidChangeHandler(IQmuiDiagnosticService diagnostics, IQmuiDocumentStore documentStore, IServiceProvider serviceProvider)
    {
        _diagnostics = diagnostics;
        _documentStore = documentStore;
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
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var content = request.ContentChanges.First().Text;

        FireAndForget(() => _documentStore.UpdateTextAsync(filePath, content, cancellationToken),
            nameof(_documentStore.UpdateTextAsync));

        CancelPreviousDebounce(request.TextDocument.Uri);

        var cts = new CancellationTokenSource();
        _debounceTokens.TryAdd(request.TextDocument.Uri, cts);

        _ = DebounceAndPublishAsync(request, cts);

        return Unit.Task;
    }

    static async void FireAndForget(Func<ValueTask> taskFactory, string operationName)
    {
        try
        {
            await taskFactory().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(
                $"[QuickMarkup] Error in {operationName}: {ex.Message}").ConfigureAwait(false);
        }
    }

    void CancelPreviousDebounce(DocumentUri uri)
    {
        if (_debounceTokens.TryRemove(uri, out var previous))
        {
            try
            {
                previous.Cancel();
                previous.Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[QuickMarkup] Error canceling previous debounce: {ex.Message}");
            }
        }
    }

    async Task DebounceAndPublishAsync(DidChangeTextDocumentParams request, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(DebounceDelayMs, cts.Token);

            var workspace = _serviceProvider.GetRequiredService<IQmuiWorkspaceService>();
            await workspace.EnsureProjectForFileAsync(request.TextDocument.Uri.GetFileSystemPath());

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
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[QuickMarkup] Error in change handler for {request.TextDocument.Uri}: {ex.Message}").ConfigureAwait(false);
        }
        finally
        {
            cts.Dispose();
            _debounceTokens.TryRemove(request.TextDocument.Uri, out _);
        }
    }

    const int DebounceDelayMs = 300;
}
