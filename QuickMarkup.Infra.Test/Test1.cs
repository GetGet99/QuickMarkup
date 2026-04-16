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

        [TestMethod]
        public void ComputedShouldRerunImmedietely()
        {
            int rerunCount = 0;
            Reference<int> value = new(0);
            Computed<int> comp = new(() =>
            {
                rerunCount++;
                return value.Value + 1;
            });

            Assert.AreEqual(1, rerunCount);
            Assert.AreEqual(1, comp.Value);
            value.Value = 1;

            // without reading the value, it should not rerun yet.
            Assert.AreEqual(1, rerunCount);

            // this line should trigger rerun
            var newValue = comp.Value;
            Assert.AreEqual(2, rerunCount);
            Assert.AreEqual(2, newValue);

            // because value is not changed
            // this line should not trigger rerun
            newValue = comp.Value;
            Assert.AreEqual(2, rerunCount);
            Assert.AreEqual(2, newValue);
        }

        [TestMethod]
        public void ComputedShouldRerunImmedietelyWhileTicking()
        {
            int rerunCount = 0;
            Reference<int> value = new(0);
            Computed<int> comp = new(() =>
            {
                rerunCount++;
                return value.Value + 1;
            });

            Assert.AreEqual(1, rerunCount);
            Assert.AreEqual(1, comp.Value);

            OnNextTick(NextTickHandler);

            ReactiveScheduler.Tick();

            void NextTickHandler()
            {
                value.Value = 1;

                // without reading the value, it should not rerun yet.
                Assert.AreEqual(1, rerunCount);

                // this line should trigger rerun
                var newValue = comp.Value;
                Assert.AreEqual(2, rerunCount);
                Assert.AreEqual(2, newValue);

                // because value is not changed
                // this line should not trigger rerun
                newValue = comp.Value;
                Assert.AreEqual(2, rerunCount);
                Assert.AreEqual(2, newValue);
            }
        }

        [TestMethod]
        public void ScheduleCallbackRunsOnNextTick()
        {
            var runs = 0;

            ReactiveScheduler.ScheduleCallback(() => runs++);

            Assert.AreEqual(0, runs);

            ReactiveScheduler.Tick();

            Assert.AreEqual(1, runs);
        }

        [TestMethod]
        public void ReactiveScopeDisposesOwnedEffects()
        {
            Reference<int> value = new(0);
            var runs = 0;
            using ReactiveScope scope = new();

            scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                () => value.Value,
                _ => runs++));

            Assert.AreEqual(1, runs);

            scope.Dispose();
            value.Value = 1;
            ReactiveScheduler.Tick();

            Assert.AreEqual(1, runs);
        }

        [TestMethod]
        public void ConditionalBlockRecreatesBranchOnToggle()
        {
            Reference<bool> condition = new(false);
            List<string> target = [];
            UIBlockHost<string> host = new(new TargetUICollection<string>(target));
            var trueCreated = 0;
            var falseCreated = 0;

            var block = new ConditionalBlock<string>(
                new ReactiveScope(),
                () => condition.Value,
                () => new StaticBlock<string>(new ReactiveScope(), [$"true-{++trueCreated}"]),
                () => new StaticBlock<string>(new ReactiveScope(), [$"false-{++falseCreated}"]));

            host.AddBlock(block);

            CollectionAssert.AreEqual(new[] { "false-1" }, target);

            condition.Value = true;
            ReactiveScheduler.Tick();

            CollectionAssert.AreEqual(new[] { "true-1" }, target);

            condition.Value = false;
            ReactiveScheduler.Tick();

            CollectionAssert.AreEqual(new[] { "false-2" }, target);
        }

        [TestMethod]
        public void ConditionalBlockWithoutFalseBranchClearsCurrentBranch()
        {
            Reference<bool> condition = new(true);
            List<string> target = [];
            UIBlockHost<string> host = new(new TargetUICollection<string>(target));

            var block = new ConditionalBlock<string>(
                new ReactiveScope(),
                () => condition.Value,
                () => new StaticBlock<string>(new ReactiveScope(), ["true"]));

            host.AddBlock(block);

            CollectionAssert.AreEqual(new[] { "true" }, target);

            condition.Value = false;
            ReactiveScheduler.Tick();

            CollectionAssert.AreEqual(Array.Empty<string>(), target);
            Assert.AreEqual(0, block.Count);

            condition.Value = true;
            ReactiveScheduler.Tick();

            CollectionAssert.AreEqual(new[] { "true" }, target);
        }

        [TestMethod]
        public void ForBlockReconcilesCollectionOnNextTick()
        {
            System.Collections.ObjectModel.ObservableCollection<int> source = [1, 2];
            List<TextBox> target = [];
            UIBlockHost<TextBox> host = new(new TargetUICollection<TextBox>(target));

            var block = new ForBlock<int, TextBox>(
                new ReactiveScope(),
                source,
                itemRef => new StaticBlock<TextBox>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBox();
                        elements.Add(box);
                        scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                            () => itemRef.Value,
                            value => box.Text = value.ToString()));
                    }));

            host.AddBlock(block);

            AssertText(target, "1", "2");

            source.Add(3);
            source[0] = 10;

            AssertText(target, "1", "2");

            ReactiveScheduler.Tick();

            AssertText(target, "10", "2", "3");
        }
        [TestMethod]
        public void ForBlockUsesCurrentSiblingOffsetAfterEarlierBlockGrows()
        {
            System.Collections.ObjectModel.ObservableCollection<int> first = [1, 2];
            System.Collections.ObjectModel.ObservableCollection<int> second = [10];
            List<TextBox> target = [];
            UIBlockHost<TextBox> host = new(new TargetUICollection<TextBox>(target));

            host.AddBlock(new ForBlock<int, TextBox>(
                new ReactiveScope(),
                first,
                itemRef => new StaticBlock<TextBox>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBox();
                        elements.Add(box);
                        scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                            () => itemRef.Value,
                            value => box.Text = $"a{value}"));
                    })));

            host.AddBlock(new ForBlock<int, TextBox>(
                new ReactiveScope(),
                second,
                itemRef => new StaticBlock<TextBox>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBox();
                        elements.Add(box);
                        scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                            () => itemRef.Value,
                            value => box.Text = $"b{value}"));
                    })));

            AssertText(target, "a1", "a2", "b10");

            first.Add(3);
            ReactiveScheduler.Tick();
            second.Add(20);
            ReactiveScheduler.Tick();

            AssertText(target, "a1", "a2", "a3", "b10", "b20");
        }

        [TestMethod]
        public void ForBlockUsesCurrentSiblingOffsetWhenBothBlocksGrowBeforeOneTick()
        {
            System.Collections.ObjectModel.ObservableCollection<int> first = [1, 2];
            System.Collections.ObjectModel.ObservableCollection<int> second = [10];
            List<TextBox> target = [];
            UIBlockHost<TextBox> host = new(new TargetUICollection<TextBox>(target));

            host.AddBlock(new ForBlock<int, TextBox>(
                new ReactiveScope(),
                first,
                itemRef => new StaticBlock<TextBox>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBox();
                        elements.Add(box);
                        scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                            () => itemRef.Value,
                            value => box.Text = $"a{value}"));
                    })));

            host.AddBlock(new ForBlock<int, TextBox>(
                new ReactiveScope(),
                second,
                itemRef => new StaticBlock<TextBox>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBox();
                        elements.Add(box);
                        scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                            () => itemRef.Value,
                            value => box.Text = $"b{value}"));
                    })));

            AssertText(target, "a1", "a2", "b10");

            first.Add(3);
            second.Add(20);

            AssertText(target, "a1", "a2", "b10");

            ReactiveScheduler.Tick();

            AssertText(target, "a1", "a2", "a3", "b10", "b20");
        }

        static void AssertText(List<TextBox> boxes, params string[] expected)
        {
            var actual = boxes.Select(x => x.Text).ToArray();
            CollectionAssert.AreEqual(
                expected,
                actual,
                $"Expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}]");
        }

        void OnNextTick(Action callback)
        {
            RefEffect effect = new(_ => callback());
            ReactiveScheduler.ScheduleEffect(effect);
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

        class TextBox
        {
            public string Text { get; set; } = "";
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
