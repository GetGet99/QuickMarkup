using QuickMarkup.Infra;

namespace QuickMarkup.Infra.Test.Blocks
{
    [TestClass]
    public sealed class ForBlockTests
    {
        [TestInitialize]
        public void Setup()
        {
            ReactiveScheduler.ResetForCurrentThread();
            ReactiveScheduler.Instance.Value!.AutoTick = false;
            ReactiveScheduler.Instance.Value!.ContinueOnException = false;
        }

        [TestMethod]
        public void ForBlock_EmptyCollection_AddsNoChildren()
        {
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateTextBlock(itemRef, value => value, () => 0)));

            Assert.AreEqual("only", target[0].Text);
            Assert.HasCount(1, target);
        }

        [TestMethod]
        public void ForBlockReconcilesCollectionOnNextTick()
        {
            System.Collections.ObjectModel.ObservableCollection<int> source = [1, 2];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            var block = new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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
            var block = new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
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

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<KeyedItem, TextBlock, int>(
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
            var block = QuickMarkup.Infra.Blocks.ForBlock.Create<KeyedItem, TextBlock, int>(
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
        public void ForBlockIndexAwareFactoryRendersInitialIndexes()
        {
            System.Collections.ObjectModel.ObservableCollection<string> source = ["alpha", "beta"];
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
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

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
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

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<KeyedItem, TextBlock, int>(
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
            ForBlockTestSupport.AssertText(boxes, expected);
        }

        static StaticBlock<TextBlock> CreateTextBlock<T>(
            Reference<T> itemRef,
            Func<T, string> text,
            Func<int> instanceId)
        {
            return ForBlockTestSupport.CreateTextBlock(itemRef, text, instanceId);
        }

        static StaticBlock<TextBlock> CreateIndexedTextBlock(
            Reference<int> indexRef,
            Reference<string> itemRef)
        {
            return ForBlockTestSupport.CreateIndexedTextBlock(indexRef, itemRef);
        }

        static StaticBlock<TextBlock> CreateIndexedTextBlock<T>(
            Reference<int> indexRef,
            Reference<T> itemRef,
            Func<T, string> text)
        {
            return ForBlockTestSupport.CreateIndexedTextBlock(indexRef, itemRef, text);
        }

        sealed record KeyedItem(int Id, string Text);
    }
}
