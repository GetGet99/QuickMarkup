namespace QuickMarkup.Infra;

public sealed class AwaitBlock<TElement, TValue> : IUIBlock<TElement>
{
    readonly ReactiveScope scope;
    readonly AsyncComputed<TValue> asyncComputed;
    readonly Func<IUIBlock<TElement>>? loadingFactory;
    readonly Func<Exception?, IUIBlock<TElement>>? errorFactory;
    readonly Func<TValue, IUIBlock<TElement>>? successFactory;
    UIBlockHost<TElement>? childHost;
    UIBlockHost<TElement>? host;
    IUIBlock<TElement>? currentBlock;
    RefEffect? stateEffect;
    AsyncComputedState? currentState;

    public AwaitBlock(
        ReactiveScope scope,
        AsyncComputed<TValue> asyncComputed,
        Func<IUIBlock<TElement>>? loadingFactory,
        Func<Exception?, IUIBlock<TElement>>? errorFactory,
        Func<TValue, IUIBlock<TElement>>? successFactory)
    {
        this.scope = scope;
        this.asyncComputed = asyncComputed;
        this.loadingFactory = loadingFactory;
        this.errorFactory = errorFactory;
        this.successFactory = successFactory;
    }

    public int Count => childHost?.Count ?? 0;

    public void Mount(UIBlockHost<TElement> host)
    {
        this.host = host;
        childHost = new UIBlockHost<TElement>(host, this);
        stateEffect = ReferenceTracker.RunAndRerunOnReferenceChange(
            () => asyncComputed.State,
            SwitchBranch);
        scope.Add(stateEffect);
    }

    public void Unmount()
    {
        if (currentBlock is not null)
        {
            childHost!.RemoveBlock(currentBlock);
            currentBlock = null;
        }

        currentState = null;
        stateEffect?.Dispose();
        stateEffect = null;
        childHost = null;
        host = null;
    }

    public void Dispose()
    {
        Unmount();
        scope.Dispose();
    }

    void SwitchBranch(AsyncComputedState newState)
    {
        if (host is null || currentState == newState)
            return;

        if (currentBlock is not null)
        {
            childHost!.RemoveBlock(currentBlock);
            currentBlock = null;
        }

        currentState = newState;

        using (ReferenceTracker.EnterStructuralScope(scope))
        {
            IUIBlock<TElement>? next = newState switch
            {
                AsyncComputedState.Loading => loadingFactory?.Invoke(),
                AsyncComputedState.Failed => errorFactory?.Invoke(asyncComputed.Failure),
                AsyncComputedState.Success => successFactory?.Invoke(asyncComputed.Value),
                _ => null
            };

            currentBlock = next;
            if (currentBlock is null)
                return;

            childHost!.AddBlock(currentBlock);
        }
    }
}

public static class AwaitBlock
{
    public static AwaitBlock<TElement, TValue> Create<TElement, TValue>(
        ReactiveScope controllerScope,
        Func<AsyncComputed<TValue>> asyncComputed,
        Func<IUIBlock<TElement>>? loadingFactory = null,
        Func<Exception?, IUIBlock<TElement>>? errorFactory = null,
        Func<TValue, IUIBlock<TElement>>? successFactory = null)
        => new(controllerScope, asyncComputed(), loadingFactory, errorFactory, successFactory);

    public static AwaitBlock<TElement, TValue> Create<TElement, TValue>(
        ReactiveScope controllerScope,
        AsyncFunction<TValue> asyncComputed,
        Func<IUIBlock<TElement>>? loadingFactory = null,
        Func<Exception?, IUIBlock<TElement>>? errorFactory = null,
        Func<TValue, IUIBlock<TElement>>? successFactory = null)
        => new(controllerScope, new(asyncComputed), loadingFactory, errorFactory, successFactory);
}
