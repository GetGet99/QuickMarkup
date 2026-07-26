using QuickMarkup.Infra;

namespace QuickMarkup.Infra.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class AutoTickTests
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
        public void NoTickWhenNoEffectsScheduled()
        {
            bool tickRequested = false;
            SetupAutoTick(() => tickRequested = true);

            Reference<int> value = new(0);

            value.Value = 1;

            Assert.IsFalse(tickRequested);
        }
    }
}
