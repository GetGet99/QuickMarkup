namespace QuickMarkup.Infra;

public static class ReactiveHelpers
{
    public static T BuildCallback<T>(T callback) where T : Delegate
        => callback;
    public static T BuildCallback<T>(Func<T> callback) where T : Delegate
        => callback();
    public static void Closure<T>(T item, Action<T> action)
    {
        action(item);
    }
}
