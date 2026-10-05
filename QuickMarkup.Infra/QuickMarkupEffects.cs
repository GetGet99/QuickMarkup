namespace QuickMarkup.Infra;

public static class QuickMarkupEffects
{
    public static void TrackEffects(List<IDisposable> disposables, IQuickMarkupEffectsDisposable child)
    {
        disposables.Add(new DisposableAction(child.DisposeEffects));
    }

    public static void TrackEffects(ReactiveScope scope, IQuickMarkupEffectsDisposable child)
    {
        scope.Add(new DisposableAction(child.DisposeEffects));
    }
}
