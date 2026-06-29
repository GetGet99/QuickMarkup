namespace QuickMarkup.Infra;

public interface IReference
{
    event Action ValueChanged;
}

public interface IReference<T> : IReference
{
    T Value { get; }
}