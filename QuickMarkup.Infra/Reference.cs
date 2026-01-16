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
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                var oldValue = _value;
                _value = value;
                ValueChanged?.Invoke(oldValue, value);
                ValueChangedBase?.Invoke();
            }
        }
    }
    internal event Action<T, T>? ValueChanged;
    event Action? ValueChangedBase;
    event Action IReference.ValueChanged
    {
        add => ValueChangedBase += value;
        remove => ValueChangedBase -= value;
    }
    public void Watch(Action<T> action, bool immediete = false)
    {
        var effect = new RefEffect(_ => action(Value));
        effect.AddDependency(this);
        if (immediete)
            effect.Tick();
    }
}
public class Computed<T> : IReference
{
    RefEffect effect;

    internal event Action<T, T>? ValueChanged;
    event Action? ValueChangedBase;
    event Action IReference.ValueChanged
    {
        add => ValueChangedBase += value;
        remove => ValueChangedBase -= value;
    }

    public Computed(Func<T> computed)
    {
        effect = ReferenceTracker.RunAndRerunOnReferenceChange(computed, x =>
        {
            var oldVal = _Value;
            _Value = x;
            ValueChanged?.Invoke(oldVal ?? x, x);
            ValueChangedBase?.Invoke();
        });
    }
    T _Value = default!;
    public T Value
    {
        get
        {
            ReferenceTracker.NotifyRefernceRead(this);
            return _Value;
        }
    }
    ~Computed()
    {
        effect.Dispose();
    }
    public void Watch(Action<T> action, bool immediete = false)
    {
        var effect = new RefEffect(_ => action(Value));
        effect.AddDependency(this);
        if (immediete)
            effect.Tick();
    }
}
