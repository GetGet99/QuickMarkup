using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis;

class QuickMarkupBinderUtilities(CodeTypeResolver resolver)
{
    public IQMValueSymbol Bind(QuickMarkupValue? value, ITypeSymbol? type, Action<QMBinderWarning>? onError = null)
    {
        switch (value)
        {
            case QuickMarkupInt32 x:
                return ValueOrAutoNew(resolver.Int32, x.Value.ToString(), type);
            case QuickMarkupDouble x:
                return ValueOrAutoNew(resolver.Double, x.Value.ToString(), type);
            case QuickMarkupBoolean x:
                return ValueOrAutoNew(resolver.Boolean, x.Value ? "true" : "false", type);
            case QuickMarkupString x:
                return ValueOrAutoNew(resolver.String, $"\"{SymbolDisplay.FormatLiteral(x.Value, false)}\"", type);
            case QuickMarkupDefault x:
                if (x.IsExplicitlyNull)
                {
                    if (type is null)
                        return Value(type, "null");
                    else
                        return Value(type, $"(({type.FullName()})null)");
                }
                if (type is null)
                {
                    // cannot resolve type, use "default" without type
                    return Value(type, "default");
                }
                return Value(type, $"default({type.FullName()})");
            case QuickMarkupForeign x:
                return Value(type, x.Code);
            case QuickMarkupIdentifier x:
                if (type is null)
                    throw new NotImplementedException($"Cannot infer type for the enum member {x.Identifier}");
                if (type.TypeKind == TypeKind.Enum && type.GetMembers().All(m => m.Name != x.Identifier))
                {
                    var candidates = type.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name);
                    var suggestions = StringSimilarity.GetSuggestions(x.Identifier, candidates);
                    onError?.Invoke(new QMBinderEnumMemberUnknownError(x, type.FullNameWithoutAnnotation(), x.Identifier, suggestions));
                }
                return Value(type, $"{type.FullName()}.{x.Identifier}");
            case QuickMarkupValueList:
            case QuickMarkupParsedTag:
                throw new ArgumentException("Parsed tag is not supported");
            case QuickMarkupRange x:
                return new QMRangeSymbol(x.RangeStart, x.RangeEnd);
            default:
                throw new NotImplementedException();
        }
        ;
    }
    static QMValueSymbol<ITypeSymbol> Value(ITypeSymbol? type, string ValueInFinalCode) => new(type, ValueInFinalCode);
    QMValueSymbol<ITypeSymbol> ValueOrAutoNew(ITypeSymbol? type, string ValueInFinalCode, ITypeSymbol? targetType)
    {
        if (targetType is null)
            return Value(type, ValueInFinalCode);
        if (resolver.ShouldAutoNew(type, targetType))
            // wrap in new(...)
            return Value(type, $"new {targetType.FullName()}({ValueInFinalCode})");
        else
            return Value(type, ValueInFinalCode);
    }
}
