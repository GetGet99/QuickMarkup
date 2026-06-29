namespace QuickMarkup.Infra;

public sealed class ReactiveScope : IDisposable
{
    readonly List<IDisposable> disposables = [];
    bool disposed;

    public void Add(RefEffect effect)
        => Add((IDisposable)effect);

    public void Add(IDisposable disposable)
    {
        if (disposed)
        {
            disposable.Dispose();
            return;
        }

        disposables.Add(disposable);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        foreach (var disposable in disposables)
            disposable.Dispose();

        disposables.Clear();
    }
}
