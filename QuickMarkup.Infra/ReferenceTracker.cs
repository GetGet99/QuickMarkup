namespace QuickMarkup.Infra;

public class ReferenceTracker
{
    private ReferenceTracker() { }
    internal static ThreadLocal<ReferenceTracker> Instance { get; } = new(() => new());
    internal event Action<IReference>? ReferenceRead;

    internal static void NotifyRefernceRead(IReference reference)
    {
        Instance.Value!.ReferenceRead?.Invoke(reference);
    }

    public static T NoCapture<T>(Func<T> action)
    {
        var current = Instance.Value!;
        var refRead = current.ReferenceRead;
        current.ReferenceRead -= refRead;
        var result = action();
        current.ReferenceRead += refRead;
        return result;
    }

    internal RefEffect? CurrentEffect;
    public static RefEffect RunAndRerunOnReferenceChange<T>(Func<T> func, Action<T> continueAction)
    {
        var tracker = Instance.Value!;
        RefEffect effect = new(Rerun);
        try
        {
            // run once
            effect.Tick();
        } catch (Exception e)
        {
            Console.WriteLine(e);
            if (!ReactiveScheduler.Instance.Value!.ContinueOnException)
                throw;
        }

        return effect;

        void Rerun(RefEffect effect)
        {
            // Remove old subscriptions
            effect.ResetDependency();

            // Collect new dependencies
            void OnRead(IReference r) => effect.AddDependency(r);
            tracker.ReferenceRead += OnRead;

            // Run the function
            tracker.CurrentEffect = effect;
            var result = func();
            tracker.CurrentEffect = null;

            tracker.ReferenceRead -= OnRead;

            continueAction(result);
        }
    }
}
