namespace QuickMarkup.Infra;

public sealed class ScopedValue<T>(T value, ReactiveScope scope) : IDisposable
{
    public T Value { get; } = value;
    public ReactiveScope Scope { get; } = scope;

    public void Dispose()
    {
        Scope.Dispose();
    }
}
