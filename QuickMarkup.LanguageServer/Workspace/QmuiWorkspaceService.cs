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

    Compilation? _compilation;
    Compilation? _enrichedCompilation;
    string? _workspaceRoot;
    string? _currentProjectPath;
    List<string> _solutionProjects = [];
    volatile bool _isStale;

    // Catalog: .qmui type entries keyed by full type name
    ImmutableArray<QuickMarkupTypeEntry> _qmuiEntries = ImmutableArray<QuickMarkupTypeEntry>.Empty;
    // Reverse map: file path -> full type name (for incremental updates)
    readonly Dictionary<string, string> _filePathToTypeName = new(StringComparer.OrdinalIgnoreCase);

    QuickMarkupGeneratedMemberTable _generatedMemberTable = QuickMarkupGeneratedMemberTable.Empty;

    FileSystemWatcher? _watcher;
    bool _disposed;

    public bool IsLoaded => _compilation is not null;
    public string? CurrentProjectPath => _currentProjectPath;

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
            BuildCatalogFromProject(project);

            // Fallback: if no AdditionalDocuments found, glob filesystem
            if (_qmuiEntries.Length == 0)
                BuildCatalogFromFilesystem();

            // Build GeneratedMemberTable using enriched compilation
            // (so .qmui types from other files are resolvable)
            _enrichedCompilation = await GetEnrichedCompilationAsync();
            _generatedMemberTable = GeneratedMemberTableBuilder.Build(
                _qmuiEntries, _fileProvider, _enrichedCompilation ?? _compilation);

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

    void BuildCatalogFromProject(Microsoft.CodeAnalysis.Project project)
    {
        var entries = ImmutableArray.CreateBuilder<QuickMarkupTypeEntry>();
        _filePathToTypeName.Clear();

        foreach (var doc in project.AdditionalDocuments)
        {
            if (!doc.Name.EndsWith(".qmui", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var filePath = doc.FilePath ?? doc.Name;
                var content = GetQmuiContent(filePath);
                var sfc = QuickMarkupProviderExtension.Parse(content);
                if (sfc?.ClassDeclaration is null)
                    continue;

                var entry = CreateQmuiEntry(filePath, sfc);
                entries.Add(entry);
                _filePathToTypeName[filePath] = entry.FullTypeName;
            }
            catch
            {
                // Skip unparsable files
            }
        }

        _qmuiEntries = entries.ToImmutable();
    }

    void BuildCatalogFromFilesystem()
    {
        if (string.IsNullOrEmpty(_workspaceRoot) || !_fileProvider.DirectoryExists(_workspaceRoot))
            return;

        var entries = ImmutableArray.CreateBuilder<QuickMarkupTypeEntry>();
        _filePathToTypeName.Clear();

        var qmuiFiles = _fileProvider.GetFiles(_workspaceRoot, "*.qmui", recursive: true);
        foreach (var file in qmuiFiles)
        {
            try
            {
                var content = GetQmuiContent(file);
                var sfc = QuickMarkupProviderExtension.Parse(content);
                if (sfc?.ClassDeclaration is null)
                    continue;

                var entry = CreateQmuiEntry(file, sfc);
                entries.Add(entry);
                _filePathToTypeName[file] = entry.FullTypeName;
            }
            catch
            {
                // Skip unparsable files
            }
        }

        _qmuiEntries = entries.ToImmutable();
    }

    static QuickMarkupTypeEntry CreateQmuiEntry(string filePath, QuickMarkupSFC sfc)
    {
        var ns = sfc.Namespace?.Name ?? "";
        var typeName = sfc.ClassDeclaration!.Name;
        var fullTypeName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";

        return new QuickMarkupTypeEntry(
            FullTypeName: fullTypeName,
            ShortName: typeName,
            Namespace: ns,
            Usings: string.Join(" ", sfc.Usings),
            Kind: QuickMarkupDefinitionKind.QmuiFile,
            FilePath: filePath,
            NameSpan: null);
    }

    /// <summary>
    /// Gets .qmui content from the document store (open files) or disk (closed files).
    /// </summary>
    string GetQmuiContent(string filePath)
    {
        var task = _documentStore.GetTextAsync(filePath);
        if (task.IsCompleted && task.Result is { } inMemory)
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

        // Parse new content
        var sfc = QuickMarkupProviderExtension.Parse(newContent);
        if (sfc?.ClassDeclaration is null)
            return;

        var newEntry = CreateQmuiEntry(filePath, sfc);

        // Remove old entry from GeneratedMemberTable if type name changed
        if (_filePathToTypeName.TryGetValue(filePath, out var oldFullName))
        {
            _generatedMemberTable.RemoveType(oldFullName);
        }

        // Update catalog entry
        UpdateCatalogEntry(newEntry);

        // Rebuild GeneratedTypeMembers for this type
        var target = new QuickMarkupTargetContext(
            Namespace: newEntry.Namespace,
            TypeName: newEntry.ShortName,
            FullTypeName: newEntry.FullTypeName,
            FileName: newEntry.FilePath,
            AttributeLocation: default,
            AttributeLineSpan: default);

        var members = QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(
            new QuickMarkupParsedAttribute(target, sfc), _enrichedCompilation ?? _compilation, CancellationToken.None);
        if (members is { } m)
            _generatedMemberTable.UpdateType(m);

        // Update reverse map
        _filePathToTypeName[filePath] = newEntry.FullTypeName;
    }

    void UpdateCatalogEntry(QuickMarkupTypeEntry newEntry)
    {
        var builder = _qmuiEntries.ToBuilder();

        // Remove old entry for this file path
        for (int i = builder.Count - 1; i >= 0; i--)
        {
            if (string.Equals(builder[i].FilePath, newEntry.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                builder.RemoveAt(i);
                break;
            }
        }

        builder.Add(newEntry);
        _qmuiEntries = builder.ToImmutable();
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

        foreach (var entry in _qmuiEntries)
        {
            if (entry.Kind != QuickMarkupDefinitionKind.QmuiFile || string.IsNullOrEmpty(entry.FilePath))
                continue;

            try
            {
                var content = GetQmuiContent(entry.FilePath);
                var sfc = QuickMarkupProviderExtension.Parse(content);
                if (sfc?.ClassDeclaration is null)
                    continue;

                var target = new QuickMarkupTargetContext(
                    Namespace: entry.Namespace,
                    TypeName: entry.ShortName,
                    FullTypeName: entry.FullTypeName,
                    FileName: entry.FilePath,
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
        => _qmuiEntries;

    public bool TryGetQmuiEntry(string fullTypeName, out QuickMarkupTypeEntry entry)
    {
        foreach (var e in _qmuiEntries)
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
        => _qmuiEntries.Where(e => e.ShortName == shortName);

    public string? FindFilePathForTypeName(string fullTypeName)
    {
        foreach (var entry in _qmuiEntries)
        {
            if (entry.FullTypeName == fullTypeName)
                return entry.FilePath;
        }
        return null;
    }

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
        _watcher?.Dispose();
        _loadLock.Dispose();
    }

}
