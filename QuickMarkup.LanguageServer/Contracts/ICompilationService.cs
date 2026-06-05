using Microsoft.CodeAnalysis;

namespace QuickMarkup.LanguageServer.Contracts;

public interface ICompilationService : IDisposable
{
    Compilation? Compilation { get; }

    Compilation? EnrichedCompilation { get; }

    Task<bool> LoadProjectAsync(string csprojPath, string workspaceRoot, CancellationToken ct = default);

    ValueTask<Compilation?> GetEnrichedCompilationAsync(CancellationToken ct = default);

    void InvalidateEnrichment();

    event EventHandler? CompilationUpdated;
}
