namespace QuickMarkup.Infra;

public interface IQuickMarkupEffectsDisposable
{
    void DisposeEffects();
}

public interface IQuickMarkupComponent<out T> : IQuickMarkupEffectsDisposable
{
    T MarkupNode { get; }
}

public interface IQuickMarkupFragmentComponent<T> : IQuickMarkupEffectsDisposable
{
    FragmentBlock<T> MarkupNode { get; }
}
