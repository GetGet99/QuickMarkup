using QuickMarkup.Infra;

namespace QuickMarkup.Infra.Test
{
    [TestClass]
    public sealed class AsyncComputedTests
    {
        [TestInitialize]
        public void Setup()
        {
            ReactiveScheduler.ResetForCurrentThread();
            ReactiveScheduler.Instance.Value!.AutoTick = false;
            ReactiveScheduler.Instance.Value!.ContinueOnException = false;
        }

        [TestMethod]
        public void InitializesInLoadingState()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task);

            Assert.IsTrue(ac.IsLoading);
            Assert.IsFalse(ac.IsSuccess);
            Assert.IsFalse(ac.IsFailed);
            Assert.AreEqual(AsyncComputedState.Loading, ac.State);
            Assert.IsNull(ac.Exception);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = ac.Value);
        }

        [TestMethod]
        public void SuccessfulCompletionSetsValue()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task);

            tcs.SetResult(42);

            Assert.IsTrue(ac.IsSuccess);
            Assert.IsFalse(ac.IsLoading);
            Assert.IsFalse(ac.IsFailed);
            Assert.AreEqual(AsyncComputedState.Success, ac.State);
            Assert.AreEqual(42, ac.Value);
            Assert.IsNull(ac.Exception);
        }

        [TestMethod]
        public void FailedCompletionSetsException()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task);
            var expected = new InvalidOperationException("boom");

            tcs.SetException(expected);

            Assert.IsTrue(ac.IsFailed);
            Assert.IsFalse(ac.IsSuccess);
            Assert.IsFalse(ac.IsLoading);
            Assert.AreEqual(AsyncComputedState.Failed, ac.State);
            Assert.AreSame(expected, ac.Exception);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = ac.Value);
        }

        [TestMethod]
        public void RerunsWhenDependencyChanges()
        {
            Reference<int> dep = new(1);
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => Task.FromResult(dep.Value * 10));

            Assert.AreEqual(10, ac.Value);

            dep.Value = 2;
            ReactiveScheduler.Tick();

            Assert.AreEqual(20, ac.Value);
        }

        [TestMethod]
        public void CancelsPreviousOperation()
        {
            var tcs1 = new TaskCompletionSource<int>();
            var tcs2 = new TaskCompletionSource<int>();
            var tokens = new List<CancellationToken>();
            Reference<int> dep = new(0);

            var ac = new AsyncComputed<int>((CancellationToken ct) =>
            {
                tokens.Add(ct);
                return dep.Value == 0 ? tcs1.Task : tcs2.Task;
            });

            Assert.AreEqual(1, tokens.Count);
            Assert.IsFalse(tokens[0].IsCancellationRequested);

            dep.Value = 1;
            ReactiveScheduler.Tick();

            Assert.AreEqual(2, tokens.Count);
            Assert.IsTrue(tokens[0].IsCancellationRequested);
            Assert.IsFalse(tokens[1].IsCancellationRequested);

            tcs2.SetResult(99);
            Assert.AreEqual(99, ac.Value);
        }

        [TestMethod]
        public void DiscardsStaleResults()
        {
            var tcs1 = new TaskCompletionSource<int>();
            var tcs2 = new TaskCompletionSource<int>();
            Reference<int> dep = new(0);

            var ac = new AsyncComputed<int>(() =>
                dep.Value == 0 ? tcs1.Task : tcs2.Task);

            dep.Value = 1;
            ReactiveScheduler.Tick();

            tcs1.SetResult(100);
            Assert.IsFalse(ac.IsSuccess);

            tcs2.SetResult(200);
            Assert.AreEqual(200, ac.Value);
        }

        [TestMethod]
        public void WatchNotifiesOnStateChange()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task);
            var states = new List<AsyncComputedState>();

            ac.Watch(a => states.Add(a.State), immediate: true);

            Assert.AreEqual(1, states.Count);
            Assert.AreEqual(AsyncComputedState.Loading, states[0]);

            tcs.SetResult(42);

            ReactiveScheduler.Tick();

            Assert.AreEqual(2, states.Count);
            Assert.AreEqual(AsyncComputedState.Loading, states[0]);
            Assert.AreEqual(AsyncComputedState.Success, states[1]);
        }

        [TestMethod]
        public void WatchImmediateFiresRightAway()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task);
            var states = new List<AsyncComputedState>();

            ac.Watch(a => states.Add(a.State), immediate: true);

            Assert.AreEqual(1, states.Count);
            Assert.AreEqual(AsyncComputedState.Loading, states[0]);
        }

        [TestMethod]
        public void RecomputeTriggersRerun()
        {
            int callCount = 0;
            var tcs = new TaskCompletionSource<int>();

            var ac = new AsyncComputed<int>(() =>
            {
                Interlocked.Increment(ref callCount);
                return tcs.Task;
            });

            Assert.AreEqual(1, callCount);

            ac.Recompute();
            ReactiveScheduler.Tick();
            // tcs.SetResult(1);
            Assert.AreEqual(2, callCount);
            // Assert.AreEqual(1, ac.Value);
        }

        [TestMethod]
        public void DisposeStopsEffect()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task);
            var states = new List<AsyncComputedState>();

            ac.Watch(a => states.Add(a.State), immediate: true);

            Assert.AreEqual(1, states.Count);
            Assert.AreEqual(AsyncComputedState.Loading, states[0]);

            ac.Dispose();

            tcs.SetResult(42);

            ReactiveScheduler.Tick();

            Assert.AreEqual(1, states.Count);
        }

        [TestMethod]
        public void AccessAfterDisposeThrows()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task);

            ac.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = ac.State);
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = ac.Value);
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = ac.Exception);
        }

        [TestMethod]
        public void ConstructorWithAsyncFunction()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task);

            tcs.SetResult(7);
            Assert.AreEqual(7, ac.Value);
        }

        [TestMethod]
        public void ConstructorWithAsyncFunctionWithCancellation()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>((CancellationToken ct) => tcs.Task);

            tcs.SetResult(8);
            Assert.AreEqual(8, ac.Value);
        }

        [TestMethod]
        public void ConstructorWithFuncReturningAsyncFunction()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => (AsyncFunction<int>)(() => tcs.Task));

            tcs.SetResult(9);
            Assert.AreEqual(9, ac.Value);
        }

        [TestMethod]
        public void ConstructorWithFuncReturningAsyncFunctionWithCancellation()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => (AsyncFunctionWithCancellation<int>)(ct => tcs.Task));

            tcs.SetResult(10);
            Assert.AreEqual(10, ac.Value);
        }

        [TestMethod]
        public void NameDefaultsToEmpty()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task);

            Assert.AreEqual("", ac.Name);
        }

        [TestMethod]
        public void NameCanBeSet()
        {
            var tcs = new TaskCompletionSource<int>();
            var ac = new AsyncComputed<int>(() => tcs.Task, "MyComputed");

            Assert.AreEqual("MyComputed", ac.Name);
        }
    }
}
