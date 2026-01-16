namespace QuickMarkup.Infra;

public static class QuickRefs
{
    public static Reference<T> Ref<T>(T initialValue)
    {
        return new(initialValue);
    }
    public static Computed<T> Computed<T>(Func<T> initialValue)
    {
        return new(initialValue);
    }
    public static RefEffect Effect(Action callback, params IReference[] references)
    {
        var effect = new RefEffect(_ => callback());
        foreach (var @ref in references)
            effect.AddDependency(@ref);
        ReactiveScheduler.ScheduleEffect(effect);
        return effect;
    }
}