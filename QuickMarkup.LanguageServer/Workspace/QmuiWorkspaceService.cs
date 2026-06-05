using System.Collections.Immutable;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Workspace;

/// <summary>
/// Centralized workspace service owning the Compilation, .qmui type catalog,
/// and generated member table. All cache invalidation flows through this service.
/// </summary>
public class QmuiWorkspaceService : IQmuiWorkspaceService, IDisposable
{
    static readonly object _msBuildLock = new();
    static bool _msBuildRegistered;

    readonly SemaphoreSlim _loadLock = new(1, 1);
    readonly IQmuiDocumentStore _documentStore;
    readonly IFileProvider _fileProvider;
    readonly QuickMarkupWorkspaceCatalog _catalog = new();

    Compilation? _compilation;
    Compilation? _enrichedCompilation;
    string? _workspaceRoot;
    string? _currentProjectPath;
    List<string> _solutionProjects = [];
    volatile bool _isStale;

    QuickMarkupGeneratedMemberTable _generatedMemberTable = QuickMarkupGeneratedMemberTable.Empty;

    FileSystemWatcher? _watcher;
    bool _disposed;

    public bool IsLoaded => _compilation is not null;
    public string? CurrentProjectPath => _currentProjectPath;
    public QuickMarkupWorkspaceCatalog Catalog => _catalog;

    public QmuiWorkspaceService(IQmuiDocumentStore documentStore, IFileProvider fileProvider)
    {
        _documentStore = documentStore;
        _fileProvider = fileProvider;
    }

    static void EnsureMSBuildRegistered()
    {
        if (_msBuildRegistered) return;
        lock (_msBuildLock)
        {
            if (_msBuildRegistered) return;
            MSBuildLocator.RegisterDefaults();
            _msBuildRegistered = true;
        }
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

        if (_isStale && _currentProjectPath is not null)
        {
            await ReloadAsync(_currentProjectPath);
            _isStale = false;
        }

        var projectPath = ProjectFinder.FindProjectForFile(qmuiFilePath, _workspaceRoot, _solutionProjects);
        if (projectPath is null)
            return false;

        if (string.Equals(_currentProjectPath, projectPath, StringComparison.OrdinalIgnoreCase) && IsLoaded)
            return true;

        return await ReloadAsync(projectPath);
    }

    /// <summary>
    /// Full reload: rebuild Compilation, catalog, and GeneratedMemberTable from scratch.
    /// </summary>
    async Task<bool> ReloadAsync(string csprojPath)
    {
        ArgumentNullException.ThrowIfNull(csprojPath);

        await _loadLock.WaitAsync();
        try
        {
            if (!File.Exists(csprojPath))
            {
                _compilation = null;
                _enrichedCompilation = null;
                _currentProjectPath = null;
                return false;
            }

            _currentProjectPath = csprojPath;
            EnsureMSBuildRegistered();

            using var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(csprojPath);
            _compilation = await project.GetCompilationAsync();
            if (_compilation is null)
                return false;

            // Build catalog from AdditionalDocuments
            BuildCatalog(project);

            // Build GeneratedMemberTable using enriched compilation
            // (so .qmui types from other files are resolvable)
            _enrichedCompilation = await GetEnrichedCompilationAsync();
            _generatedMemberTable = GeneratedMemberTableBuilder.Build(
                _catalog, _documentStore, _fileProvider, _enrichedCompilation ?? _compilation);

            // Wire up file watcher
            WatchProjectChanges(csprojPath);

            return true;
        }
        catch
        {
            _compilation = null;
            _enrichedCompilation = null;
            return false;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    void BuildCatalog(Microsoft.CodeAnalysis.Project project)
    {
        // Build from AdditionalDocuments first
        bool hasAdditionalDocs = false;
        foreach (var doc in project.AdditionalDocuments)
        {
            if (!doc.Name.EndsWith(".qmui", StringComparison.OrdinalIgnoreCase))
                continue;

            hasAdditionalDocs = true;
            try
            {
                var filePath = doc.FilePath ?? doc.Name;
                var content = GetQmuiContent(filePath);
                _catalog.AddOrUpdateQmuiFile(filePath, content);
            }
            catch
            {
                // Skip unparsable files
            }
        }

        // Fallback: if no AdditionalDocuments found, glob filesystem
        if (!hasAdditionalDocs && !string.IsNullOrEmpty(_workspaceRoot) && _fileProvider.DirectoryExists(_workspaceRoot))
        {
            var qmuiFiles = _fileProvider.GetFiles(_workspaceRoot, "*.qmui", recursive: true);
            foreach (var file in qmuiFiles)
            {
                try
                {
                    var content = GetQmuiContent(file);
                    _catalog.AddOrUpdateQmuiFile(file, content);
                }
                catch
                {
                    // Skip unparsable files
                }
            }
        }
    }

    /// <summary>
    /// Gets .qmui content from the document store (open files) or disk (closed files).
    /// </summary>
    string GetQmuiContent(string filePath)
    {
        var storeTask = _documentStore.GetTextAsync(filePath);
        if (storeTask.IsCompletedSuccessfully && storeTask.Result is { } inMemory)
            return inMemory;
        return _fileProvider.ReadAllText(filePath);
    }

    /// <summary>
    /// Handles a .qmui document change: incrementally updates the catalog entry
    /// and GeneratedMemberTable entry for the changed type only.
    /// </summary>
    public void OnQmuiContentChanged(string filePath, string newContent)
    {
        if (_compilation is null)
            return;

        // Remove old type from member table before updating catalog
        RemoveTypeFromMemberTable(filePath);

        // Update catalog (parses once, caches AST)
        _catalog.AddOrUpdateQmuiFile(filePath, newContent);

        // Rebuild GeneratedTypeMembers for this type using cached AST
        if (!_catalog.TryGetCachedAst(filePath, out var sfc) || sfc?.ClassDeclaration is null)
            return;

        var ns = sfc.Namespace?.Name ?? "";
        var typeName = sfc.ClassDeclaration.Name;
        var fullTypeName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";

        var target = new QuickMarkupTargetContext(
            Namespace: ns,
            TypeName: typeName,
            FullTypeName: fullTypeName,
            FileName: filePath,
            AttributeLocation: default,
            AttributeLineSpan: default);

        var members = QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(
            new QuickMarkupParsedAttribute(target, sfc), _enrichedCompilation ?? _compilation, CancellationToken.None);
        if (members is { } m)
            _generatedMemberTable.UpdateType(m);
    }

    void RemoveTypeFromMemberTable(string filePath)
    {
        foreach (var entry in _catalog.Entries)
        {
            if (string.Equals(entry.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                _generatedMemberTable.RemoveType(entry.FullTypeName);
                break;
            }
        }
    }

    /// <summary>
    /// Notifies the service that external files (.cs, .csproj) have changed.
    /// Sets the stale flag so the next .qmui access triggers a reload.
    /// </summary>
    void OnExternalFileChanged()
    {
        _isStale = true;
    }

    public async Task<Compilation?> GetEnrichedCompilationAsync(CancellationToken ct = default)
    {
        var compilation = _compilation;
        if (compilation is null)
            return null;

        var astSnapshot = _catalog.CachedAst;
        foreach (var (filePath, sfc) in astSnapshot)
        {
            if (sfc.ClassDeclaration is null)
                continue;

            try
            {
                var ns = sfc.Namespace?.Name ?? "";
                var typeName = sfc.ClassDeclaration.Name;
                var fullTypeName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";

                var target = new QuickMarkupTargetContext(
                    Namespace: ns,
                    TypeName: typeName,
                    FullTypeName: fullTypeName,
                    FileName: filePath,
                    AttributeLocation: default,
                    AttributeLineSpan: default);

                compilation = QuickMarkupCompilationEnricher.EnsureTypeSymbolInCompilation(target, sfc, compilation);
            }
            catch
            {
                // Skip problematic files
            }
        }

        return compilation;
    }

    public QuickMarkupGeneratedMemberTable GetGeneratedMemberTable()
        => _generatedMemberTable;

    public IReadOnlyList<QuickMarkupTypeEntry> GetAllQmuiEntries()
        => _catalog.Entries;

    public bool TryGetQmuiEntry(string fullTypeName, out QuickMarkupTypeEntry entry)
        => _catalog.TryGetEntry(fullTypeName, out entry);

    public IEnumerable<QuickMarkupTypeEntry> GetQmuiEntriesByShortName(string shortName)
        => _catalog.GetEntriesByShortName(shortName);

    public string? FindFilePathForTypeName(string fullTypeName)
        => _catalog.FindFilePathForTypeName(fullTypeName);

    void WatchProjectChanges(string csprojPath)
    {
        _watcher?.Dispose();

        var dir = Path.GetDirectoryName(csprojPath);
        if (dir is null) return;

        _watcher = new FileSystemWatcher(dir, "*.*")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
            IncludeSubdirectories = true
        };

        _watcher.Changed += (_, _) => OnExternalFileChanged();
        _watcher.Created += (_, _) => OnExternalFileChanged();
        _watcher.Deleted += (_, _) => OnExternalFileChanged();
        _watcher.Renamed += (_, _) => OnExternalFileChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
        _loadLock.Dispose();
    }

}
