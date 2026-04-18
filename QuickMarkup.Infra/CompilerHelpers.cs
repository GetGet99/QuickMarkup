namespace QuickMarkup.Infra;

public static class CompilerHelpers
{
    /// <summary>
    /// Typed helper that helps with running the item callback
    /// </summary>
    /// <typeparam name="T">Explicit if type is avaliable or can be left to be inferred</typeparam>
    /// <param name="item">The item to call the user defined action with</param>
    /// <param name="action"></param>
    public static void Closure<T>(T item, Action<T> action)
    {
        action(item);
    }

    public static TResult ClosureValue<T, TResult>(T item, Func<T, TResult> func)
    {
        return func(item);
    }

    public static TResult ClosureValue<T1, T2, TResult>(T1 item1, T2 item2, Func<T1, T2, TResult> func)
    {
        return func(item1, item2);
    }
}
