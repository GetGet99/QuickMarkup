using System.Collections.Specialized;

namespace QuickMarkup.Infra;

public sealed class ForBlock<TItem, TElement>(
    ReactiveScope controllerScope,
    IReadOnlyList<TItem> source,
    INotifyCollectionChanged? collectionChanged,
    Func<Reference<TItem>, IUIBlock<TElement>> itemFactory) : IUIBlock<TElement>
{
    readonly List<ForItemState<TItem, TElement>> items = [];
    UIBlockHost<TElement>? childHost;
    UIBlockHost<TElement>? host;
    bool dirty;
    bool scheduled;
    bool disposed;

    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TItem> source,
        Func<Reference<TItem>, IUIBlock<TElement>> itemFactory)
        : this(controllerScope, source, source as INotifyCollectionChanged, itemFactory)
    {
    }

    public int Count => childHost?.Count ?? 0;

    public void Mount(UIBlockHost<TElement> host)
    {
        this.host = host;
        childHost = new UIBlockHost<TElement>(host, this);

        collectionChanged?.CollectionChanged += Source_CollectionChanged;

        for (var i = 0; i < source.Count; i++)
            AppendItem(source[i]);
    }

    public void Unmount()
    {
        collectionChanged?.CollectionChanged -= Source_CollectionChanged;

        while (items.Count > 0)
            RemoveItemAt(items.Count - 1);

        dirty = false;
        scheduled = false;
        childHost = null;
        host = null;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Unmount();
        controllerScope.Dispose();
    }

    void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        MarkDirty();
    }

    void MarkDirty()
    {
        dirty = true;

        if (scheduled)
            return;

        scheduled = true;
        ReactiveScheduler.ScheduleCallback(ReconcileOnTick);
    }

    void ReconcileOnTick()
    {
        scheduled = false;

        if (disposed || host is null || !dirty)
            return;

        dirty = false;
        ReconcileToCurrentSource();
    }

    void ReconcileToCurrentSource()
    {
        var commonCount = Math.Min(items.Count, source.Count);

        for (var i = 0; i < commonCount; i++)
            items[i].ItemRef.Value = source[i];

        while (items.Count > source.Count)
            RemoveItemAt(items.Count - 1);

        while (items.Count < source.Count)
            AppendItem(source[items.Count]);
    }

    void AppendItem(TItem item)
    {
        var itemRef = new Reference<TItem>(item);
        var block = itemFactory(itemRef);
        items.Add(new ForItemState<TItem, TElement>(itemRef, block));
        childHost!.AddBlock(block);
    }

    void RemoveItemAt(int index)
    {
        var state = items[index];
        childHost!.RemoveBlock(state.Block);
        items.RemoveAt(index);
    }
}

public sealed record ForItemState<TItem, TElement>(
    Reference<TItem> ItemRef,
    IUIBlock<TElement> Block
);
