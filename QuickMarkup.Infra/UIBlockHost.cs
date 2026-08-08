namespace QuickMarkup.Infra;

public sealed class UIBlockHost<TElement>
{
    readonly IUICollection<TElement>? target;
    readonly UIBlockHost<TElement>? parentHost;
    readonly IUIBlock<TElement>? parentOwner;
    readonly List<IUIBlock<TElement>> blocks = [];

    public UIBlockHost(IUICollection<TElement> target)
    {
        this.target = target;
    }

    public UIBlockHost(UIBlockHost<TElement> parentHost, IUIBlock<TElement> parentOwner)
    {
        this.parentHost = parentHost;
        this.parentOwner = parentOwner;
    }

    public int Count => blocks.Sum(x => x.Count);

    public int GetStartIndex(IUIBlock<TElement> block)
    {
        var index = 0;

        foreach (var current in blocks)
        {
            if (ReferenceEquals(current, block))
                return index;

            index += current.Count;
        }

        throw new InvalidOperationException("Block is not mounted in this host.");
    }

    public void AddBlock(IUIBlock<TElement> block)
    {
        InsertBlock(blocks.Count, block);
    }

    public void InsertBlock(int index, IUIBlock<TElement> block)
    {
        blocks.Insert(index, block);
        block.Mount(this);
    }

    public void RemoveBlock(IUIBlock<TElement> block)
    {
        block.Unmount();
        blocks.Remove(block);
        block.Dispose();
    }

    public void Clear()
    {
        while (blocks.Count > 0)
            RemoveBlock(blocks[^1]);
    }

    internal void UnmountAll()
    {
        for (var i = blocks.Count - 1; i >= 0; i--)
            blocks[i].Unmount();
    }

    internal void RemountAll()
    {
        foreach (var block in blocks)
            block.Mount(this);
    }

    internal void DetachBlock(IUIBlock<TElement> block)
    {
        block.Unmount();
        blocks.Remove(block);
    }

    /// <summary>
    /// Moves a mounted block to a new position in this host without unmounting it.
    /// </summary>
    public void MoveBlock(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex)
            return;

        var block = blocks[oldIndex];
        var count = block.Count;

        if (count == 0)
        {
            blocks.RemoveAt(oldIndex);
            blocks.Insert(newIndex, block);
            return;
        }

        var oldStart = GetStartIndex(block);

        blocks.RemoveAt(oldIndex);

        var newStart = newIndex < blocks.Count
            ? GetStartIndex(blocks[newIndex])
            : Count;

        blocks.Insert(newIndex, block);

        MoveElementRange(oldStart, count, newStart);
    }

    void MoveElementRange(int start, int count, int dest)
    {
        if (target is not null)
        {
            MoveInTarget(target, start, count, dest);
            return;
        }

        parentHost!.MoveElementRange(parentOwner!, start, count, dest);
    }

    /// <summary>
    /// Moves a contiguous range of elements relative to an owner block within this host.
    /// </summary>
    public void MoveElementRange(IUIBlock<TElement> owner, int start, int count, int dest)
    {
        var startGlobal = GetStartIndex(owner) + start;
        var destGlobal = GetStartIndex(owner) + dest;

        if (target is not null)
        {
            MoveInTarget(target, startGlobal, count, destGlobal);
            return;
        }

        parentHost!.MoveElementRange(parentOwner!, startGlobal, count, destGlobal);
    }

    static void MoveInTarget(IUICollection<TElement> target, int start, int count, int dest)
    {
        if (count == 0 || start == dest)
            return;

        if (dest < start)
        {
            for (var i = 0; i < count; i++)
                target.Move(start + i, dest + i);
        }
        else
        {
            for (var i = count - 1; i >= 0; i--)
                target.Move(start + i, dest + i);
        }
    }

    public void InsertElement(IUIBlock<TElement> owner, int localIndex, TElement element)
    {
        var index = GetStartIndex(owner) + localIndex;

        if (target is not null)
        {
            target.Insert(index, element);
            return;
        }

        parentHost!.InsertElement(parentOwner!, index, element);
    }

    public void RemoveElement(IUIBlock<TElement> owner, int localIndex)
    {
        var index = GetStartIndex(owner) + localIndex;

        if (target is not null)
        {
            target.RemoveAt(index);
            return;
        }

        parentHost!.RemoveElement(parentOwner!, index);
    }
}
