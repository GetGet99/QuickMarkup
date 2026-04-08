namespace System.Diagnostics;

public sealed class UnreachableException(string? message, Exception? innerException) : Exception(message ?? "A part that was thought to be unreachable was reached.", innerException)
{
    public UnreachableException() : this(null) { }
    public UnreachableException(string? message) : this(message, null) { }
}
