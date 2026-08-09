namespace QuickMarkup.Infra;

public class RefEffect(Action<RefEffect> callback) : IDisposable
{
    static long EffectSequence;

    internal HashSet<IReference> Dependencies { get; } = [];

    /// <summary>
    /// The structural scope this effect belongs to, assigned when the effect is added to
    /// a scope. Null for effects without UI structural lifetime (e.g. Computed internals).
    /// </summary>
    internal ReactiveScope? Scope { get; set; }

    internal long Sequence { get; } = Interlocked.Increment(ref EffectSequence);

    public void AddDependency(IReference reference)
    {
        if (Dependencies.Add(reference))
        {
            reference.ValueChanged += Reference_ValueChanged;
        }
    }
    internal void ResetDependency()
    {
        foreach (var reference in Dependencies)
        {
            reference.ValueChanged -= Reference_ValueChanged;
        }
        Dependencies.Clear();
    }

    private void Reference_ValueChanged()
    {
        ReactiveScheduler.ScheduleEffect(this);
    }

    internal void Tick()
    {
        callback(this);
    }
    public void Dispose()
    {
        ResetDependency();
    }
}
