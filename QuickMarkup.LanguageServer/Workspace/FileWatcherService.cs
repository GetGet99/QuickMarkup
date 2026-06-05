using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Workspace;

sealed class FileWatcherService : IFileWatcherService
{
    FileSystemWatcher? _watcher;
    bool _disposed;

    public void Start(string directoryPath)
    {
        Stop();

        _watcher = new FileSystemWatcher(directoryPath, "*.*")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
            IncludeSubdirectories = true
        };

        _watcher.Changed += OnWatcherEvent;
        _watcher.Created += OnWatcherEvent;
        _watcher.Deleted += OnWatcherEvent;
        _watcher.Renamed += OnWatcherEvent;
    }

    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        ExternalFileChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<EventArgs>? ExternalFileChanged;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
