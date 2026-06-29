namespace QuickMarkup.Infra;

public sealed class ConditionalBlock<TElement> : IUIBlock<TElement>
{
    readonly ReactiveScope controllerScope;
    readonly Func<bool> condition;
    readonly Func<IUIBlock<TElement>> trueFactory;
    readonly Func<IUIBlock<TElement>>? falseFactory;
    UIBlockHost<TElement>? childHost;
    UIBlockHost<TElement>? host;
    IUIBlock<TElement>? current;
    RefEffect? conditionEffect;
    bool? currentConditionValue;
    bool disposed;

    public ConditionalBlock(
        ReactiveScope controllerScope,
        Func<bool> condition,
        Func<IUIBlock<TElement>> trueFactory,
        Func<IUIBlock<TElement>>? falseFactory = null)
    {
        this.controllerScope = controllerScope;
        this.condition = condition;
        this.trueFactory = trueFactory;
        this.falseFactory = falseFactory;
    }

    public int Count => childHost?.Count ?? 0;

    public void Mount(UIBlockHost<TElement> host)
    {
        this.host = host;
        childHost = new UIBlockHost<TElement>(host, this);
        conditionEffect = ReferenceTracker.RunAndRerunOnReferenceChange(
            condition,
            SwitchBranch);
        controllerScope.Add(conditionEffect);
    }

    public void Unmount()
    {
        if (current is not null)
        {
            childHost!.RemoveBlock(current);
            current = null;
        }

        currentConditionValue = null;
        conditionEffect?.Dispose();
        conditionEffect = null;
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

    void SwitchBranch(bool conditionValue)
    {
        if (disposed || host is null || currentConditionValue == conditionValue)
            return;

        if (current is not null)
        {
            childHost!.RemoveBlock(current);
            current = null;
        }

        currentConditionValue = conditionValue;

        var next = conditionValue ? trueFactory() : falseFactory?.Invoke();
        current = next;
        if (current is null)
            return;

        childHost!.AddBlock(current);
    }
}
