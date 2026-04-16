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
        public void ConditionalSlotAssignsInitialBranchAndSwitches()
        {
            Reference<bool> condition = new(false);
            string? content = null;
            var trueCreated = 0;
            var falseCreated = 0;

            using var slot = new ConditionalSlot<string>(
                new ReactiveScope(),
                () => condition.Value,
                value => content = value,
                () => new ScopedValue<string>($"true-{++trueCreated}", new ReactiveScope()),
                () => new ScopedValue<string>($"false-{++falseCreated}", new ReactiveScope()));

            Assert.AreEqual("false-1", content);

            condition.Value = true;
            ReactiveScheduler.Tick();

            Assert.AreEqual("true-1", content);

            condition.Value = false;
            ReactiveScheduler.Tick();

            Assert.AreEqual("false-2", content);
        }

        [TestMethod]
        public void ConditionalSlotDisposesPreviousBranchEffects()
        {
            Reference<bool> condition = new(true);
            Reference<string> trueText = new("true");
            Reference<string> falseText = new("false");
            TextBlock? content = null;

            using var slot = new ConditionalSlot<TextBlock>(
                new ReactiveScope(),
                () => condition.Value,
                value => content = value,
                () => CreateScopedTextBlock(trueText),
                () => CreateScopedTextBlock(falseText));

            Assert.IsNotNull(content);
            var trueBlock = content;
            Assert.AreEqual("true", content.Text);

            condition.Value = false;
            ReactiveScheduler.Tick();

            Assert.IsNotNull(content);
            Assert.AreNotSame(trueBlock, content);
            Assert.AreEqual("false", content.Text);

            trueText.Value = "stale";
            falseText.Value = "updated false";
            ReactiveScheduler.Tick();

            Assert.AreEqual("true", trueBlock.Text);
            Assert.AreEqual("updated false", content.Text);
        }

        [TestMethod]
        public void ConditionalSlotDisposeStopsControllerAndCurrentBranchEffects()
        {
            Reference<bool> condition = new(true);
            Reference<string> text = new("active");
            TextBlock? content = null;

            var slot = new ConditionalSlot<TextBlock>(
                new ReactiveScope(),
                () => condition.Value,
                value => content = value,
                () => CreateScopedTextBlock(text),
                () => new ScopedValue<TextBlock>(new TextBlock { Text = "inactive" }, new ReactiveScope()));

            Assert.IsNotNull(content);
            var activeBlock = content;
            slot.Dispose();

            text.Value = "changed";
            condition.Value = false;
            ReactiveScheduler.Tick();

            Assert.AreSame(activeBlock, content);
            Assert.AreEqual("active", activeBlock.Text);
        }

        [TestMethod]
        public void ForBlockReconcilesCollectionOnNextTick()
        {
            System.Collections.ObjectModel.ObservableCollection<int> source = [1, 2];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            var block = new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => new StaticBlock<TextBlock>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBlock();
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
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                first,
                itemRef => new StaticBlock<TextBlock>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBlock();
                        elements.Add(box);
                        scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                            () => itemRef.Value,
                            value => box.Text = $"a{value}"));
                    })));

            host.AddBlock(new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                second,
                itemRef => new StaticBlock<TextBlock>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBlock();
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
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                first,
                itemRef => new StaticBlock<TextBlock>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBlock();
                        elements.Add(box);
                        scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                            () => itemRef.Value,
                            value => box.Text = $"a{value}"));
                    })));

            host.AddBlock(new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                second,
                itemRef => new StaticBlock<TextBlock>(
                    new ReactiveScope(),
                    (elements, scope) =>
                    {
                        var box = new TextBlock();
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

        [TestMethod]
        public void ForBlockImplicitIdentityPreservesBlockInstanceAcrossMove()
        {
            System.Collections.ObjectModel.ObservableCollection<int> source = [1, 2, 3];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var created = 0;

            host.AddBlock(new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateTextBlock(itemRef, value => value.ToString(), () => ++created)));

            var first = target.ToArray();

            source.Move(2, 0);
            ReactiveScheduler.Tick();

            Assert.AreSame(first[2], target[0]);
            Assert.AreSame(first[0], target[1]);
            Assert.AreSame(first[1], target[2]);
            Assert.AreEqual(3, created);
            AssertText(target, "3", "1", "2");
        }

        [TestMethod]
        public void ForBlockImplicitIdentityHandlesDuplicateSourceValues()
        {
            System.Collections.ObjectModel.ObservableCollection<int> source = [1, 1, 2];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            var first = target.ToArray();

            source.Move(1, 0);
            ReactiveScheduler.Tick();

            Assert.AreSame(first[1], target[0]);
            Assert.AreSame(first[0], target[1]);
            Assert.AreSame(first[2], target[2]);
            AssertText(target, "1", "1", "2");
        }

        [TestMethod]
        public void ForBlockImplicitIdentityCreatesNewBlockOnReplace()
        {
            System.Collections.ObjectModel.ObservableCollection<int> source = [1, 2, 3];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            var first = target.ToArray();

            source[1] = 20;
            ReactiveScheduler.Tick();

            Assert.AreSame(first[0], target[0]);
            Assert.AreNotSame(first[1], target[1]);
            Assert.AreSame(first[2], target[2]);
            AssertText(target, "1", "20", "3");
        }

        [TestMethod]
        public void ForBlockImplicitRefreshRecreatesAllBlocks()
        {
            System.Collections.ObjectModel.ObservableCollection<int> source = [1, 2];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var block = new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateTextBlock(itemRef, value => value.ToString(), () => 0));

            host.AddBlock(block);
            var first = target.ToArray();

            block.Refresh();
            ReactiveScheduler.Tick();

            Assert.AreNotSame(first[0], target[0]);
            Assert.AreNotSame(first[1], target[1]);
            AssertText(target, "1", "2");
        }

        [TestMethod]
        public void ForBlockImplicitIdentityRecreatesAllBlocksAcrossResetLikeChanges()
        {
            System.Collections.ObjectModel.ObservableCollection<int> source = [1, 2];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            var first = target.ToArray();

            source.Clear();
            source.Add(1);
            source.Add(2);
            ReactiveScheduler.Tick();

            Assert.AreNotSame(first[0], target[0]);
            Assert.AreNotSame(first[1], target[1]);
            AssertText(target, "1", "2");
        }

        [TestMethod]
        public void ForBlockExplicitKeysPreserveBlockInstanceAcrossResetLikeChanges()
        {
            System.Collections.ObjectModel.ObservableCollection<KeyedItem> source = [
                new(1, "one"),
                new(2, "two")
            ];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(ForBlock.Create<KeyedItem, TextBlock, int>(
                new ReactiveScope(),
                source,
                item => item.Id,
                itemRef => CreateTextBlock(itemRef, item => item.Text, () => 0)));

            var first = target.ToArray();

            source.Clear();
            source.Add(new(2, "two updated"));
            source.Add(new(1, "one updated"));
            ReactiveScheduler.Tick();

            Assert.AreSame(first[1], target[0]);
            Assert.AreSame(first[0], target[1]);
            AssertText(target, "two updated", "one updated");
        }

        [TestMethod]
        public void ForBlockExplicitDuplicateKeysThrow()
        {
            System.Collections.ObjectModel.ObservableCollection<KeyedItem> source = [
                new(1, "one"),
                new(1, "duplicate")
            ];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var block = ForBlock.Create<KeyedItem, TextBlock, int>(
                new ReactiveScope(),
                source,
                item => item.Id,
                itemRef => CreateTextBlock(itemRef, item => item.Text, () => 0));

            try
            {
                host.AddBlock(block);
                Assert.Fail("Expected duplicate keys to throw.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        [TestMethod]
        public void ExplicitKeyManagerDoesNotCaptureReactiveDependencies()
        {
            Reference<int> key = new(1);
            var source = new[] { 42 };
            var effect = ReferenceTracker.RunAndRerunOnReferenceChange(
                () =>
                {
                    var manager = ForKeyManager.Create<int, int>(_ => key.Value);
                    manager.Initialize(source);
                    return manager.Keys[0];
                },
                _ => { });

            Assert.HasCount(0, effect.Dependencies);
        }

        [TestMethod]
        public void ForBlockIndexAwareFactoryRendersInitialIndexes()
        {
            System.Collections.ObjectModel.ObservableCollection<string> source = ["alpha", "beta"];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                (indexRef, itemRef) => CreateIndexedTextBlock(indexRef, itemRef)));

            AssertText(target, "1. alpha", "2. beta");
        }

        [TestMethod]
        public void ForBlockIndexAwareFactoryUpdatesIndexesAcrossMove()
        {
            System.Collections.ObjectModel.ObservableCollection<string> source = ["alpha", "beta", "gamma"];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                (indexRef, itemRef) => CreateIndexedTextBlock(indexRef, itemRef)));

            var first = target.ToArray();

            source.Move(2, 0);
            ReactiveScheduler.Tick();

            Assert.AreSame(first[2], target[0]);
            Assert.AreSame(first[0], target[1]);
            Assert.AreSame(first[1], target[2]);
            AssertText(target, "1. gamma", "2. alpha", "3. beta");
        }

        [TestMethod]
        public void ForBlockIndexAwareFactoryDefersAddUntilNextTick()
        {
            System.Collections.ObjectModel.ObservableCollection<string> source = ["alpha"];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                (indexRef, itemRef) => CreateIndexedTextBlock(indexRef, itemRef)));

            source.Add("beta");

            AssertText(target, "1. alpha");

            ReactiveScheduler.Tick();

            AssertText(target, "1. alpha", "2. beta");
        }

        [TestMethod]
        public void ForBlockExplicitKeysWithIndexAwareFactoryPreserveBlocksAndUpdateIndexes()
        {
            System.Collections.ObjectModel.ObservableCollection<KeyedItem> source = [
                new(1, "one"),
                new(2, "two")
            ];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(ForBlock.Create<KeyedItem, TextBlock, int>(
                new ReactiveScope(),
                source,
                item => item.Id,
                (indexRef, itemRef) => CreateIndexedTextBlock(indexRef, itemRef, item => item.Text)));

            var first = target.ToArray();

            source.Clear();
            source.Add(new(2, "two updated"));
            source.Add(new(1, "one updated"));
            ReactiveScheduler.Tick();

            Assert.AreSame(first[1], target[0]);
            Assert.AreSame(first[0], target[1]);
            AssertText(target, "1. two updated", "2. one updated");
        }

        static void AssertText(List<TextBlock> boxes, params string[] expected)
        {
            var actual = boxes.Select(x => x.Text).ToArray();
            CollectionAssert.AreEqual(
                expected,
                actual,
                $"Expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}]");
        }

        static StaticBlock<TextBlock> CreateTextBlock<T>(
            Reference<T> itemRef,
            Func<T, string> text,
            Func<int> instanceId)
        {
            return new StaticBlock<TextBlock>(
                new ReactiveScope(),
                (elements, scope) =>
                {
                    var box = new TextBlock { InstanceId = instanceId() };
                    elements.Add(box);
                    scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                        () => itemRef.Value,
                        value => box.Text = text(value)));
                });
        }

        static StaticBlock<TextBlock> CreateIndexedTextBlock(
            Reference<int> indexRef,
            Reference<string> itemRef)
        {
            return CreateIndexedTextBlock(indexRef, itemRef, item => item);
        }

        static StaticBlock<TextBlock> CreateIndexedTextBlock<T>(
            Reference<int> indexRef,
            Reference<T> itemRef,
            Func<T, string> text)
        {
            return new StaticBlock<TextBlock>(
                new ReactiveScope(),
                (elements, scope) =>
                {
                    var box = new TextBlock();
                    elements.Add(box);
                    scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                        () => $"{indexRef.Value + 1}. {text(itemRef.Value)}",
                        value => box.Text = value));
                });
        }

        static ScopedValue<TextBlock> CreateScopedTextBlock(Reference<string> text)
        {
            var scope = new ReactiveScope();
            var block = new TextBlock();
            scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                () => text.Value,
                value => block.Text = value));
            return new ScopedValue<TextBlock>(block, scope);
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

        class TextBlock
        {
            public int InstanceId { get; set; }
            public string Text { get; set; } = "";
        }

        sealed record KeyedItem(int Id, string Text);
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
