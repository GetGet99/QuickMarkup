using QuickMarkup.Infra;

namespace QuickMarkup.Infra.Test.Blocks
{
    internal static class ForBlockTestSupport
    {
        public static void AssertText(List<TextBlock> boxes, params string[] expected)
        {
            var actual = boxes.Select(x => x.Text).ToArray();
            CollectionAssert.AreEqual(
                expected,
                actual,
                $"Expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}]");
        }

        public static StaticBlock<TextBlock> CreateTextBlock<T>(
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

        public static StaticBlock<TextBlock> CreateIndexedTextBlock(
            Reference<int> indexRef,
            Reference<string> itemRef)
        {
            return CreateIndexedTextBlock(indexRef, itemRef, item => item);
        }

        public static StaticBlock<TextBlock> CreateIndexedTextBlock<T>(
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
    }

    class TextBlock
    {
        public int InstanceId { get; set; }
        public string Text { get; set; } = "";
    }
}
