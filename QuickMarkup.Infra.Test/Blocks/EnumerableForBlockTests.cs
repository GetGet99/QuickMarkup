using QuickMarkup.Infra;
using QuickMarkup.Infra.Collections;

namespace QuickMarkup.Infra.Test.Blocks
{
    [TestClass]
    public sealed class EnumerableForBlockTests
    {
        [TestInitialize]
        public void Setup()
        {
            ReactiveScheduler.ResetForCurrentThread();
            ReactiveScheduler.Instance.Value!.AutoTick = false;
            ReactiveScheduler.Instance.Value!.ContinueOnException = false;
        }

        [TestMethod]
        public void EnumerableForBlock_RendersFromDirectSource()
        {
            IEnumerable<string> source = new[] { "alpha", "beta" };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<string, TextBlock>(
                new ReactiveScope(),
                source,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0)));

            ForBlockTestSupport.AssertText(target, "alpha", "beta");
        }

        [TestMethod]
        public void EnumerableForBlock_RendersFromGetter()
        {
            var all = new List<string> { "apple", "banana", "cherry" };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<string, TextBlock>(
                new ReactiveScope(),
                () => all.Where(x => x.StartsWith('a')),
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0)));

            ForBlockTestSupport.AssertText(target, "apple");
        }

        [TestMethod]
        public void EnumerableForBlock_RendersFromLinqOverReactiveList()
        {
            var source = new ReactiveList<string> { "a", "b", "c" };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<string, TextBlock>(
                new ReactiveScope(),
                () => source.Take(2),
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0)));

            var first = target.ToArray();

            ForBlockTestSupport.AssertText(target, "a", "b");

            source.Insert(0, "z");
            TickReactive();

            Assert.AreSame(first[0], target[1]);
            ForBlockTestSupport.AssertText(target, "z", "a");
        }

        [TestMethod]
        public void EnumerableForBlock_WithExplicitKeys()
        {
            IEnumerable<IdItem> source = new[] { new IdItem(1, "one"), new IdItem(2, "two") };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<IdItem, TextBlock, int>(
                new ReactiveScope(),
                source,
                item => item.Id,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, item => item.Text, () => 0)));

            ForBlockTestSupport.AssertText(target, "one", "two");
        }

        [TestMethod]
        public void EnumerableForBlock_WithIndexAwareFactory()
        {
            IEnumerable<string> source = new[] { "alpha", "beta" };
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<string, TextBlock>(
                new ReactiveScope(),
                source,
                (indexRef, itemRef) => ForBlockTestSupport.CreateIndexedTextBlock(indexRef, itemRef)));

            ForBlockTestSupport.AssertText(target, "1. alpha", "2. beta");
        }

        [TestMethod]
        public void EnumerableForBlock_RefreshRematerializes()
        {
            var all = new List<string> { "a", "b" };
            Func<IEnumerable<string>> getter = () => all.ToList();
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));
            var block = QuickMarkup.Infra.Blocks.ForBlock.Create<string, TextBlock>(
                new ReactiveScope(),
                getter,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0));

            host.AddBlock(block);

            ForBlockTestSupport.AssertText(target, "a", "b");

            all.Add("c");
            block.Refresh();
            TickReactive();

            ForBlockTestSupport.AssertText(target, "a", "b", "c");
        }

        [TestMethod]
        public void EnumerableForBlock_NullGetter_RendersNothing()
        {
            Func<IEnumerable<string>> getter = () => null!;
            List<TextBlock> target = [];
            UIBlockHost<TextBlock> host = new(new TargetUICollection<TextBlock>(target));

            host.AddBlock(QuickMarkup.Infra.Blocks.ForBlock.Create<string, TextBlock>(
                new ReactiveScope(),
                getter,
                itemRef => ForBlockTestSupport.CreateTextBlock(itemRef, value => value, () => 0)));

            Assert.IsEmpty(target);
        }

        static void TickReactive()
        {
            ReactiveScheduler.Tick();
            ReactiveScheduler.Tick();
        }

        sealed record IdItem(int Id, string Text);
    }
}
