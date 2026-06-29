namespace QuickMarkup.Infra;

public interface IQuickMarkupComponent<out T>
{
    T MarkupNode { get; }
}

public interface IQuickMarkupFragmentComponent<T>
{
    FragmentBlock<T> MarkupNode { get; }
}
