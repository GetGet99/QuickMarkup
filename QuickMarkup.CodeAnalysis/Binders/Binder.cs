using QuickMarkup.AST;

namespace QuickMarkup.CodeAnalysis.Binders;
using AST = AST.AST;

class Binder(bool failFast = false)
{
    public List<QMDiagnostic> Diagnostics { get; } = [];
    protected void Error(QMBinderError error)
    {
        Diagnostics.Add(error);
        if (failFast)
            throw new InvalidOperationException(error.ToString());
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
