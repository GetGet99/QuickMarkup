using System.Collections;

namespace QuickMarkup.Infra.Collections;

public class ReactiveSet<T> : ISet<T>, IReference
{
    readonly HashSet<T> backingSet = [];

    public int Count
    {
        get
        {
            ReferenceTracker.NotifyRefernceRead(this);
            return backingSet.Count;
        }
    }

    public bool IsReadOnly => false;

    public event Action? ValueChanged;

    public bool Add(T item)
    {
        if (backingSet.Add(item))
        {
            ValueChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void Clear()
    {
        if (Count is 0) return;
        backingSet.Clear();
        ValueChanged?.Invoke();
    }

    public bool Contains(T item)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingSet.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        backingSet.CopyTo(array, arrayIndex);
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        int cnt = backingSet.Count;
        backingSet.ExceptWith(other);
        if (cnt != backingSet.Count)
            ValueChanged?.Invoke();
    }

    public IEnumerator<T> GetEnumerator()
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingSet.GetEnumerator();
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        int cnt = backingSet.Count;
        backingSet.IntersectWith(other);
        if (cnt != backingSet.Count)
            ValueChanged?.Invoke();
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingSet.IsProperSubsetOf(other);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingSet.IsProperSupersetOf(other);
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingSet.IsSubsetOf(other);
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingSet.IsSupersetOf(other);
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingSet.Overlaps(other);
    }

    public bool Remove(T item)
    {
        if (backingSet.Remove(item))
        {
            ValueChanged?.Invoke();
            return true;
        }
        return false;
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingSet.SetEquals(other);
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        int cnt = backingSet.Count;
        backingSet.SymmetricExceptWith(other);
        if (cnt != backingSet.Count)
            ValueChanged?.Invoke();
    }

    public void UnionWith(IEnumerable<T> other)
    {
        int cnt = backingSet.Count;
        backingSet.UnionWith(other);
        if (cnt != backingSet.Count)
            ValueChanged?.Invoke();
    }

    void ICollection<T>.Add(T item) => Add(item);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}