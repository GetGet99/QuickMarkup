namespace QuickMarkup.Infra;

public interface IUIBlock<TElement> : IDisposable
{
    int Count { get; }
    void Mount(UIBlockHost<TElement> host);
    void Unmount();
}
