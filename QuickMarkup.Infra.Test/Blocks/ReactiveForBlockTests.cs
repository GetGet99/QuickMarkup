using System.Collections.ObjectModel;
using QuickMarkup.Infra;
using QuickMarkup.Infra.Collections;

namespace QuickMarkup.Infra.Test.Blocks
{
    [TestClass]
    public sealed class ReactiveForBlockTests
    {
        [TestInitialize]
        public void Setup()
        {
            ReactiveScheduler.ResetForCurrentThread();
            ReactiveScheduler.Instance.Value!.AutoTick = false;
            ReactiveScheduler.Instance.Value!.ContinueOnException = false;
        }

        [TestMethod]
        public void ReactiveForBlock_RendersInitialItems()
        {
            var source = new ReactiveList<string> { "alpha", "beta" };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0)));

            ForBlockTestSupport.AssertText(target, "alpha", "beta");
        }

        [TestMethod]
        public void ReactiveForBlock_Add_DefersUntilTickAndReusesExistingBlocks()
        {
            var source = new ReactiveList<int> { 1, 2 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            var first = target.ToArray();

            source.Add(3);

            ForBlockTestSupport.AssertText(target, "1", "2");

            TickReactive();

            Assert.AreSame(first[0], target[0]);
            Assert.AreSame(first[1], target[1]);
            Assert.AreNotSame(first[0], target[2]);
            ForBlockTestSupport.AssertText(target, "1", "2", "3");
        }

        [TestMethod]
        public void ReactiveForBlock_Insert_ReusesExistingBlocks()
        {
            var source = new ReactiveList<int> { 1, 2, 3 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            var first = target.ToArray();

            source.Insert(0, 0);
            TickReactive();

            Assert.AreSame(first[0], target[1]);
            Assert.AreSame(first[1], target[2]);
            Assert.AreSame(first[2], target[3]);
            ForBlockTestSupport.AssertText(target, "0", "1", "2", "3");
        }

        [TestMethod]
        public void ReactiveForBlock_RemoveAt_ReusesSurvivingBlocks()
        {
            var source = new ReactiveList<int> { 1, 2, 3 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            var first = target.ToArray();

            source.RemoveAt(1);
            TickReactive();

            Assert.AreSame(first[0], target[0]);
            Assert.AreSame(first[2], target[1]);
            ForBlockTestSupport.AssertText(target, "1", "3");
        }

        [TestMethod]
        public void ReactiveForBlock_Replace_RecreatesOnlyReplacedBlock()
        {
            var source = new ReactiveList<int> { 1, 2, 3 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            var first = target.ToArray();

            source[1] = 20;
            TickReactive();

            Assert.AreSame(first[0], target[0]);
            Assert.AreNotSame(first[1], target[1]);
            Assert.AreSame(first[2], target[2]);
            ForBlockTestSupport.AssertText(target, "1", "20", "3");
        }

        [TestMethod]
        public void ReactiveForBlock_MoveViaRemoveAndInsert_PreservesBlockInstances()
        {
            var source = new ReactiveList<int> { 1, 2, 3 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            var first = target.ToArray();

            var moving = source[2];
            source.RemoveAt(2);
            source.Insert(0, moving);
            TickReactive();

            Assert.AreSame(first[2], target[0]);
            Assert.AreSame(first[0], target[1]);
            Assert.AreSame(first[1], target[2]);
            ForBlockTestSupport.AssertText(target, "3", "1", "2");
        }

        [TestMethod]
        public void ReactiveForBlock_ClearThenAdd_RecreatesAllBlocks()
        {
            var source = new ReactiveList<int> { 1, 2 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            var first = target.ToArray();

            source.Clear();
            TickReactive();

            Assert.IsEmpty(target);

            source.Add(1);
            source.Add(2);
            TickReactive();

            Assert.AreNotSame(first[0], target[0]);
            Assert.AreNotSame(first[1], target[1]);
            ForBlockTestSupport.AssertText(target, "1", "2");
        }

        [TestMethod]
        public void ReactiveForBlock_DistinctReferenceItemsWithEqualValues_Work()
        {
            var a = new string("abc".ToCharArray());
            var b = new string("abc".ToCharArray());
            var source = new ReactiveList<string> { a, b };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0)));

            var first = target.ToArray();

            source.Add(a);
            TickReactive();

            Assert.AreSame(first[0], target[0]);
            Assert.AreSame(first[1], target[1]);
            Assert.AreNotSame(first[0], target[2]);
            ForBlockTestSupport.AssertText(target, "abc", "abc", "abc");
        }

        [TestMethod]
        public void ReactiveForBlock_ValueTypeDuplicates_RenderDistinctBlocks()
        {
            var source = new ReactiveList<int> { 1, 1 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value.ToString(), () => 0)));

            ForBlockTestSupport.AssertText(target, "1", "1");
        }

        [TestMethod]
        public void ReactiveForBlock_IndexAwareFactory_UpdatesIndexes()
        {
            var source = new ReactiveList<string> { "alpha", "beta" };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                (indexRef, itemRef) => ForBlockTestSupport.CreateIndexedTextBlock(indexRef, itemRef)));

            ForBlockTestSupport.AssertText(target, "1. alpha", "2. beta");

            source.Add("gamma");
            TickReactive();

            ForBlockTestSupport.AssertText(target, "1. alpha", "2. beta", "3. gamma");

            var moving = source[2];
            source.RemoveAt(2);
            source.Insert(0, moving);
            TickReactive();

            ForBlockTestSupport.AssertText(target, "1. gamma", "2. alpha", "3. beta");
        }

        [TestMethod]
        public void ReactiveForBlock_ExplicitKeys_KeyRefMutation_RecreatesThatBlock()
        {
            var item1 = new ReactiveKeyedItem(new(1), new("one"));
            var item2 = new ReactiveKeyedItem(new(2), new("two"));
            var source = new ReactiveList<ReactiveKeyedItem> { item1, item2 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var created = 0;

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<ReactiveKeyedItem, TextBlock, int>(
                new ReactiveScope(),
                source,
                (item, _) => item.Id.Value,
                itemRef => { created++; return CreateKeyedTextBlock(itemRef); }));

            var first = target.ToArray();

            item1.Id.Value = 3;
            TickReactive();

            Assert.AreEqual(3, created);
            Assert.AreNotSame(first[0], target[0]);
            Assert.AreSame(first[1], target[1]);
            ForBlockTestSupport.AssertText(target, "one", "two");
        }

        [TestMethod]
        public void ReactiveForBlock_ItemReferenceMutation_UpdatesThatBlock()
        {
            var item1 = new ReactiveKeyedItem(new(1), new("one"));
            var item2 = new ReactiveKeyedItem(new(2), new("two"));
            var source = new ReactiveList<ReactiveKeyedItem> { item1, item2 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<ReactiveKeyedItem, TextBlock, int>(
                new ReactiveScope(),
                source,
                (item, _) => item.Id.Value,
                itemRef => CreateKeyedTextBlock(itemRef)));

            item2.Text.Value = "TWO";
            ReactiveScheduler.Tick();

            ForBlockTestSupport.AssertText(target, "one", "TWO");
        }

        [TestMethod]
        public void ReactiveForBlock_ExplicitKeys_SwapSource_ResetsAndRecreates()
        {
            var initial = new ObservableCollection<string> { "a", "b" };
            Reference<IReadOnlyList<string>> sourceRef = new(initial);
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var created = 0;

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<string, TextBlock>(
                new ReactiveScope(),
                () => sourceRef.Value,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => ++created)));

            var first = target.ToArray();

            sourceRef.Value = new ReactiveList<string> { "a", "b" };
            TickReactive();

            Assert.AreEqual(4, created);
            Assert.AreNotSame(first[0], target[0]);
            Assert.AreNotSame(first[1], target[1]);
            ForBlockTestSupport.AssertText(target, "a", "b");
        }

        [TestMethod]
        public void ReactiveForBlock_NullSourceGetter_RendersNothing()
        {
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<string, TextBlock>(
                new ReactiveScope(),
                () => null!,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0)));

            Assert.IsEmpty(target);
        }

        [TestMethod]
        public void ReactiveForBlock_Refresh_PreservesIdentityForUnchangedValues()
        {
            var source = new ReactiveList<int> { 1, 2 };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var block = new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value.ToString(), () => 0));

            host.AddBlock(block);
            var first = target.ToArray();

            block.Refresh();
            TickReactive();

            Assert.AreSame(first[0], target[0]);
            Assert.AreSame(first[1], target[1]);
            ForBlockTestSupport.AssertText(target, "1", "2");
        }

        [TestMethod]
        public void ReactiveForBlock_UnmountThenRemount_Rerenders()
        {
            var source = new ReactiveList<string> { "a", "b" };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var block = new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0));

            host.AddBlock(block);
            ForBlockTestSupport.AssertText(target, "a", "b");

            block.Unmount();

            Assert.IsEmpty(target);

            host.AddBlock(block);
            ForBlockTestSupport.AssertText(target, "a", "b");
        }

        [TestMethod]
        public void ReactiveForBlock_Dispose_RemovesChildrenAndStopsReacting()
        {
            var source = new ReactiveList<string> { "a", "b" };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var block = new QuickMarkup.Infra.Blocks.ForBlock<string, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0));

            host.AddBlock(block);
            ForBlockTestSupport.AssertText(target, "a", "b");

            block.Dispose();

            Assert.IsEmpty(target);

            source.Add("c");
            ReactiveScheduler.Tick();

            Assert.IsEmpty(target);
        }

        static void TickReactive()
        {
            ReactiveScheduler.Tick();
            ReactiveScheduler.Tick();
        }

        [TestMethod]
        public void ReactiveForBlock_SingleAdd_MountsOnlyTheNewBlock()
        {
            var source = new ReactiveList<int> { 1, 2, 3 };
            var counter = new BlockLifecycleCounter();
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateCountingTextBlock(itemRef, value => value.ToString(), counter)));

            ForBlockTestSupport.AssertText(target, "1", "2", "3");
            Assert.AreEqual(3, counter.Created);
            Assert.AreEqual(3, counter.Mounted);

            source.Add(4);
            TickReactive();

            ForBlockTestSupport.AssertText(target, "1", "2", "3", "4");
            Assert.AreEqual(4, counter.Created);
            Assert.AreEqual(4, counter.Mounted);
            Assert.AreEqual(0, counter.Unmounted);
            Assert.AreEqual(0, counter.Disposed);
        }

        [TestMethod]
        public void ReactiveForBlock_SingleRemove_UnmountsOnlyTheRemovedBlock()
        {
            var source = new ReactiveList<int> { 1, 2, 3 };
            var counter = new BlockLifecycleCounter();
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateCountingTextBlock(itemRef, value => value.ToString(), counter)));

            source.RemoveAt(1);
            TickReactive();

            ForBlockTestSupport.AssertText(target, "1", "3");
            Assert.AreEqual(3, counter.Created);
            Assert.AreEqual(3, counter.Mounted);
            Assert.AreEqual(1, counter.Unmounted);
            Assert.AreEqual(1, counter.Disposed);
        }

        [TestMethod]
        public void ReactiveForBlock_Move_MovesBlocksWithoutRemounting()
        {
            var source = new ReactiveList<int> { 1, 2, 3 };
            var counter = new BlockLifecycleCounter();
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(new QuickMarkup.Infra.Blocks.ForBlock<int, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => CreateCountingTextBlock(itemRef, value => value.ToString(), counter)));

            var moving = source[2];
            source.RemoveAt(2);
            source.Insert(0, moving);
            TickReactive();

            ForBlockTestSupport.AssertText(target, "3", "1", "2");
            Assert.AreEqual(3, counter.Created);
            Assert.AreEqual(3, counter.Mounted);
            Assert.AreEqual(0, counter.Unmounted);
        }

        [TestMethod]
        public void ReactiveForBlock_ExplicitKeys_SingleAdd_MountsOnlyTheNewBlock()
        {
            var source = new ReactiveList<ReactiveKeyedItem>
            {
                new(new(1), new("one")),
                new(new(2), new("two")),
            };
            var counter = new BlockLifecycleCounter();
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<ReactiveKeyedItem, TextBlock, int>(
                new ReactiveScope(),
                source,
                (item, _) => item.Id.Value,
                itemRef => CreateCountingTextBlock(itemRef, item => item.Text.Value, counter)));

            var first = target.ToArray();

            source.Add(new(new(3), new("three")));
            TickReactive();

            Assert.AreSame(first[0], target[0]);
            Assert.AreSame(first[1], target[1]);
            ForBlockTestSupport.AssertText(target, "one", "two", "three");
            Assert.AreEqual(3, counter.Created);
            Assert.AreEqual(3, counter.Mounted);
            Assert.AreEqual(0, counter.Unmounted);
            Assert.AreEqual(0, counter.Disposed);
        }

        static StaticBlock<TextBlock> CreateKeyedTextBlock(Reference<ReactiveKeyedItem> itemRef)
        {
            return new StaticBlock<TextBlock>(
                new ReactiveScope(),
                (elements, scope) =>
                {
                    var box = new TextBlock();
                    elements.Add(box);
                    scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                        () => itemRef.Value.Text.Value,
                        value => box.Text = value));
                });
        }

        static CountingBlock<TextBlock> CreateCountingTextBlock<T>(
            Reference<T> itemRef,
            Func<T, string> text,
            BlockLifecycleCounter counter)
        {
            var scope = new ReactiveScope();
            var box = new TextBlock();
            scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
                () => itemRef.Value,
                value => box.Text = text(value)));
            return new CountingBlock<TextBlock>([box], scope, counter);
        }

        sealed record ReactiveKeyedItem(Reference<int> Id, Reference<string> Text);
    }

    sealed class BlockLifecycleCounter
    {
        public int Created;
        public int Mounted;
        public int Unmounted;
        public int Disposed;
    }

    sealed class CountingBlock<TElement> : IUIBlock<TElement>
    {
        readonly List<TElement> elements;
        readonly ReactiveScope scope;
        readonly BlockLifecycleCounter counter;
        UIBlockHost<TElement>? host;

        public CountingBlock(List<TElement> elements, ReactiveScope scope, BlockLifecycleCounter counter)
        {
            this.elements = elements;
            this.scope = scope;
            this.counter = counter;
            counter.Created++;
        }

        public int Count => elements.Count;

        public void Mount(UIBlockHost<TElement> host)
        {
            this.host = host;
            counter.Mounted++;
            for (var i = 0; i < elements.Count; i++)
                host.InsertElement(this, i, elements[i]);
        }

        public void Unmount()
        {
            if (host is null)
                return;

            counter.Unmounted++;
            for (var i = 0; i < elements.Count; i++)
                host.RemoveElement(this, 0);

            host = null;
        }

        public void Dispose()
        {
            counter.Disposed++;
            Unmount();
            scope.Dispose();
        }
    }
}
