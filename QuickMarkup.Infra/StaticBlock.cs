namespace QuickMarkup.Infra;

public sealed class StaticBlock<TElement> : IUIBlock<TElement>
{
    readonly ReactiveScope scope;
    readonly List<TElement> elements = [];
    UIBlockHost<TElement>? host;

    public StaticBlock(ReactiveScope scope, Action<IList<TElement>, ReactiveScope> build)
    {
        this.scope = scope;
        build(elements, scope);
    }

    public StaticBlock(ReactiveScope scope, IEnumerable<TElement> elements)
    {
        this.scope = scope;
        this.elements.AddRange(elements);
    }

    public int Count => elements.Count;

    public void Mount(UIBlockHost<TElement> host)
    {
        this.host = host;

        for (var i = 0; i < elements.Count; i++)
            host.InsertElement(this, i, elements[i]);
    }

    public void Unmount()
    {
        if (host is null)
            return;

        for (var i = 0; i < elements.Count; i++)
            host.RemoveElement(this, 0);

        host = null;
    }

    public void Dispose()
    {
        Unmount();
        scope.Dispose();
    }
}
