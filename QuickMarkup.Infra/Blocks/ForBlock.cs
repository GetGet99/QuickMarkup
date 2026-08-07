using System.Collections.Specialized;

namespace QuickMarkup.Infra.Blocks;

public sealed class ForBlock<TSrc, TElement> : ForBlock<TSrc, TElement, int>
{
    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(controllerScope, () => source, null, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(controllerScope, () => source, null, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(controllerScope, sourceGetter, null, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(controllerScope, sourceGetter, null, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        IEnumerable<TSrc> source,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(controllerScope, Materialize(() => source), null, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        IEnumerable<TSrc> source,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(controllerScope, Materialize(() => source), null, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(controllerScope, Materialize(sourceGetter), null, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : base(controllerScope, Materialize(sourceGetter), null, itemFactory)
    {
    }
}

public class ForBlock<TSrc, TElement, TKey> : IUIBlock<TElement>
{
    static readonly IReadOnlyList<TSrc> EmptySource = Array.Empty<TSrc>();

    readonly ReactiveScope controllerScope;
    readonly Func<IReadOnlyList<TSrc>> sourceGetter;
    readonly Func<TSrc, int, TKey>? keyFn;
    readonly Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory;
    readonly Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>>? indexedItemFactory;
    readonly List<ForItemState<TSrc, TElement, TKey>> items = [];
    UIBlockHost<TElement>? childHost;
    UIBlockHost<TElement>? host;
    IForKeyManager<TSrc, TKey> keyManager;
    INotifyCollectionChanged? subscribedCollectionChanged;
    bool incrementalKeys;
    bool dirty;
    bool scheduled;
    bool disposed;
    bool mounting;
    RefEffect? referenceEffect;

    public ForBlock(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<TSrc, int, TKey>? keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        this.controllerScope = controllerScope;
        this.sourceGetter = sourceGetter;
        this.keyFn = keyFn;
        this.itemFactory = itemFactory;
        keyManager = CreateKeyManager(EmptySource);
    }

    public ForBlock(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<TSrc, int, TKey>? keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        this.controllerScope = controllerScope;
        this.sourceGetter = sourceGetter;
        this.keyFn = keyFn;
        this.itemFactory = static _ => throw new InvalidOperationException("This for block uses the index-aware item factory.");
        indexedItemFactory = itemFactory;
        keyManager = CreateKeyManager(EmptySource);
    }

    public ForBlock(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<TSrc, int, TKey>? keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : this(controllerScope, Materialize(sourceGetter), keyFn, itemFactory)
    {
    }

    public ForBlock(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<TSrc, int, TKey>? keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
        : this(controllerScope, Materialize(sourceGetter), keyFn, itemFactory)
    {
    }

    internal static Func<IReadOnlyList<TSrc>> Materialize(Func<IEnumerable<TSrc>> sourceGetter)
        => () => sourceGetter()?.ToList() as IReadOnlyList<TSrc> ?? Array.Empty<TSrc>();

    public int Count => childHost?.Count ?? 0;

    public void Mount(UIBlockHost<TElement> host)
    {
        this.host = host;
        childHost = new UIBlockHost<TElement>(host, this);

        var source = ReadSource();
        EnsureKeyManager(source);
        SubscribeCollectionChanged(source);

        mounting = true;
        referenceEffect = ReferenceTracker.RunAndRerunOnReferenceChange(
            () =>
            {
                var s = ReadSource();
                _ = s.Count;
                keyManager.Refresh(s);
                return s;
            },
            _ => MarkDirty());
        controllerScope.Add(referenceEffect);
        mounting = false;

        ValidateKeyCount(source);
        ValidateUniqueKeys();

        for (var i = 0; i < source.Count; i++)
            AddInitialItem(keyManager.Keys[i], source[i]);
    }

    public void Refresh()
    {
        if (disposed)
            return;

        keyManager.Refresh(ReadSource());
        MarkDirty();
    }

    public void Unmount()
    {
        UnsubscribeCollectionChanged();

        referenceEffect?.Dispose();
        referenceEffect = null;

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

    IReadOnlyList<TSrc> ReadSource()
    {
        return sourceGetter() ?? EmptySource;
    }

    void EnsureKeyManager(IReadOnlyList<TSrc> source)
    {
        var wantsIncremental = keyFn is null && source is INotifyCollectionChanged;
        if (wantsIncremental == incrementalKeys)
            return;

        incrementalKeys = wantsIncremental;
        keyManager = CreateKeyManager(source);
    }

    IForKeyManager<TSrc, TKey> CreateKeyManager(IReadOnlyList<TSrc> source)
    {
        if (keyFn is not null)
            return new ReactiveExplicitKeyManager<TSrc, TKey>(keyFn);

        if (source is INotifyCollectionChanged)
            return (IForKeyManager<TSrc, TKey>)(object)new OperationForKeyManager<TSrc>();

        return (IForKeyManager<TSrc, TKey>)(object)new IdentityForKeyManager<TSrc>();
    }

    void SubscribeCollectionChanged(IReadOnlyList<TSrc> source)
    {
        if (source is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += Source_CollectionChanged;
            subscribedCollectionChanged = ncc;
        }
    }

    void UnsubscribeCollectionChanged()
    {
        if (subscribedCollectionChanged is not null)
            subscribedCollectionChanged.CollectionChanged -= Source_CollectionChanged;
        subscribedCollectionChanged = null;
    }

    void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        keyManager.ApplyCollectionChanged(e, ReadSource());
        MarkDirty();
    }

    void MarkDirty()
    {
        dirty = true;

        if (host is null || scheduled || mounting)
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
        var source = ReadSource();

        var currentIncc = source as INotifyCollectionChanged;
        if (!ReferenceEquals(subscribedCollectionChanged, currentIncc))
        {
            UnsubscribeCollectionChanged();
            SubscribeCollectionChanged(source);
            EnsureKeyManager(source);
            keyManager.Initialize(source);
            while (items.Count > 0)
                RemoveItemAt(items.Count - 1);
        }
        else if (!incrementalKeys)
        {
            keyManager.Refresh(source);
        }

        ValidateKeyCount(source);

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

    void ValidateKeyCount(IReadOnlyList<TSrc> source)
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

public static class ForBlock
{
    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            () => source,
            null,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            () => source,
            null,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            sourceGetter,
            null,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            sourceGetter,
            null,
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
            () => source,
            (item, _) => keyFn(item),
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
            () => source,
            (item, _) => keyFn(item),
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
            () => source,
            keyFn,
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
            () => source,
            keyFn,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<TSrc, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            sourceGetter,
            (item, _) => keyFn(item),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<TSrc, TKey> keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            sourceGetter,
            (item, _) => keyFn(item),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<TSrc, int, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            sourceGetter,
            keyFn,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        Func<IReadOnlyList<TSrc>> sourceGetter,
        Func<TSrc, int, TKey> keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            sourceGetter,
            keyFn,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        IEnumerable<TSrc> source,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, int>.Materialize(() => source),
            null,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        IEnumerable<TSrc> source,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, int>.Materialize(() => source),
            null,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, int>.Materialize(sourceGetter),
            null,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, int>.Materialize(sourceGetter),
            null,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IEnumerable<TSrc> source,
        Func<TSrc, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, TKey>.Materialize(() => source),
            (item, _) => keyFn(item),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IEnumerable<TSrc> source,
        Func<TSrc, TKey> keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, TKey>.Materialize(() => source),
            (item, _) => keyFn(item),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IEnumerable<TSrc> source,
        Func<TSrc, int, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, TKey>.Materialize(() => source),
            keyFn,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IEnumerable<TSrc> source,
        Func<TSrc, int, TKey> keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, TKey>.Materialize(() => source),
            keyFn,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<TSrc, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, TKey>.Materialize(sourceGetter),
            (item, _) => keyFn(item),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<TSrc, TKey> keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, TKey>.Materialize(sourceGetter),
            (item, _) => keyFn(item),
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<TSrc, int, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, TKey>.Materialize(sourceGetter),
            keyFn,
            itemFactory);
    }

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        Func<IEnumerable<TSrc>> sourceGetter,
        Func<TSrc, int, TKey> keyFn,
        Func<Reference<int>, Reference<TSrc>, IUIBlock<TElement>> itemFactory)
    {
        return new(
            controllerScope,
            ForBlock<TSrc, TElement, TKey>.Materialize(sourceGetter),
            keyFn,
            itemFactory);
    }
}
