using System.Collections.ObjectModel;
using QuickMarkup.Infra;

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

public sealed class DependencyProperty;

public sealed class TestDependencyHoldButton : TestElement
{
    public static readonly DependencyProperty IsHoldingProperty = new();
    readonly List<Action<object?, object?>> propertyChangedCallbacks = [];
    bool isHolding;

    public bool IsHolding
    {
        get => isHolding;
        set
        {
            if (isHolding == value)
                return;

            isHolding = value;
            foreach (var callback in propertyChangedCallbacks)
                callback(this, EventArgs.Empty);
        }
    }

    public void RegisterPropertyChangedCallback(DependencyProperty property, Action<object?, object?> callback)
    {
        if (property == IsHoldingProperty)
            propertyChangedCallbacks.Add(callback);
    }
}

public sealed class TestComputedHoldButton : TestElement
{
    public Reference<bool> IsHoldingInputProp => field ??= new(false);
    public bool IsHoldingInput
    {
        get => IsHoldingInputProp.Value;
        set => IsHoldingInputProp.Value = value;
    }

    public Computed<bool> IsHoldingComp => field ??= new(() => IsHoldingInput);
    public bool IsHolding => IsHoldingComp.Value;
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

public sealed record EventItem(int Id, string Text, EventHandler Clicked);
