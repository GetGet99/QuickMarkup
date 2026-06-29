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
        public void ReferenceWatchRunsOnlyAfterChangeUnlessImmediate()
        {
            Reference<int> value = new(1);
            List<int> seen = [];

            value.Watch(seen.Add);

            Assert.HasCount(0, seen);

            value.Value = 2;
            ReactiveScheduler.Tick();

            CollectionAssert.AreEqual(new[] { 2 }, seen);
        }

        [TestMethod]
        public void ComputedWatchImmediateReadsCurrentValueAndTracksChanges()
        {
            Reference<int> value = new(1);
            Computed<int> doubled = new(() => value.Value * 2);
            List<int> seen = [];

            doubled.Watch(seen.Add, immediete: true);

            CollectionAssert.AreEqual(new[] { 2 }, seen);

            value.Value = 3;
            ReactiveScheduler.Tick();

            CollectionAssert.AreEqual(new[] { 2 }, seen);

            ReactiveScheduler.Tick();

            CollectionAssert.AreEqual(new[] { 2, 6 }, seen);
        }

        [TestMethod]
        public void NoCapturePreventsDependencyTracking()
        {
            Reference<int> tracked = new(1);
            Reference<int> ignored = new(10);
            int result = 0;

            var effect = ReferenceTracker.RunAndRerunOnReferenceChange(
                () => tracked.Value + ReferenceTracker.NoCapture(() => ignored.Value),
                x => result = x);

            Assert.AreEqual(11, result);
            Assert.DepsEqual(effect.Dependencies, tracked);

            ignored.Value = 20;
            ReactiveScheduler.Tick();

            Assert.AreEqual(11, result);

            tracked.Value = 2;
            ReactiveScheduler.Tick();

            Assert.AreEqual(22, result);
        }

        [TestMethod]
        public void QuickRefsEffectRunsOnNextTickForSelectedReferences()
        {
            Reference<int> a = new(1);
            Reference<int> b = new(10);
            int runs = 0;
            int sum = 0;

            using var effect = QuickRefs.Effect(() =>
            {
                runs++;
                sum = a.Value + b.Value;
            }, a);

            Assert.AreEqual(0, runs);

            ReactiveScheduler.Tick();

            Assert.AreEqual(1, runs);
            Assert.AreEqual(11, sum);

            b.Value = 20;
            ReactiveScheduler.Tick();

            Assert.AreEqual(1, runs);

            a.Value = 2;
            ReactiveScheduler.Tick();

            Assert.AreEqual(2, runs);
            Assert.AreEqual(22, sum);
        }

        [TestMethod]
        public void TargetUICollectionMoveUsesFinalIndexWhenMovingForward()
        {
            List<string> target = ["A", "B", "C", "D"];
            TargetUICollection<string> collection = new(target);

            collection.Move(0, 2);

            CollectionAssert.AreEqual(new[] { "B", "C", "A", "D" }, target);
        }

        [TestMethod]
        public void TargetUICollectionMoveUsesFinalIndexWhenMovingBackward()
        {
            List<string> target = ["A", "B", "C", "D"];
            TargetUICollection<string> collection = new(target);

            collection.Move(2, 0);

            CollectionAssert.AreEqual(new[] { "C", "A", "B", "D" }, target);
        }

        [TestMethod]
        public void TargetUICollectionMoveSameIndexIsNoOp()
        {
            List<string> target = ["A", "B", "C"];
            TargetUICollection<string> collection = new(target);

            collection.Move(1, 1);

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, target);
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
        public void NestedConditionalSlotAssignsSelectedNestedBranch()
        {
            Reference<bool> outerCondition = new(true);
            Reference<bool> innerCondition = new(false);
            TextBlock? content = null;

            using var slot = new ConditionalSlot<TextBlock>(
                new ReactiveScope(),
                () => outerCondition.Value,
                value => content = value,
                () => CreateNestedConditionalScopedTextBlock(
                    innerCondition,
                    new Reference<string>("inner true"),
                    new Reference<string>("inner false"),
                    value => content = value),
                () => new ScopedValue<TextBlock>(new TextBlock { Text = "outer false" }, new ReactiveScope()));

            Assert.IsNotNull(content);
            Assert.AreEqual("inner false", content.Text);

            innerCondition.Value = true;
            ReactiveScheduler.Tick();

            Assert.IsNotNull(content);
            Assert.AreEqual("inner true", content.Text);
        }

        [TestMethod]
        public void NestedConditionalSlotOuterSwitchDisposesActiveInnerSlot()
        {
            Reference<bool> outerCondition = new(true);
            Reference<bool> innerCondition = new(true);
            Reference<string> innerTrueText = new("inner true");
            Reference<string> innerFalseText = new("inner false");
            TextBlock? content = null;

            using var slot = new ConditionalSlot<TextBlock>(
                new ReactiveScope(),
                () => outerCondition.Value,
                value => content = value,
                () => CreateNestedConditionalScopedTextBlock(
                    innerCondition,
                    innerTrueText,
                    innerFalseText,
                    value => content = value),
                () => new ScopedValue<TextBlock>(new TextBlock { Text = "outer false" }, new ReactiveScope()));

            Assert.IsNotNull(content);
            var innerTrueBlock = content;
            Assert.AreEqual("inner true", content.Text);

            outerCondition.Value = false;
            ReactiveScheduler.Tick();

            Assert.IsNotNull(content);
            Assert.AreEqual("outer false", content.Text);

            innerCondition.Value = false;
            innerTrueText.Value = "stale";
            innerFalseText.Value = "should not show";
            ReactiveScheduler.Tick();

            Assert.AreEqual("inner true", innerTrueBlock.Text);
            Assert.AreEqual("outer false", content.Text);
        }

        [TestMethod]
        public void NestedConditionalSlotInnerSwitchDisposesOnlyReplacedInnerBranch()
        {
            Reference<bool> outerCondition = new(true);
            Reference<bool> innerCondition = new(true);
            Reference<string> innerTrueText = new("inner true");
            Reference<string> innerFalseText = new("inner false");
            TextBlock? content = null;

            using var slot = new ConditionalSlot<TextBlock>(
                new ReactiveScope(),
                () => outerCondition.Value,
                value => content = value,
                () => CreateNestedConditionalScopedTextBlock(
                    innerCondition,
                    innerTrueText,
                    innerFalseText,
                    value => content = value),
                () => new ScopedValue<TextBlock>(new TextBlock { Text = "outer false" }, new ReactiveScope()));

            Assert.IsNotNull(content);
            var innerTrueBlock = content;

            innerCondition.Value = false;
            ReactiveScheduler.Tick();

            Assert.IsNotNull(content);
            var innerFalseBlock = content;
            Assert.AreNotSame(innerTrueBlock, innerFalseBlock);
            Assert.AreEqual("inner false", innerFalseBlock.Text);

            innerTrueText.Value = "stale";
            innerFalseText.Value = "updated false";
            ReactiveScheduler.Tick();

            Assert.AreEqual("inner true", innerTrueBlock.Text);
            Assert.AreEqual("updated false", innerFalseBlock.Text);
        }

        [TestMethod]
        public void FragmentBlockMountsMultipleChildrenAsOneLogicalBlock()
        {
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            var fragment = new FragmentBlock<TextBlock>(
                new ReactiveScope(),
                (fragmentHost, _) =>
                {
                    fragmentHost.AddBlock(new StaticBlock<TextBlock>(
                        new ReactiveScope(),
                        [new TextBlock { Text = "a" }]));
                    fragmentHost.AddBlock(new StaticBlock<TextBlock>(
                        new ReactiveScope(),
                        [new TextBlock { Text = "b" }]));
                });

            host.AddBlock(fragment);

            Assert.AreEqual(2, fragment.Count);
            AssertText(target, "a", "b");
        }

        [TestMethod]
        public void FragmentBlockSupportsNestedConditionalAndForBlocks()
        {
            Reference<bool> showHeader = new(true);
            System.Collections.ObjectModel.ObservableCollection<string> items = ["one"];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new FragmentBlock<TextBlock>(
                new ReactiveScope(),
                (fragmentHost, _) =>
                {
                    fragmentHost.AddBlock(new ConditionalBlock<TextBlock>(
                        new ReactiveScope(),
                        () => showHeader.Value,
                        () => new StaticBlock<TextBlock>(
                            new ReactiveScope(),
                            [new TextBlock { Text = "header" }])));
                    fragmentHost.AddBlock(new ForBlock<string, TextBlock>(
                        new ReactiveScope(),
                        items,
                        itemRef => CreateTextBlock(itemRef, item => item, () => 0)));
                }));

            AssertText(target, "header", "one");

            showHeader.Value = false;
            items.Add("two");
            ReactiveScheduler.Tick();

            AssertText(target, "one", "two");
        }

        [TestMethod]
        public void FragmentBlockPreservesChildrenAcrossDetachAndRemount()
        {
            System.Collections.ObjectModel.ObservableCollection<int> source = [1, 2];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => new FragmentBlock<TextBlock>(
                    new ReactiveScope(),
                    (fragmentHost, _) =>
                    {
                        fragmentHost.AddBlock(CreateTextBlock(
                            itemRef,
                            value => value.ToString(),
                            () => 0));
                    })));

            var first = target.ToArray();

            source.Move(1, 0);
            ReactiveScheduler.Tick();

            Assert.AreSame(first[1], target[0]);
            Assert.AreSame(first[0], target[1]);
            AssertText(target, "2", "1");
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

        static ScopedValue<TextBlock> CreateNestedConditionalScopedTextBlock(
            Reference<bool> condition,
            Reference<string> trueText,
            Reference<string> falseText,
            Action<TextBlock> setValue)
        {
            var scope = new ReactiveScope();
            TextBlock value = null!;
            var slot = new ConditionalSlot<TextBlock>(
                new ReactiveScope(),
                () => condition.Value,
                next =>
                {
                    value = next;
                    setValue(next);
                },
                () => CreateScopedTextBlock(trueText),
                () => CreateScopedTextBlock(falseText));
            scope.Add(slot);
            return new ScopedValue<TextBlock>(value, scope);
        }

        [TestMethod]
        public void ReferenceWithNullValue_StoresAndReturnsNull()
        {
            Reference<string?> value = new(null);
            Assert.IsNull(value.Value);
        }

        [TestMethod]
        public void ReferenceWithNullInitial_CanBeSetToString()
        {
            Reference<string?> value = new(null);
            value.Value = "hello";
            Assert.AreEqual("hello", value.Value);
        }

        [TestMethod]
        public void ComputedWithThrowingExpression_ThrowsDuringConstruction()
        {
            Func<int> throwing = () => throw new InvalidOperationException("fail");
            try
            {
                _ = new Computed<int>(throwing);
                Assert.Fail("Expected InvalidOperationException during construction");
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public void ComputedWithDivisionByZero_ThrowsDuringConstruction()
        {
            Reference<int> divisor = new(0);
            try
            {
                _ = new Computed<int>(() => 10 / divisor.Value);
                Assert.Fail("Expected DivideByZeroException during construction");
            }
            catch (DivideByZeroException) { }
        }

        [TestMethod]
        public void ForBlock_EmptyCollection_AddsNoChildren()
        {
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<string, TextBlock>(
                new ReactiveScope(),
                [],
                _ => new StaticBlock<TextBlock>(new ReactiveScope(), [])));

            Assert.IsEmpty(target);
        }

        [TestMethod]
        public void ForBlock_SingleItem_AddsOneChild()
        {
            System.Collections.ObjectModel.ObservableCollection<string> source = ["only"];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateTextBlock(itemRef, value => value, () => 0)));

            Assert.AreEqual("only", target[0].Text);
            Assert.HasCount(1, target);
        }

        [TestMethod]
        public void FragmentBlock_Empty_AddsNoChildren()
        {
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new FragmentBlock<TextBlock>(
                new ReactiveScope(),
                (_, _) => { }));

            Assert.IsEmpty(target);
        }

        [TestMethod]
        public void FragmentBlock_AddsBlocksAfterHostIsReady()
        {
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new FragmentBlock<TextBlock>(
                new ReactiveScope(),
                (fragmentHost, _) =>
                {
                    fragmentHost.AddBlock(new StaticBlock<TextBlock>(
                        new ReactiveScope(),
                        [new TextBlock { Text = "alpha" }]));
                    fragmentHost.AddBlock(new StaticBlock<TextBlock>(
                        new ReactiveScope(),
                        [new TextBlock { Text = "beta" }]));
                }));

            Assert.AreEqual("alpha", target[0].Text);
            Assert.AreEqual("beta", target[1].Text);
        }

        [TestMethod]
        public void ConditionalBlock_ToggleBackAndForth_MultipleTimes()
        {
            Reference<bool> condition = new(true);
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var trueCreated = 0;
            var falseCreated = 0;

            var block = new ConditionalBlock<TextBlock>(
                new ReactiveScope(),
                () => condition.Value,
                () => new StaticBlock<TextBlock>(new ReactiveScope(), [new TextBlock { Text = $"true-{++trueCreated}" }]),
                () => new StaticBlock<TextBlock>(new ReactiveScope(), [new TextBlock { Text = $"false-{++falseCreated}" }]));

            host.AddBlock(block);

            Assert.HasCount(1, target);
            Assert.AreEqual("true-1", target[0].Text);

            for (int i = 0; i < 3; i++)
            {
                condition.Value = false;
                ReactiveScheduler.Tick();
                Assert.HasCount(1, target);
                Assert.AreEqual($"false-{i + 1}", target[0].Text);

                condition.Value = true;
                ReactiveScheduler.Tick();
                Assert.HasCount(1, target);
                Assert.AreEqual($"true-{i + 2}", target[0].Text);
            }
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
