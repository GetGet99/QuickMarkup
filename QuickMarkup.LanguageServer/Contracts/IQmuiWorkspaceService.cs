using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;

namespace QuickMarkup.LanguageServer.Contracts;

/// <summary>
/// Centralized workspace service that owns the Compilation, .qmui type catalog,
/// and generated member table. All cache invalidation flows through this service.
/// </summary>
public interface IQmuiWorkspaceService
{
    bool IsLoaded { get; }
    string? CurrentProjectPath { get; }

    /// <summary>
    /// Initializes the workspace by finding and loading the default .csproj.
    /// </summary>
    Task<bool> InitializeAsync(string workspaceRoot);

    /// <summary>
    /// Ensures the correct project is loaded for the given .qmui file.
    /// If the cache is stale (external files changed), triggers a reload.
    /// </summary>
    Task<bool> EnsureProjectForFileAsync(string qmuiFilePath);

    /// <summary>
    /// Gets the compilation enriched with dummy type symbols for all .qmui files.
    /// Reads content from the document store for open files, disk for closed files.
    /// </summary>
    Task<Compilation?> GetEnrichedCompilationAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current generated member table (ref properties, computed properties, component outputs).
    /// </summary>
    QuickMarkupGeneratedMemberTable GetGeneratedMemberTable();

    /// <summary>
    /// Gets all .qmui catalog entries.
    /// </summary>
    IReadOnlyList<QuickMarkupTypeEntry> GetAllQmuiEntries();

    /// <summary>
    /// Tries to find a .qmui type catalog entry by full type name.
    /// </summary>
    bool TryGetQmuiEntry(string fullTypeName, out QuickMarkupTypeEntry entry);

    /// <summary>
    /// Gets all .qmui catalog entries matching a short type name (for tag resolution).
    /// </summary>
    IEnumerable<QuickMarkupTypeEntry> GetQmuiEntriesByShortName(string shortName);

    /// <summary>
    /// Finds the file path for a given full type name.
    /// </summary>
    string? FindFilePathForTypeName(string fullTypeName);
}
