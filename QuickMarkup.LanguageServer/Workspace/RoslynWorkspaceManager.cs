using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Workspace;

public class RoslynWorkspaceManager : IRoslynWorkspaceManager, IDisposable
{
    static readonly object _msBuildLock = new();
    static bool _msBuildRegistered;

    readonly SemaphoreSlim _loadLock = new(1, 1);
    FileSystemWatcher? _watcher;
    bool _disposed;
    string? _workspaceRoot;
    List<string> _solutionProjects = [];

    public bool IsLoaded { get; private set; }
    public string? CurrentProjectPath { get; private set; }
    public Compilation? Compilation { get; private set; }

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
            return await TryLoadAsync(defaultProject);

        return false;
    }

    public async Task<bool> EnsureProjectForFileAsync(string qmuiFilePath)
    {
        if (_workspaceRoot is null)
            return false;

        var projectPath = ProjectFinder.FindProjectForFile(qmuiFilePath, _workspaceRoot, _solutionProjects);
        if (projectPath is null)
            return false;

        if (string.Equals(CurrentProjectPath, projectPath, StringComparison.OrdinalIgnoreCase) && IsLoaded)
            return true;

        return await TryLoadAsync(projectPath);
    }

    public async Task<bool> TryLoadAsync(string csprojPath)
    {
        ArgumentNullException.ThrowIfNull(csprojPath);

        await _loadLock.WaitAsync();
        try
        {
            if (!File.Exists(csprojPath))
            {
                Compilation = null;
                IsLoaded = false;
                return false;
            }

            CurrentProjectPath = csprojPath;
            EnsureMSBuildRegistered();
            using var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(csprojPath);
            Compilation = await project.GetCompilationAsync();
            IsLoaded = Compilation is not null;
            return IsLoaded;
        }
        catch
        {
            Compilation = null;
            IsLoaded = false;
            return false;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    void OnProjectFileChanged(object sender, FileSystemEventArgs e)
    {
        var path = CurrentProjectPath;
        if (path is not null)
            _ = TryLoadAsync(path);
    }

    public void WatchProjectChanges(string csprojPath)
    {
        _watcher?.Dispose();

        var dir = Path.GetDirectoryName(csprojPath);
        if (dir is null) return;

        _watcher = new FileSystemWatcher(dir, "*.csproj")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnProjectFileChanged;
        _watcher.Created += OnProjectFileChanged;
        _watcher.Deleted += OnProjectFileChanged;
        _watcher.Renamed += OnProjectFileChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
        _loadLock.Dispose();
    }
}
