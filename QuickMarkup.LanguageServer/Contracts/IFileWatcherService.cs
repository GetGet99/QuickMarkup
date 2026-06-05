namespace QuickMarkup.LanguageServer.Contracts;

public interface IFileWatcherService : IDisposable
{
    void Start(string directoryPath);

    void Stop();

    event EventHandler<EventArgs>? ExternalFileChanged;
}
