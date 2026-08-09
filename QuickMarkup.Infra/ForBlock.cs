using System.Collections.Specialized;

#pragma warning disable CS0618
namespace QuickMarkup.Infra;

[Obsolete("Use QuickMarkup.Infra.Blocks.ForBlock instead.")]
public sealed class ForBlock<TSrc, TElement> : ForBlock<TSrc, TElement, int>
{
    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : this(controllerScope, source, source as INotifyCollectionChanged, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : this(controllerScope, source, source as INotifyCollectionChanged, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        INotifyCollectionChanged? collectionChanged,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(
            controllerScope,
            source,
            collectionChanged,
            ForKeyManager.CreateImplicit<TSrc>(),
            itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        INotifyCollectionChanged? collectionChanged,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(
            controllerScope,
            source,
            collectionChanged,
            ForKeyManager.CreateImplicit<TSrc>(),
            itemFactory)
    {
    }
}

[Obsolete("Use QuickMarkup.Infra.Blocks.ForBlock instead.")]
public class ForBlock<TSrc, TElement, TKey> : IUIBlock<TElement>
{
    readonly ReactiveScope controllerScope;
    readonly IReadOnlyList<TSrc> source;
    readonly INotifyCollectionChanged? collectionChanged;
    readonly IForKeyManager<TSrc, TKey> keyManager;
    readonly Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory;
    readonly Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>>? indexedItemFactory;
    readonly List<ForItemState<TSrc, TElement, TKey>> items = [];
    UIBlockHost<TElement>? childHost;
    UIBlockHost<TElement>? host;
    bool dirty;
    bool scheduled;
    bool disposed;

    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        IForKeyManager<TSrc, TKey> keyManager,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : this(controllerScope, source, source as INotifyCollectionChanged, keyManager, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        INotifyCollectionChanged? collectionChanged,
        IForKeyManager<TSrc, TKey> keyManager,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        this.controllerScope = controllerScope;
        this.source = source;
        this.collectionChanged = collectionChanged;
        this.keyManager = keyManager;
        this.itemFactory = itemFactory;
    }

    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        IForKeyManager<TSrc, TKey> keyManager,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : this(controllerScope, source, source as INotifyCollectionChanged, keyManager, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        INotifyCollectionChanged? collectionChanged,
        IForKeyManager<TSrc, TKey> keyManager,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        this.controllerScope = controllerScope;
        this.source = source;
        this.collectionChanged = collectionChanged;
        this.keyManager = keyManager;
        this.itemFactory = static _ => throw new InvalidOperationException("This for block uses the index-aware item factory.");
        indexedItemFactory = itemFactory;
    }

    public int Count => childHost?.Count ?? 0;

    public void Mount(UIBlockHost<TElement> host)
    {
        this.host = host;
        childHost = new UIBlockHost<TElement>(host, this);

        keyManager.Initialize(source);
        ValidateKeyCount();
        ValidateUniqueKeys();

        collectionChanged?.CollectionChanged += Source_CollectionChanged;

        for (var i = 0; i < source.Count; i++)
            AddInitialItem(keyManager.Keys[i], source[i]);
    }

    public void Refresh()
    {
        if (disposed)
            return;

        keyManager.Refresh(source);
        MarkDirty();
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
        keyManager.ApplyCollectionChanged(e, source);
        MarkDirty();
    }

    void MarkDirty()
    {
        dirty = true;

        if (host is null || scheduled)
            return;

        if (ReactiveScheduler.Instance.Value!.IsTicking)
        {
            ReconcileOnTick();
            return;
        }

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
        ValidateKeyCount();

        var oldItems = items.ToArray();
        var nextItems = new List<ForItemState<TSrc, TElement, TKey>>(source.Count);
        var reused = new HashSet<ForItemState<TSrc, TElement, TKey>>();
        var nextKeys = new HashSet<TKey>();

        for (var i = 0; i < source.Count; i++)
        {
            var key = keyManager.Keys[i];
            if (!nextKeys.Add(key))
                throw new InvalidOperationException($"Duplicate key found in for block: {key}");

            var state = FindState(oldItems, key);
            if (state is not null)
            {
                state.IndexRef?.Value = i;
                state.ItemRef.Value = source[i];
                reused.Add(state);
                nextItems.Add(state);
            }
            else
            {
                nextItems.Add(CreateItem(key, source[i], i));
            }
        }

        foreach (var old in oldItems)
        {
            if (reused.Contains(old))
                childHost!.DetachBlock(old.Block);
            else
                childHost!.RemoveBlock(old.Block);
        }

        items.Clear();
        foreach (var item in nextItems)
        {
            items.Add(item);
            childHost!.AddBlock(item.Block);
        }
    }

    void AddInitialItem(TKey key, TSrc item)
    {
        var state = CreateItem(key, item, items.Count);
        items.Add(state);
        childHost!.AddBlock(state.Block);
    }

    ForItemState<TSrc, TElement, TKey> CreateItem(TKey key, TSrc item, int index)
    {
        var itemRef = new Reference<TSrc>(item);

        using var _ = ReferenceTracker.EnterStructuralScope(controllerScope);

        if (indexedItemFactory is not null)
        {
            var indexRef = new Reference<int>(index);
            var block = indexedItemFactory(indexRef, itemRef);
            return new ForItemState<TSrc, TElement, TKey>(key, itemRef, indexRef, block);
        }

        return new ForItemState<TSrc, TElement, TKey>(key, itemRef, null, itemFactory(itemRef));
    }

    void RemoveItemAt(int index)
    {
        var state = items[index];
        childHost!.RemoveBlock(state.Block);
        items.RemoveAt(index);
    }

    void ValidateKeyCount()
    {
        if (keyManager.Keys.Count != source.Count)
            throw new InvalidOperationException(
                $"For block key count mismatch. Expected {source.Count}, got {keyManager.Keys.Count}.");
    }

    void ValidateUniqueKeys()
    {
        var keys = new HashSet<TKey>();
        foreach (var key in keyManager.Keys)
        {
            if (!keys.Add(key))
                throw new InvalidOperationException($"Duplicate key found in for block: {key}");
        }
    }

    static ForItemState<TSrc, TElement, TKey>? FindState(
        IReadOnlyList<ForItemState<TSrc, TElement, TKey>> states,
        TKey key)
    {
        var comparer = EqualityComparer<TKey>.Default;

        for (var i = 0; i < states.Count; i++)
        {
            if (comparer.Equals(states[i].Key, key))
                return states[i];
        }

        return null;
    }
}

[Obsolete("Use QuickMarkup.Infra.Blocks.ForBlock instead.")]
public static class ForBlock
{
    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            source,
            ForKeyManager.CreateImplicit<TSrc>(),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            source,
            ForKeyManager.CreateImplicit<TSrc>(),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<TSrc, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            source,
            ForKeyManager.Create(keyFn),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<TSrc, TKey> keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            source,
            ForKeyManager.Create(keyFn),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<TSrc, int, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            source,
            ForKeyManager.Create(keyFn),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<TSrc, int, TKey> keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            source,
            ForKeyManager.Create(keyFn),
            itemFactory);
    }
}

public sealed record ForItemState<TSrc, TElement, TKey>(
    TKey Key,
    Reference<TSrc> ItemRef,
    Reference<int>? IndexRef,
    IUIBlock<TElement> Block
);
