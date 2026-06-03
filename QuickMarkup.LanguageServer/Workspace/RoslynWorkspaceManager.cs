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

    public bool IsLoaded { get; private set; }
    public Compilation? Compilation { get; private set; }
    public event Action? CompilationChanged;

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

    void OnProjectFileChanged(object sender, FileSystemEventArgs e)
    {
        CompilationChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
        _loadLock.Dispose();
    }
}
