namespace QuickMarkup.Infra;

public sealed class ReactiveScope : IDisposable
{
    readonly List<RefEffect> effects = [];
    bool disposed;

    public void Add(RefEffect effect)
    {
        if (disposed)
        {
            effect.Dispose();
            return;
        }

        effects.Add(effect);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        foreach (var effect in effects)
            effect.Dispose();

        effects.Clear();
    }
}
