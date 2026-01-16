namespace QuickMarkup.Infra;

interface IUICollection<T> : IList<T>
{
    void Move(uint oldIndex, uint newIndex);
}
