using System.Collections.Concurrent;

namespace QuickMarkup.LanguageServer.Contracts;

/// <summary>
/// Stores the latest content of .qmui documents keyed by file path.
/// Updated by didOpen/didChange/didClose handlers.
/// </summary>
public class QmuiDocumentStore : IQmuiDocumentStore
{
    private readonly ConcurrentDictionary<string, string> _documents = new();

    public ValueTask<string?> GetTextAsync(string filePath, CancellationToken ct = default)
    {
        _documents.TryGetValue(filePath, out var content);
        return new ValueTask<string?>(content);
    }

    public ValueTask UpdateTextAsync(string filePath, string content, CancellationToken ct = default)
    {
        _documents[filePath] = content;
        return default;
    }

    public ValueTask RemoveAsync(string filePath, CancellationToken ct = default)
    {
        _documents.TryRemove(filePath, out _);
        return default;
    }
}
