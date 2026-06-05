using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Workspace;

public sealed class QmuiWorkspaceService : IQmuiWorkspaceService, IDisposable
{
    readonly SemaphoreSlim _loadLock = new(1, 1);
    readonly IQmuiDocumentStore _documentStore;
    readonly IFileProvider _fileProvider;
    readonly ICompilationService _compilation;
    readonly ICatalogService _catalog;
    readonly IMemberTableService _members;
    readonly IFileWatcherService _watcher;

    string? _workspaceRoot;
    string? _currentProjectPath;
    List<string> _solutionProjects = [];
    volatile bool _isStale;
    bool _disposed;

    public bool IsLoaded => _compilation.Compilation is not null;
    public string? CurrentProjectPath => _currentProjectPath;
    public QuickMarkupWorkspaceCatalog Catalog => _catalog.Catalog;

    public QmuiWorkspaceService(
        IQmuiDocumentStore documentStore,
        IFileProvider fileProvider,
        ICompilationService compilation,
        ICatalogService catalog,
        IMemberTableService members,
        IFileWatcherService watcher)
    {
        _documentStore = documentStore;
        _fileProvider = fileProvider;
        _compilation = compilation;
        _catalog = catalog;
        _members = members;
        _watcher = watcher;

        _watcher.ExternalFileChanged += (_, _) => _isStale = true;
    }

    public async Task<bool> InitializeAsync(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
        _solutionProjects = ProjectFinder.FindSolutionProjects(workspaceRoot);

        var defaultProject = ProjectFinder.FindDefaultProject(workspaceRoot);
        if (defaultProject is not null)
            return await ReloadAsync(defaultProject);

        return false;
    }

    public async Task<bool> EnsureProjectForFileAsync(string qmuiFilePath)
    {
        if (_workspaceRoot is null)
            return false;

        await _loadLock.WaitAsync();
        try
        {
            if (_isStale && _currentProjectPath is not null)
            {
                await ReloadCoreAsync(_currentProjectPath);
                _isStale = false;
            }

            var projectPath = ProjectFinder.FindProjectForFile(qmuiFilePath, _workspaceRoot, _solutionProjects);
            if (projectPath is null)
                return false;

            if (string.Equals(_currentProjectPath, projectPath, StringComparison.OrdinalIgnoreCase) && IsLoaded)
                return true;

            return await ReloadCoreAsync(projectPath);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    async Task<bool> ReloadAsync(string csprojPath)
    {
        await _loadLock.WaitAsync();
        try
        {
            return await ReloadCoreAsync(csprojPath);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    async Task<bool> ReloadCoreAsync(string csprojPath)
    {
        if (!File.Exists(csprojPath))
        {
            _currentProjectPath = null;
            return false;
        }

        _currentProjectPath = csprojPath;
        try
        {
            var loaded = await _compilation.LoadProjectAsync(csprojPath, _workspaceRoot!);
            if (!loaded)
            {
                _currentProjectPath = null;
                return false;
            }

            var enriched = await _compilation.GetEnrichedCompilationAsync();
            if (enriched is null)
                return false;

            await _members.RebuildAllAsync(enriched);
            _watcher.Start(Path.GetDirectoryName(csprojPath)!);
            return true;
        }
        catch
        {
            _currentProjectPath = null;
            return false;
        }
    }

    public async Task OnQmuiContentChangedAsync(string filePath, string newContent, CancellationToken ct = default)
    {
        if (_compilation.Compilation is null)
            return;

        _catalog.OnContentChanged(filePath, newContent);
        _compilation.InvalidateEnrichment();

        await _loadLock.WaitAsync(ct);
        try
        {
            var enriched = await _compilation.GetEnrichedCompilationAsync(ct);
            if (enriched is not null)
                await _members.InvalidateTypeAsync(filePath, enriched, ct);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public ValueTask<Compilation?> GetEnrichedCompilationAsync(CancellationToken ct = default)
        => _compilation.GetEnrichedCompilationAsync(ct);

    public QuickMarkupGeneratedMemberTable GetGeneratedMemberTable()
        => _members.GetTable();

    public IReadOnlyList<QuickMarkupTypeEntry> GetAllQmuiEntries()
        => _catalog.Catalog.Entries;

    public bool TryGetQmuiEntry(string fullTypeName, out QuickMarkupTypeEntry entry)
        => _catalog.Catalog.TryGetEntry(fullTypeName, out entry!);

    public IEnumerable<QuickMarkupTypeEntry> GetQmuiEntriesByShortName(string shortName)
        => _catalog.Catalog.GetEntriesByShortName(shortName);

    public string? FindFilePathForTypeName(string fullTypeName)
        => _catalog.Catalog.FindFilePathForTypeName(fullTypeName);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.Dispose();
        _compilation.Dispose();
        _loadLock.Dispose();
    }
}
