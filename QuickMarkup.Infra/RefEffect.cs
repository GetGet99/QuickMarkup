namespace QuickMarkup.Infra;

public class RefEffect(Action<RefEffect> callback)
{
    internal HashSet<IReference> Dependencies { get; } = [];
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
