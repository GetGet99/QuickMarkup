namespace QuickMarkup.SourceGen.Test;

static class TestTreeAssert
{
    public static T Child<T>(IList<TestElement> children, int index)
        where T : TestElement
    {
        Assert.IsInstanceOfType<T>(children[index]);
        return (T)children[index];
    }

    public static T Content<T>(TestButton button)
        where T : TestElement
    {
        Assert.IsNotNull(button.Content);
        Assert.IsInstanceOfType<T>(button.Content);
        return (T)button.Content;
    }

    public static void Texts(IList<TestElement> children, params string?[] expected)
    {
        Assert.HasCount(expected.Length, children);
        for (var i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], Child<TestText>(children, i).Text);
    }
}
