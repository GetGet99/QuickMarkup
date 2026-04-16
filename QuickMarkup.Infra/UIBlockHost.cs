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

    public void UnmountAll()
    {
        for (var i = blocks.Count - 1; i >= 0; i--)
            blocks[i].Unmount();
    }

    public void RemountAll()
    {
        foreach (var block in blocks)
            block.Mount(this);
    }

    public void DetachBlock(IUIBlock<TElement> block)
    {
        block.Unmount();
        blocks.Remove(block);
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
