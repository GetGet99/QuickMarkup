using System.Xml.Linq;

namespace QuickMarkup.Infra;

public class AsyncComputed<T> : IReference, IDisposable
{
    bool disposed;
    readonly RefEffect effect;

    event Action? StateChanged;
    event Action IReference.ValueChanged
    {
        add => StateChanged += value;
        remove => StateChanged -= value;
    }

    public string Name { get; }
    uint currentVersion = 0;

    public AsyncComputed(AsyncFunctionWithCancellation<T> computed, string name = "")
    {
        Name = name;
        Loading();
        CancellationTokenSource? cts = null;
        effect = ReferenceTracker.RunAndRerunOnReferenceChange(() =>
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = new();
            return computed(cts.Token);
        }, async task =>
        {
            uint curIter = Interlocked.Increment(ref currentVersion);
            Loading();
            T result;
            try
            {
                result = await task;
            } catch (Exception e)
            {
                if (curIter == Volatile.Read(ref currentVersion)) Failed(e);
                return;
            }
            if (curIter == Volatile.Read(ref currentVersion)) Success(result);
        });
    }

    public AsyncComputed(AsyncFunction<T> computedFunc, string name = "") : this(ct => computedFunc(), name) {}
    public AsyncComputed(Func<AsyncFunctionWithCancellation<T>> computedFunc, string name = "") : this(computedFunc(), name) {}
    public AsyncComputed(Func<AsyncFunction<T>> computedFunc, string name = "") : this(computedFunc(), name) {}
    public AsyncComputedState State
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ReferenceTracker.NotifyRefernceRead(this);
            return field;
        }
        private set;
    }
    public bool IsSuccess => State is AsyncComputedState.Success;
    public bool IsLoading => State is AsyncComputedState.Loading;
    public bool IsFailed => State is AsyncComputedState.Failed;
    T? value;
    public T Value
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ReferenceTracker.NotifyRefernceRead(this);
            if (State is not AsyncComputedState.Success)
                throw new InvalidOperationException();
            return value!;
        }
    }
    public Exception? Failure
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ReferenceTracker.NotifyRefernceRead(this);
            return field;
        }
        private set;
    }

    ~AsyncComputed() => Dispose(true);
    public void Dispose() {
        Dispose(false);
        GC.SuppressFinalize(this);
    }
    void Dispose(bool fromGC)
    {
        disposed = true;
        effect?.Dispose();
    }
    public RefEffect Watch(Action<AsyncComputed<T>> action, bool immediate = false)
    {
        var watchEffect = new RefEffect(_ => action(this));
        watchEffect.AddDependency(this);
        if (immediate)
            watchEffect.Tick();
        return watchEffect;
    }
    public void Recompute()
    {
        ReactiveScheduler.ScheduleEffect(effect);
    }
    void Loading()
    {
        if (disposed) return;
        value = default;
        Failure = default;
        State = AsyncComputedState.Loading;
        StateChanged?.Invoke();
    }
    void Success(T result)
    {
        if (disposed) return;
        value = result;
        Failure = default!;
        State = AsyncComputedState.Success;
        StateChanged?.Invoke();
    }
    void Failed(Exception e)
    {
        if (disposed) return;
        value = default;
        Failure = e;
        State = AsyncComputedState.Failed;
        StateChanged?.Invoke();
    }
}

public delegate Task<T> AsyncFunction<T>();
public delegate Task<T> AsyncFunctionWithCancellation<T>(CancellationToken token);
public enum AsyncComputedState { Loading, Success, Failed }
