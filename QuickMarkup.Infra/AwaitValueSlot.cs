namespace QuickMarkup.Infra;

public sealed class AwaitValueSlot<TElement, TValue> : IDisposable
{
    readonly ReactiveScope controllerScope;
    readonly AsyncComputed<TValue> asyncComputed;
    readonly Action<TElement> setValue;
    readonly Func<ScopedValue<TElement>>? loadingFactory;
    readonly Func<Exception?, ScopedValue<TElement>>? errorFactory;
    readonly Func<TValue, ScopedValue<TElement>>? successFactory;
    ScopedValue<TElement>? current;
    RefEffect? stateEffect;
    AsyncComputedState? currentState;
    bool disposed;

    public AwaitValueSlot(
        ReactiveScope controllerScope,
        AsyncComputed<TValue> asyncComputed,
        Action<TElement> setValue,
        Func<ScopedValue<TElement>>? loadingFactory,
        Func<Exception?, ScopedValue<TElement>>? errorFactory,
        Func<TValue, ScopedValue<TElement>>? successFactory)
    {
        this.controllerScope = controllerScope;
        this.asyncComputed = asyncComputed;
        this.setValue = setValue;
        this.loadingFactory = loadingFactory;
        this.errorFactory = errorFactory;
        this.successFactory = successFactory;

        stateEffect = ReferenceTracker.RunAndRerunOnReferenceChange(
            () => asyncComputed.State,
            SwitchBranch);
        controllerScope.Add(stateEffect);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        stateEffect?.Dispose();
        stateEffect = null;
        current?.Dispose();
        current = null;
        controllerScope.Dispose();
    }

    void SwitchBranch(AsyncComputedState newState)
    {
        if (disposed || currentState == newState)
            return;

        var old = current;
        currentState = newState;

        using (ReferenceTracker.EnterStructuralScope(controllerScope))
        {
            ScopedValue<TElement>? next = newState switch
            {
                AsyncComputedState.Loading => loadingFactory?.Invoke(),
                AsyncComputedState.Failed => errorFactory?.Invoke(asyncComputed.Failure),
                AsyncComputedState.Success => successFactory?.Invoke(asyncComputed.Value),
                _ => null
            };

            current = next;
            if (next is not null)
                setValue(next.Value);
            old?.Dispose();
        }
    }
}

public static class AwaitValueSlot
{
    public static AwaitValueSlot<TElement, TValue> Create<TElement, TValue>(
        ReactiveScope controllerScope,
        Func<AsyncComputed<TValue>> asyncComputed,
        Action<TElement> setValue,
        Func<ScopedValue<TElement>>? loadingFactory = null,
        Func<Exception?, ScopedValue<TElement>>? errorFactory = null,
        Func<TValue, ScopedValue<TElement>>? successFactory = null)
        => new(controllerScope, asyncComputed(), setValue, loadingFactory, errorFactory, successFactory);

    public static AwaitValueSlot<TElement, TValue> Create<TElement, TValue>(
        ReactiveScope controllerScope,
        AsyncFunction<TValue> asyncComputed,
        Action<TElement> setValue,
        Func<ScopedValue<TElement>>? loadingFactory = null,
        Func<Exception?, ScopedValue<TElement>>? errorFactory = null,
        Func<TValue, ScopedValue<TElement>>? successFactory = null)
        => new(controllerScope, new(asyncComputed), setValue, loadingFactory, errorFactory, successFactory);
}
