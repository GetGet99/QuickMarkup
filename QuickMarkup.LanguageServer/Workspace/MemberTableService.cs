using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Workspace;

sealed class MemberTableService : IMemberTableService
{
    readonly ICatalogService _catalog;
    readonly IQmuiDocumentStore _documentStore;
    readonly IFileProvider _fileProvider;

    QuickMarkupGeneratedMemberTable _table = QuickMarkupGeneratedMemberTable.Empty;

    public MemberTableService(ICatalogService catalog, IQmuiDocumentStore documentStore, IFileProvider fileProvider)
    {
        _catalog = catalog;
        _documentStore = documentStore;
        _fileProvider = fileProvider;
    }

    public QuickMarkupGeneratedMemberTable GetTable() => _table;

    public async Task RebuildAllAsync(Compilation enrichedCompilation, CancellationToken ct = default)
    {
        var members = new List<QuickMarkupGeneratedTypeMembers>();

        foreach (var entry in _catalog.Catalog.Entries)
        {
            if (entry.Kind != QuickMarkupDefinitionKind.QmuiFile || string.IsNullOrEmpty(entry.FilePath))
                continue;

            ct.ThrowIfCancellationRequested();

            try
            {
                var sfc = await GetSfcAsync(entry.FilePath);
                if (sfc?.ClassDeclaration is null)
                    continue;

                var analysis = QuickMarkupFileAnalyzer.Analyze(
                    sfc, entry.FilePath, entry.Namespace, enrichedCompilation, _table, failFast: true);

                if (analysis.GeneratedMembers is { } m)
                    members.Add(m);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[QuickMarkup] Failed to analyze {entry.FilePath}: {ex.Message}");
            }
        }

        _table = new QuickMarkupGeneratedMemberTable(members);
        TableUpdated?.Invoke(this, _table);
    }

    public async Task InvalidateTypeAsync(string filePath, Compilation enrichedCompilation, CancellationToken ct = default)
    {
        foreach (var entry in _catalog.Catalog.Entries)
        {
            if (string.Equals(entry.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                _table.RemoveType(entry.FullTypeName);
                break;
            }
        }

        if (!_catalog.Catalog.TryGetCachedAst(filePath, out var sfc) || sfc?.ClassDeclaration is null)
            return;

        var ns = sfc.Namespace?.Name ?? "";

        var analysis = QuickMarkupFileAnalyzer.Analyze(
            sfc, filePath, ns, enrichedCompilation, _table, failFast: true);

        if (analysis.GeneratedMembers is { } m)
            _table.UpdateType(m);

        TableUpdated?.Invoke(this, _table);
    }

    async Task<QuickMarkupSFC?> GetSfcAsync(string filePath)
    {
        var content = await _documentStore.GetTextAsync(filePath);
        content ??= _fileProvider.ReadAllText(filePath);
        return _catalog.Catalog.GetOrParse(filePath, content).Sfc;
    }

    public event EventHandler<QuickMarkupGeneratedMemberTable>? TableUpdated;
}
