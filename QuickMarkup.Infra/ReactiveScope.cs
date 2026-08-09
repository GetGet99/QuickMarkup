namespace QuickMarkup.Infra;

public sealed class ReactiveScope : IDisposable
{
    static long ScopeSequence;

    readonly List<IDisposable> disposables = [];
    bool disposed;

    /// <summary>
    /// The structural scope this scope belongs to, captured at creation time from the
    /// ambient scope established while rendering. Null for top-level scopes.
    /// </summary>
    internal ReactiveScope? Parent { get; }

    internal int Depth { get; }

    internal long Sequence { get; }

    internal bool IsDisposed => disposed;

    internal bool IsDisposedInHierarchy
    {
        get
        {
            for (var current = this; current is not null; current = current.Parent)
            {
                if (current.disposed)
                    return true;
            }

            return false;
        }
    }

    public ReactiveScope()
    {
        Parent = ReferenceTracker.CurrentStructuralScope;
        Depth = Parent?.Depth + 1 ?? 0;
        Sequence = Interlocked.Increment(ref ScopeSequence);
    }

    public void Add(RefEffect effect)
    {
        effect.Scope ??= this;
        Add((IDisposable)effect);
    }

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
