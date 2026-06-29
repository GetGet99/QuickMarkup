namespace QuickMarkup.Infra;

public sealed class DisposableAction(Action action) : IDisposable
{
    bool disposed;

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        action();
    }
}
