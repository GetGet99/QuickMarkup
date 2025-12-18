using QuickMarkup.Infra;

namespace QuickMarkup.Infra.Test
{
    [TestClass]
    public sealed class Test1
    {
        [TestInitialize]
        public void Setup()
        {
            ReactiveScheduler.ResetForCurrentThread();
            ReactiveScheduler.Instance.Value!.AutoTick = false;
            ReactiveScheduler.Instance.Value!.ContinueOnException = false;
        }
        void SetupAutoTick(Action schedulingCallback)
        {
            ReactiveScheduler.Instance.Value!.AutoTick = true;
            ReactiveScheduler.AddTickCallbackForCurrentThread(schedulingCallback);
        }
        void SetupImmedieteAutoTick()
        {
            ReactiveScheduler.Instance.Value!.AutoTick = true;
            ReactiveScheduler.AddTickCallbackForCurrentThread(ReactiveScheduler.Tick);
        }

        [TestMethod]
        public void ShouldUpdateWhenRefIsSetAfterTick()
        {
            Reference<int> value = new(0);

            NumberBox nb = new();


            var effect = ReferenceTracker.RunAndRerunOnReferenceChange(() => value.Value, x => nb.Value = x);

            Assert.DepsEqual(effect.Dependencies, (IReference)value);

            Assert.AreEqual(0, nb.Value);

            value.Value = 1;

            Assert.AreEqual(0, nb.Value);

            ReactiveScheduler.Tick();

            Assert.AreEqual(1, nb.Value);
        }

        [TestMethod]
        public void ShouldUpdateWhenRefIsSetOnAutoTick()
        {
            bool tickRequested = false;
            SetupAutoTick(() => tickRequested = true);

            Reference<int> value = new(0);

            NumberBox nb = new();

            Assert.IsFalse(tickRequested);
            var effect = ReferenceTracker.RunAndRerunOnReferenceChange(() => value.Value, x => nb.Value = x);

            Assert.IsFalse(tickRequested);
            Assert.DepsEqual(effect.Dependencies, value);
            Extension.DepsEqual(effect.Dependencies, value);
            Assert.AreEqual(0, nb.Value);


            value.Value = 1;
            Assert.IsTrue(tickRequested);
            Assert.AreEqual(0, nb.Value);
            
            if (tickRequested) ReactiveScheduler.Tick();

            Assert.AreEqual(1, nb.Value);
        }

        [TestMethod]
        public void EffectRunsOnlyOncePerTick()
        {
            Reference<int> value = new(0);
            int runCount = 0;

            var effect = ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                _ => runCount++);

            Assert.AreEqual(1, runCount);

            value.Value = 1;
            value.Value = 2;
            value.Value = 3;

            ReactiveScheduler.Tick();

            Assert.AreEqual(2, runCount); // initial + one rerun
        }

        [TestMethod]
        public void EffectReTracksDependencies()
        {
            Reference<int> a = new(1);
            Reference<int> b = new(2);
            Reference<bool> useA = new(true);

            int result = 0;

            var effect = ReferenceTracker.RunAndRerunOnReferenceChange(
                () => useA.Value ? a.Value : b.Value,
                x => result = x);

            Assert.AreEqual(1, result);
            Assert.DepsEqual(effect.Dependencies, useA, a);

            useA.Value = false;
            ReactiveScheduler.Tick();

            Assert.AreEqual(2, result);
            Assert.DepsEqual(effect.Dependencies, useA, b);
        }

        [TestMethod]
        public void DisposedEffectDoesNotRerun()
        {
            Reference<int> value = new(0);
            int runs = 0;

            var effect = ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                _ => runs++);

            Assert.AreEqual(1, runs);

            effect.Dispose();

            value.Value = 1;
            ReactiveScheduler.Tick();

            Assert.AreEqual(1, runs); // initial only
        }

        [TestMethod]
        public void ExceptionInOneEffectDoesNotStopOthers()
        {
            ReactiveScheduler.Instance.Value!.ContinueOnException = true;
            Reference<int> value = new(0);
            int safeRuns = 0;

            ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                _ => throw new InvalidOperationException());

            ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                _ => safeRuns++);
            
            Assert.AreEqual(1, safeRuns);

            value.Value = 1;
            ReactiveScheduler.Tick();

            Assert.AreEqual(2, safeRuns); // initial + rerun
        }

        [TestMethod]
        public void NestedUpdatesScheduleAnotherTick()
        {
            Reference<int> value = new(0);
            int runs = 0;

            ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                x =>
                {
                    runs++;
                    if (x == 0)
                        value.Value = 1;
                });

            ReactiveScheduler.Tick();

            // should run as "value.Value = 1;" is called
            ReactiveScheduler.Tick();

            Assert.AreEqual(2, runs);
        }

        [TestMethod]
        public void AutoTickIsScheduledOnlyOnce()
        {
            int scheduleCount = 0;
            SetupAutoTick(() => scheduleCount++);

            Reference<int> value = new(0);

            ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                _ => { });

            value.Value = 1;
            value.Value = 2;
            value.Value = 3;

            Assert.AreEqual(1, scheduleCount);
        }

        [TestMethod]
        public void MultipleEffectsUpdateIndependently()
        {
            Reference<int> a = new(1);
            Reference<int> b = new(10);

            int ra = 0, rb = 0;

            ReferenceTracker.RunAndRerunOnReferenceChange(() => a.Value, x => ra = x);
            ReferenceTracker.RunAndRerunOnReferenceChange(() => b.Value, x => rb = x);

            a.Value = 2;
            b.Value = 20;

            ReactiveScheduler.Tick();

            Assert.AreEqual(2, ra);
            Assert.AreEqual(20, rb);
        }

        [TestMethod]
        public void NoTickWhenNoEffectsScheduled()
        {
            bool tickRequested = false;
            SetupAutoTick(() => tickRequested = true);

            Reference<int> value = new(0);

            value.Value = 1;

            Assert.IsFalse(tickRequested);
        }

        class NumberBox
        {
            public event Action? ValueChanegd;
            public int Value
            {
                get => field;
                set
                {
                    field = value;
                    ValueChanegd?.Invoke();
                }
            }
        }
    }
}
static class Extension
{
    extension(Assert)
    {
        public static void DepsEqual(HashSet<IReference> deps, params ICollection<IReference> refs)
        {
            Assert.HasCount(refs.Count, deps);
            foreach (var r in refs)
            {
                if (!deps.Contains(r))
                    Assert.Fail();
            }
        }
    }
}