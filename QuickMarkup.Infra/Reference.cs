namespace QuickMarkup.Infra;

public class Reference<T>(T defaultValue) : IReference
{
    T _value = defaultValue;
    public T Value
    {
        get
        {
            ReferenceTracker.NotifyRefernceRead(this);
            return _value;
        }
        set
        {
            var oldValue = _value;
            _value = value;
            ValueChanged?.Invoke(oldValue, value);
            ValueChangedBase?.Invoke();
        }
    }
    internal event Action<T, T>? ValueChanged;
    event Action? ValueChangedBase;
    event Action IReference.ValueChanged
    {
        add => ValueChangedBase += value;
        remove => ValueChangedBase -= value;
    }

    public void Watch(Action<T, T> action, bool immediete = false)
    {
        ValueChanged += action;
        if (immediete)
            action(Value, Value);
    }
    public void Watch(Action<T> action, bool immediete = false)
    {
        ValueChanged += (x, _) => action(x);
        if (immediete)
            action(Value);
    }
}
