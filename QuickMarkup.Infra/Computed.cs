using System.Xml.Linq;

namespace QuickMarkup.Infra;

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

    string name;
    public Computed(Func<T> computed, string name = "")
    {
        this.name = name;
        effect = ReferenceTracker.RunAndRerunOnReferenceChange(computed, x =>
        {
            var oldVal = _Value;
            _Value = x;
            ValueChanged?.Invoke(oldVal ?? x, x);
            ValueChangedBase?.Invoke();
        });
    }

    public Computed(Func<Func<T>> computedFunc, string name = "") : this(computedFunc(), name)
    {
        
    }
    T _Value = default!;
    public T Value
    {
        get
        {
            DebugPrintCalleeRead();
            // if dependencies changed, we need to get already updated value
            // so we do them early
            ReactiveScheduler.DoNowIfScheduled(effect);
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
    public void Invalidate()
    {
        ReactiveScheduler.ScheduleEffect(effect);
    }
    private void DebugPrintCalleeRead()
    {
        //System.Diagnostics.Debug.WriteLine($"Computed Read: {name}");
    }
}
