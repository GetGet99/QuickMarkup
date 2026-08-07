using System.Collections;

namespace QuickMarkup.Infra.Collections;

public class ReactiveList<T> : IList<T>, IReference
{
    readonly List<T> backingList = [];

    public T this[int index] {
        get
        {
            ReferenceTracker.NotifyRefernceRead(this);
            return backingList[index];
        } set
        {
            backingList[index] = value;
            ValueChanged?.Invoke();
        }
    }

    public int Count
    {
        get
        {
            ReferenceTracker.NotifyRefernceRead(this);
            return backingList.Count;
        }
    }

    public bool IsReadOnly => false;

    public event Action? ValueChanged;

    public void Add(T item)
    {
        backingList.Add(item);
        ValueChanged?.Invoke();
    }

    public void Clear()
    {
        backingList.Clear();
        ValueChanged?.Invoke();
    }

    public bool Contains(T item)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingList.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        backingList.CopyTo(array, arrayIndex);
    }

    public IEnumerator<T> GetEnumerator()
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingList.GetEnumerator();
    }

    public int IndexOf(T item)
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingList.IndexOf(item);
    }

    public void Insert(int index, T item)
    {
        backingList.Insert(index, item);
        ValueChanged?.Invoke();
    }

    public bool Remove(T item)
    {
        var removed = backingList.Remove(item);
        // TODO: Check if false always mean no mutation
        if (removed) ValueChanged?.Invoke();
        return removed;
    }

    public void RemoveAt(int index)
    {
        ((IList<T>)backingList).RemoveAt(index);
        ValueChanged?.Invoke();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        ReferenceTracker.NotifyRefernceRead(this);
        return backingList.GetEnumerator();
    }
}