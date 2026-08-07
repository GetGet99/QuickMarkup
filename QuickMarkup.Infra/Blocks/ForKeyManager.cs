using System.Collections.Specialized;

namespace QuickMarkup.Infra.Blocks;

public sealed class IdentityForKeyManager<TSrc> : IForKeyManager<TSrc, int>
{
    readonly List<KeyValuePair<TSrc, int>> history = [];
    readonly List<int> keys = [];
    int nextId;

    public IReadOnlyList<int> Keys => keys;

    public void Initialize(IReadOnlyList<TSrc> source) => Recompute(source);

    public void ApplyCollectionChanged(NotifyCollectionChangedEventArgs e, IReadOnlyList<TSrc> source)
        => Recompute(source);

    public void Refresh(IReadOnlyList<TSrc> source) => Recompute(source);

    void Recompute(IReadOnlyList<TSrc> source)
    {
        keys.Clear();

        var available = new List<KeyValuePair<TSrc, int>>(history);
        var nextHistory = new List<KeyValuePair<TSrc, int>>(source.Count);

        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            var match = FindMatch(available, item);
            if (match is not null)
            {
                available.Remove(match.Value);
                nextHistory.Add(match.Value);
                keys.Add(match.Value.Value);
            }
            else
            {
                var key = nextId++;
                keys.Add(key);
                nextHistory.Add(new(item, key));
            }
        }

        history.Clear();
        history.AddRange(nextHistory);
    }

    static KeyValuePair<TSrc, int>? FindMatch(List<KeyValuePair<TSrc, int>> available, TSrc item)
    {
        for (var i = 0; i < available.Count; i++)
        {
            if (Match(available[i].Key, item))
                return available[i];
        }

        return null;
    }

    static bool Match(TSrc a, TSrc b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is ValueType)
            return EqualityComparer<TSrc>.Default.Equals(a, b);
        return false;
    }
}

public sealed class ReactiveExplicitKeyManager<TSrc, TKey>(Func<TSrc, int, TKey> keyFn) : IForKeyManager<TSrc, TKey>
{
    readonly List<TKey> keys = [];

    public IReadOnlyList<TKey> Keys => keys;

    public void Initialize(IReadOnlyList<TSrc> source) => Recompute(source);

    public void ApplyCollectionChanged(NotifyCollectionChangedEventArgs e, IReadOnlyList<TSrc> source)
        => Recompute(source);

    public void Refresh(IReadOnlyList<TSrc> source) => Recompute(source);

    public void Recompute(IReadOnlyList<TSrc> source)
    {
        keys.Clear();

        for (var i = 0; i < source.Count; i++)
        {
            var index = i;
            var item = source[index];
            keys.Add(keyFn(item, index));
        }
    }
}
