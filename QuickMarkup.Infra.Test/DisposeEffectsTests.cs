using QuickMarkup.Infra;

namespace QuickMarkup.Infra.Test;

[TestClass]
public sealed class DisposeEffectsTests
{
    [TestInitialize]
    public void Setup()
    {
        ReactiveScheduler.ResetForCurrentThread();
        ReactiveScheduler.Instance.Value!.AutoTick = false;
        ReactiveScheduler.Instance.Value!.ContinueOnException = false;
    }

    [TestMethod]
    public void DisposedEffectScheduledBeforeDisposeDoesNotTick()
    {
        var source = new Reference<int>(0);
        var seen = new List<int>();
        var effect = ReferenceTracker.RunAndRerunOnReferenceChange(() => source.Value, seen.Add);
        Assert.AreEqual(1, seen.Count);

        source.Value = 1;
        effect.Dispose();
        ReactiveScheduler.Tick();

        Assert.AreEqual(1, seen.Count);
    }

    [TestMethod]
    public void DisposedEffectIgnoresLaterChanges()
    {
        var source = new Reference<int>(0);
        var seen = new List<int>();
        var effect = ReferenceTracker.RunAndRerunOnReferenceChange(() => source.Value, seen.Add);

        effect.Dispose();
        source.Value = 2;
        ReactiveScheduler.Tick();

        Assert.AreEqual(1, seen.Count);
    }

    [TestMethod]
    public void ComputedDisposeStopsUpdates()
    {
        var source = new Reference<int>(1);
        var computed = new Computed<int>(() => source.Value * 2);
        Assert.AreEqual(2, computed.Value);

        computed.Dispose();
        computed.Dispose();
        source.Value = 5;
        ReactiveScheduler.Tick();

        Assert.AreEqual(2, computed.Value);
    }

    [TestMethod]
    public void TrackEffectsDisposesChildWithList()
    {
        var child = new StubEffectsDisposable();
        List<IDisposable> disposables = [];
        QuickMarkupEffects.TrackEffects(disposables, child);

        foreach (var disposable in disposables)
            disposable.Dispose();

        Assert.AreEqual(1, child.DisposeEffectsCalls);
    }

    [TestMethod]
    public void TrackEffectsDisposesChildWithScope()
    {
        var child = new StubEffectsDisposable();
        var scope = new ReactiveScope();
        QuickMarkupEffects.TrackEffects(scope, child);

        scope.Dispose();

        Assert.AreEqual(1, child.DisposeEffectsCalls);
    }

    [TestMethod]
    public void AwaitBlockDisposesOwnedAsyncComputed()
    {
        var owned = new AsyncComputed<int>(() => Task.FromResult(1));
        var block = new AwaitBlock<string, int>(new ReactiveScope(), owned, null, null, null, ownsAsyncComputed: true);

        block.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = owned.State);
    }

    [TestMethod]
    public void AwaitBlockKeepsSharedAsyncComputed()
    {
        var shared = new AsyncComputed<int>(() => Task.FromResult(1));
        var block = new AwaitBlock<string, int>(new ReactiveScope(), shared, null, null, null, ownsAsyncComputed: false);

        block.Dispose();

        Assert.AreEqual(AsyncComputedState.Success, shared.State);
    }

    [TestMethod]
    public void AwaitValueSlotDisposesOwnedAsyncComputed()
    {
        var owned = new AsyncComputed<int>(() => Task.FromResult(1));
        var slot = new AwaitValueSlot<string, int>(
            new ReactiveScope(), owned, _ => { }, null, null, null, ownsAsyncComputed: true);

        slot.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = owned.State);
    }

    [TestMethod]
    public void AwaitValueSlotKeepsSharedAsyncComputed()
    {
        var shared = new AsyncComputed<int>(() => Task.FromResult(1));
        var slot = new AwaitValueSlot<string, int>(
            new ReactiveScope(), shared, _ => { }, null, null, null, ownsAsyncComputed: false);

        slot.Dispose();

        Assert.AreEqual(AsyncComputedState.Success, shared.State);
    }

    sealed class StubEffectsDisposable : IQuickMarkupEffectsDisposable
    {
        public int DisposeEffectsCalls { get; private set; }

        public void DisposeEffects()
        {
            DisposeEffectsCalls++;
        }
    }
}
