namespace QuickMarkup.Infra;

public sealed class FragmentBlock<TElement> : IUIBlock<TElement>
{
    readonly ReactiveScope scope;
    readonly Action<UIBlockHost<TElement>, ReactiveScope> build;
    UIBlockHost<TElement>? childHost;
    UIBlockHost<TElement>? parentHost;
    bool disposed;

    public FragmentBlock(
        ReactiveScope scope,
        Action<UIBlockHost<TElement>, ReactiveScope> build)
    {
        this.scope = scope;
        this.build = build;
    }

    public int Count => childHost?.Count ?? 0;

    public void Mount(UIBlockHost<TElement> host)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(FragmentBlock<TElement>));

        parentHost = host;
        if (childHost is null)
        {
            childHost = new UIBlockHost<TElement>(host, this);
            build(childHost, scope);
            return;
        }

        childHost.RemountAll();
    }

    public void Unmount()
    {
        if (childHost is null)
            return;

        childHost.UnmountAll();
        parentHost = null;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        childHost?.Clear();
        childHost = null;
        parentHost = null;
        scope.Dispose();
    }
}
