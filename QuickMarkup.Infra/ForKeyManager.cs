using System.Collections.Specialized;

namespace QuickMarkup.Infra;

public interface IForKeyManager<TSrc, TKey>
{
    IReadOnlyList<TKey> Keys { get; }
    void Initialize(IReadOnlyList<TSrc> source);
    void ApplyCollectionChanged(NotifyCollectionChangedEventArgs e, IReadOnlyList<TSrc> source);
    void Refresh(IReadOnlyList<TSrc> source);
}

public sealed class OperationForKeyManager<TSrc> : IForKeyManager<TSrc, int>
{
    readonly List<int> keys = [];
    int nextId;

    public IReadOnlyList<int> Keys => keys;

    public void Initialize(IReadOnlyList<TSrc> source)
    {
        keys.Clear();

        for (var i = 0; i < source.Count; i++)
            keys.Add(NewKey());
    }

    public void ApplyCollectionChanged(NotifyCollectionChangedEventArgs e, IReadOnlyList<TSrc> source)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewStartingIndex < 0 || e.NewItems is null)
                    goto default;
                keys.InsertRange(e.NewStartingIndex, NewKeys(e.NewItems.Count));
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldStartingIndex < 0 || e.OldItems is null)
                    goto default;
                keys.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                break;
            case NotifyCollectionChangedAction.Move:
                if (e.OldStartingIndex < 0 || e.NewStartingIndex < 0 || e.OldItems is null)
                    goto default;
                var moving = keys.GetRange(e.OldStartingIndex, e.OldItems.Count);
                keys.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                keys.InsertRange(e.NewStartingIndex, moving);
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldStartingIndex < 0 || e.OldItems is null || e.NewItems is null)
                    goto default;
                keys.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                keys.InsertRange(e.OldStartingIndex, NewKeys(e.NewItems.Count));
                break;
            case NotifyCollectionChangedAction.Reset:
                Initialize(source);
                break;
            default:
                Initialize(source);
                break;
        }
    }

    public void Refresh(IReadOnlyList<TSrc> source)
    {
        Initialize(source);
    }

    int NewKey()
    {
        return nextId++;
    }

    IEnumerable<int> NewKeys(int count)
    {
        for (var i = 0; i < count; i++)
            yield return NewKey();
    }
}

public sealed class ExplicitForKeyManager<TSrc, TKey>(Func<TSrc, int, TKey> keyFn) : IForKeyManager<TSrc, TKey>
{
    readonly List<TKey> keys = [];

    public IReadOnlyList<TKey> Keys => keys;

    public void Initialize(IReadOnlyList<TSrc> source)
    {
        Recompute(source);
    }

    public void ApplyCollectionChanged(NotifyCollectionChangedEventArgs e, IReadOnlyList<TSrc> source)
    {
        Recompute(source);
    }

    public void Refresh(IReadOnlyList<TSrc> source)
    {
        Recompute(source);
    }

    void Recompute(IReadOnlyList<TSrc> source)
    {
        keys.Clear();

        for (var i = 0; i < source.Count; i++)
        {
            var index = i;
            var item = source[index];
            keys.Add(ReferenceTracker.NoCapture(() => keyFn(item, index)));
        }
    }
}

public static class ForKeyManager
{
    public static IForKeyManager<TSrc, int> CreateImplicit<TSrc>()
    {
        return new OperationForKeyManager<TSrc>();
    }

    public static IForKeyManager<TSrc, TKey> Create<TSrc, TKey>(Func<TSrc, TKey> keyFn)
    {
        return new ExplicitForKeyManager<TSrc, TKey>((item, _) => keyFn(item));
    }

    public static IForKeyManager<TSrc, TKey> Create<TSrc, TKey>(Func<TSrc, int, TKey> keyFn)
    {
        return new ExplicitForKeyManager<TSrc, TKey>(keyFn);
    }
}
