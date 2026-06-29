namespace QuickMarkup.LanguageServer.Contracts;

/// <summary>
/// Stores the latest content of .qmui documents keyed by file path.
/// Updated by didOpen/didChange/didClose handlers.
/// </summary>
public interface IQmuiDocumentStore
{
    /// <summary>
    /// Gets the content of a document, or null if not tracked.
    /// </summary>
    ValueTask<string?> GetTextAsync(string filePath, CancellationToken ct = default);
    
    /// <summary>
    /// Updates the content of a document.
    /// </summary>
    ValueTask UpdateTextAsync(string filePath, string content, CancellationToken ct = default);
    
    /// <summary>
    /// Removes a document from the store.
    /// </summary>
    ValueTask RemoveAsync(string filePath, CancellationToken ct = default);
}