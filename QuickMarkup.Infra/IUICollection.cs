namespace QuickMarkup.Infra;

public interface IUICollection<T> : IList<T>
{
    void Move(int oldIndex, int newIndex);
}
