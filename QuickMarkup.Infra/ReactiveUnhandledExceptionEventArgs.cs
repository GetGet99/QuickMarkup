namespace QuickMarkup.Infra;

/// <summary>
/// Describes an exception thrown by a reactive callback or effect.
/// </summary>
public sealed class ReactiveUnhandledExceptionEventArgs(Exception exception) : EventArgs
{
    /// <summary>
    /// The exception thrown by the reactive callback or effect.
    /// </summary>
    public Exception Exception { get; } = exception;

    /// <summary>
    /// Set to true to swallow the exception and let the scheduler continue
    /// with the remaining work. When false, the scheduler rethrows.
    /// </summary>
    public bool Handled { get; set; }
}
