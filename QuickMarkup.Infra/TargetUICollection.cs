using System.Collections;

namespace QuickMarkup.Infra;

record TargetUICollection<T>(IList<T> targetList, Action<int, int> MoveFunction) : IUICollection<T>
{
    public TargetUICollection(IList<T> targetList) : this(targetList, (oldIndex, newIndex) =>
    {
        if (oldIndex == newIndex)
            return;

        var item = targetList[oldIndex];

        // Remove first — critical invariant
        targetList.RemoveAt(oldIndex);

        // Adjust index if needed
        if (newIndex > oldIndex)
            newIndex--;

        targetList.Insert(newIndex, item);
    })
    { }

    public void Move(uint oldIndex, uint newIndex)
        => MoveFunction((int)oldIndex, (int)newIndex);

    // IList<T> forwards
    public T this[int index] { get => targetList[index]; set => targetList[index] = value; }
    public int Count => targetList.Count;
    public bool IsReadOnly => targetList.IsReadOnly;
    public void Add(T item) => targetList.Add(item);
    public void Clear() => targetList.Clear();
    public bool Contains(T item) => targetList.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => targetList.CopyTo(array, arrayIndex);
    public IEnumerator<T> GetEnumerator() => targetList.GetEnumerator();
    public int IndexOf(T item) => targetList.IndexOf(item);
    public void Insert(int index, T item) => targetList.Insert(index, item);
    public bool Remove(T item) => targetList.Remove(item);
    public void RemoveAt(int index) => targetList.RemoveAt(index);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
