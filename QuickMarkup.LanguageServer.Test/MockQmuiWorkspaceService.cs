using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Test;

internal class MockQmuiWorkspaceService : IQmuiWorkspaceService
{
    public bool IsLoaded { get; set; }
    public string? CurrentProjectPath { get; set; }
    public Compilation? Compilation { get; set; }
    public IReadOnlyList<QuickMarkupTypeEntry> QmuiEntries { get; set; } = [];
    public QuickMarkupWorkspaceCatalog Catalog { get; set; } = new();

    public Task<bool> InitializeAsync(string workspaceRoot) => Task.FromResult(true);
    public Task<bool> EnsureProjectForFileAsync(string qmuiFilePath) => Task.FromResult(true);

    public ValueTask<Compilation?> GetEnrichedCompilationAsync(CancellationToken ct = default)
        => new(Compilation);

    public QuickMarkupGeneratedMemberTable GetGeneratedMemberTable()
        => QuickMarkupGeneratedMemberTable.Empty;

    public bool TryGetQmuiEntry(string fullTypeName, out QuickMarkupTypeEntry entry)
    {
        foreach (var e in QmuiEntries)
        {
            if (e.FullTypeName == fullTypeName)
            {
                entry = e;
                return true;
            }
        }
        entry = default;
        return false;
    }

    public IEnumerable<QuickMarkupTypeEntry> GetQmuiEntriesByShortName(string shortName)
        => QmuiEntries.Where(e => e.ShortName == shortName);

    public IReadOnlyList<QuickMarkupTypeEntry> GetAllQmuiEntries()
        => QmuiEntries;

    public string? FindFilePathForTypeName(string fullTypeName)
    {
        foreach (var entry in QmuiEntries)
        {
            if (entry.FullTypeName == fullTypeName)
                return entry.FilePath;
        }
        return null;
    }
}
