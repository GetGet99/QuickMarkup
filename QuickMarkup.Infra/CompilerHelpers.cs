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
}