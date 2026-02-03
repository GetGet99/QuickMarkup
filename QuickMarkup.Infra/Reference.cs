namespace QuickMarkup.Infra;

public class Reference<T>(T defaultValue, string name = "") : IReference
{
    T _value = defaultValue;
    public T Value
    {
        get
        {
            ReferenceTracker.NotifyRefernceRead(this);
            DebugPrintCalleeRead();
            return _value;
        }
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                DebugPrintCalleeWrite();
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
    private void DebugPrintCalleeRead()
    {
        //System.Diagnostics.Debug.WriteLine($"Reference Read: {name}");
    }
    private void DebugPrintCalleeWrite()
    {
        //System.Diagnostics.Debug.WriteLine($"Reference Write: {name}");
    }
}
