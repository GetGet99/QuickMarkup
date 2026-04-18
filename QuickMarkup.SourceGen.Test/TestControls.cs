using System.Collections.ObjectModel;

namespace QuickMarkup.SourceGen.Test;

public abstract class TestElement
{
    public string? Name { get; set; }
}

public class TestRoot
{
    public TestElementCollection Children { get; } = [];
}

public sealed class TestPanel : TestElement
{
    public TestElementCollection Children { get; } = [];
}

public sealed class TestButton : TestElement
{
    public TestElement? Content { get; set; }
    public event EventHandler? Clicked;

    public void RaiseClicked()
    {
        Clicked?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class TestText : TestElement
{
    public string? Text { get; set; }
    public int Number { get; set; }
    public bool Flag { get; set; }
}

public sealed class ItemsOnlyElement : TestElement
{
    public TestElementCollection Items { get; } = [];
}

public sealed class ChildOnlyElement : TestElement
{
    public TestElement? Child { get; set; }
}

public sealed class ContentOnlyElement : TestElement
{
    public TestElement? Content { get; set; }
}

public sealed class AmbiguousElement : TestElement
{
    public TestElementCollection Children { get; } = [];
    public TestElement? Content { get; set; }
}

public sealed class TestElementCollection : Collection<TestElement>
{
    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex)
            return;

        var item = this[oldIndex];
        RemoveAt(oldIndex);
        Insert(newIndex, item);
    }
}

public sealed record TestItem(int Id, string Text);

public sealed record EventItem(string Text, EventHandler Clicked);
