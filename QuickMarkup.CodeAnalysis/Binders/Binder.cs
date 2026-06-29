using QuickMarkup.AST;

namespace QuickMarkup.CodeAnalysis.Binders;
using AST = AST.AST;

class Binder(Action<QMBinderError> onError)
{
    public static Action<QMBinderError> FailFast => d => throw new InvalidOperationException(d.ToString());
    public static Action<QMBinderError> Collect => _ => { };

    public List<QMDiagnostic> Diagnostics { get; } = [];
    protected void Error(QMBinderError error)
    {
        Diagnostics.Add(error);
        onError(error);
    }
    protected void Warn(QMBinderWarning warning)
    {
        Diagnostics.Add(warning);
    }
    protected void Error(AST ast, string message)
        => Error(new(ast, message));
    protected void Warn(AST ast, string message)
        => Warn(new(ast, message));
}
