using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using QuickMarkup.Infra;

namespace QuickMarkup.SourceGen.Test.DeferredInit;

public abstract class TestElement
{
    public string? Name { get; set; }
    public bool ElementExtensionApplied { get; set; }
    protected readonly List<Action<object?, object?>> propertyChangedCallbacks = [];
    public void RegisterPropertyChangedCallback(DependencyProperty property, Action<object?, object?> callback)
        => propertyChangedCallbacks.Add(callback);
}

public class TestRoot
{
    public TestElementCollection Children { get; } = [];
}

public sealed class TestPanel : TestElement
{
    public TestElementCollection Children { get; } = [];
    public bool ExtensionApplied { get; set; }
    public bool CallbackApplied { get; set; }
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
    bool isHolding;

    public bool IsHolding
    {
        get => isHolding;
        set
        {
            if (isHolding == value)
                return;

            isHolding = value;
            propertyChangedCallbacks.ForEach(c => c(this, EventArgs.Empty));
        }
    }
}

public sealed class Grid
{
    public static readonly DependencyProperty RowProperty = new();
    static readonly ConcurrentDictionary<TestElement, int> attachedRowValues = [];
    public static void SetRow(TestElement element, int value)
        => attachedRowValues[element] = value;
    public static int GetRow(TestElement element)
        => attachedRowValues.TryGetValue(element, out var val) ? val : 0;
    public static readonly DependencyProperty ColumnProperty = new();
    static readonly ConcurrentDictionary<TestElement, int> attachedColumnValues = [];
    public static void SetColumn(TestElement element, int value)
        => attachedColumnValues[element] = value;
    public static int GetColumn(TestElement element)
        => attachedColumnValues.TryGetValue(element, out var val) ? val : 0;
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
    public NestedData Nested { get; set; } = new();
}

public sealed class NestedData
{
    public string? Text { get; set; }
}

public enum TestKind
{
    Default,
    Secondary
}

public sealed class NullableRefItem;

public readonly record struct TestRadius(int Value);

public sealed class AutoNewElement : TestElement
{
    public TestRadius Radius { get; set; }
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

public static class TestElementExtensions
{
    public static void MarkPanel(this TestPanel panel)
    {
        panel.ExtensionApplied = true;
    }

    public static void MarkElement(this TestElement element)
    {
        element.ElementExtensionApplied = true;
    }
}
