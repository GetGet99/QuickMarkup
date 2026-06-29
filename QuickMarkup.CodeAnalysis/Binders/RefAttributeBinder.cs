using QuickMarkup.AST;

namespace QuickMarkup.CodeAnalysis.Binders;

abstract class RefAttributeBinder<T>(CodeTypeResolver resolver, Action<QMBinderError> onError, string? attributeName = null) : Binder(onError)
{
    protected readonly QuickMarkupBinderUtilities utils = new(resolver);
    protected abstract T Bind(RefDeclaration reference, QMAttribute attribute);
    public IEnumerable<T> Bind(IEnumerable<RefDeclaration> declarations)
    {
        foreach (var declaration in declarations)
        {
            foreach (var attribute in declaration.Attributes)
                if (attributeName is null || attribute.AttributeName.Name == attributeName)
                    yield return Bind(declaration, attribute);
        }
    }
}
