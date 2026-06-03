using System.Collections.Concurrent;

namespace QuickMarkup.LanguageServer.Contracts;

/// <summary>
/// Stores the latest content of .qmui documents keyed by file path.
/// Updated by didOpen/didChange/didClose handlers.
/// </summary>
public class QmuiDocumentStore : IQmuiDocumentStore
{
    private readonly ConcurrentDictionary<string, string> _documents = new();

    public Task<string?> GetTextAsync(string filePath, CancellationToken ct = default)
    {
        _documents.TryGetValue(filePath, out var content);
        return Task.FromResult(content);
    }

    public Task UpdateTextAsync(string filePath, string content, CancellationToken ct = default)
    {
        _documents[filePath] = content;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string filePath, CancellationToken ct = default)
    {
        _documents.TryRemove(filePath, out _);
        return Task.CompletedTask;
    }
}
