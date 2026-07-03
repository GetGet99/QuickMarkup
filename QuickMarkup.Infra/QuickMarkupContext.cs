namespace QuickMarkup.Infra;

public class QuickMarkupContext
{
    private readonly QuickMarkupContext? _parent;
    private readonly Dictionary<(Type, string), object> _locals = new();

    public QuickMarkupContext() : this(null) { }

    public QuickMarkupContext(QuickMarkupContext? parent)
    {
        _parent = parent;
    }

    public void Provide<T>(string name, Reference<T> reference)
    {
        _locals[(typeof(T), name)] = reference;
    }

    public Reference<T> Inject<T>(string name)
    {
        return TryInject<T>(name) ?? throw new InvalidOperationException(
            $"No provider found for '{typeof(T).Name} {name}'.");
    }

    public Reference<T>? TryInject<T>(string name)
    {
        if (_locals.TryGetValue((typeof(T), name), out var value))
            return (Reference<T>)value;
        return _parent?.TryInject<T>(name);
    }
}
