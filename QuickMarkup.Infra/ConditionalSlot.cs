namespace QuickMarkup.Infra;

public sealed class ConditionalSlot<T> : IDisposable
{
    readonly ReactiveScope controllerScope;
    readonly Func<bool> condition;
    readonly Action<T> setValue;
    readonly Func<ScopedValue<T>> trueFactory;
    readonly Func<ScopedValue<T>> falseFactory;
    ScopedValue<T>? current;
    RefEffect? conditionEffect;
    bool? currentConditionValue;
    bool disposed;

    public ConditionalSlot(
        ReactiveScope controllerScope,
        Func<bool> condition,
        Action<T> setValue,
        Func<ScopedValue<T>> trueFactory,
        Func<ScopedValue<T>> falseFactory)
    {
        this.controllerScope = controllerScope;
        this.condition = condition;
        this.setValue = setValue;
        this.trueFactory = trueFactory;
        this.falseFactory = falseFactory;

        conditionEffect = ReferenceTracker.RunAndRerunOnReferenceChange(
            condition,
            SwitchBranch);
        controllerScope.Add(conditionEffect);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        conditionEffect?.Dispose();
        conditionEffect = null;
        current?.Dispose();
        current = null;
        controllerScope.Dispose();
    }

    void SwitchBranch(bool conditionValue)
    {
        if (disposed || currentConditionValue == conditionValue)
            return;

        var old = current;

        using (ReferenceTracker.EnterStructuralScope(controllerScope))
        {
            var next = conditionValue ? trueFactory() : falseFactory();

            currentConditionValue = conditionValue;
            current = next;
            setValue(next.Value);
            old?.Dispose();
        }
    }
}
