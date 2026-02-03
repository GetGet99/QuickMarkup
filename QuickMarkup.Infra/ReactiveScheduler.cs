namespace QuickMarkup.Infra;

public class ReactiveScheduler
{
    private ReactiveScheduler() { }
    internal static ThreadLocal<ReactiveScheduler> Instance { get; } = new(() => new());

    internal static void DoNowIfScheduled(RefEffect effect)
        => Instance.Value!.DoNowIfScheduledPrivate(effect);

    /// <summary>
    /// Schedules work to be executed on this thread's next tick.
    /// </summary>
    public static void ScheduleEffect(RefEffect effect) => Instance.Value!.ScheduleEffectPrivate(effect);

    /// <summary>
    /// Executes all pending scheduled actions for this thread.
    /// Usually called at the end of the render loop or periodically.
    /// </summary>
    public static void Tick() => Instance.Value!.TickPrivate();

    public static void AddTickCallbackForCurrentThread(Action action)
    {
        Instance.Value!.ScheduleTickAction += action;
    }

    public static void RemoveTickCallbackForCurrentThread(Action action)
    {
        Instance.Value!.ScheduleTickAction -= action;
    }

    internal static void ResetForCurrentThread()
    {
        Instance.Value = new ReactiveScheduler();
    }
    internal bool ContinueOnException { get; set; } = false;
    internal bool AutoTick { get; set; } = true;
    private readonly HashSet<RefEffect> Effects = [];
    private HashSet<RefEffect> TickingEffects = [];
    private bool NeedsSchedulingTick = true;
    private event Action? ScheduleTickAction;
    private bool isTicking;
    private void ScheduleEffectPrivate(RefEffect effect)
    {
        if (Effects.Add(effect) && AutoTick && NeedsSchedulingTick)
        {
            NeedsSchedulingTick = false;
            ScheduleTickAction?.Invoke();
        }
    }
    private void DoNowIfScheduledPrivate(RefEffect effect)
    {
        if (isTicking && TickingEffects.Remove(effect))
        {
            goto tick;
        }
        else if (Effects.Remove(effect))
        {
            goto tick;
        }
        return;
    tick:
        if (isTicking)
        {
            effect.Tick();
        }
        else
        {
            isTicking = true;
            effect.Tick();
            isTicking = false;
        }
    }
    public void TickPrivate()
    {
        if (isTicking)
            return;
        isTicking = true;
        //System.Diagnostics.Debug.WriteLine("Tick");
        try
        {
            NeedsSchedulingTick = true;
            // clone
            TickingEffects = [.. Effects];
            Effects.Clear();
            while (TickingEffects.Count > 0)
            {
                var effect = TickingEffects.First();
                TickingEffects.Remove(effect);
                try
                {
                    effect.Tick();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    if (!ContinueOnException)
                        throw;
                }
            }
        }
        finally
        {
            isTicking = false;
        }
    }
}
