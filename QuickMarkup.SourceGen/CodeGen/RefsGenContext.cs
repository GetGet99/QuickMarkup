using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Microsoft.CodeAnalysis;
using QuickMarkup.Language.Symbols;
using System.Text;

namespace QuickMarkup.SourceGen.CodeGen;

class RefsGenContext(StringBuilder membersBuilder, string nameHint)
{
    public void CGenWrite(IReadOnlyList<QMRefDeclarationSymbol<ITypeSymbol?>> refs, CancellationToken tok)
    {
        foreach (var @ref in refs)
        {
            CGenWrite(@ref);
            tok.ThrowIfCancellationRequested();
        }
    }

    public void CGenWrite(QMRefDeclarationSymbol<ITypeSymbol?> bound)
    {
        // Phase 1: compile-time attributes on the bound symbol are intentionally not emitted here.
        var typeName = RefTypeDisplayName(bound.RefType, bound.Name);
        var defaultValue = bound.DefaultValue is null
            ? "default"
            : ValueSymbolToInitExpression(bound.DefaultValue);
        var accessibility = bound.IsStatic
            ? (bound.IsPrivate ? "private static" : "public static")
            : (bound.IsPrivate ? "private" : "public");
        var thisRef = bound.IsStatic ? "" : "this.";
        if (bound.IsComputedDeclaration)
        {
            var computedType = $"global::QuickMarkup.Infra.Computed<{typeName}>";
            membersBuilder.AppendLine($$"""
                {{accessibility}} {{computedType}} {{bound.Name}}Comp => field ??= new {{computedType}}(() => {{defaultValue}}, "{{nameHint}}.{{bound.Name}}");
                {{accessibility}} {{typeName}} {{bound.Name}} {
                    get {
                        return {{thisRef}}{{bound.Name}}Comp.Value;
                    }
                }
                """);
        }
        else
        {
            var refType = $"global::QuickMarkup.Infra.Reference<{typeName}>";
            membersBuilder.AppendLine($$"""
                {{accessibility}} {{refType}} {{bound.Name}}Prop => field ??= new {{refType}}({{defaultValue}}, "{{nameHint}}.{{bound.Name}}");
                {{accessibility}} {{typeName}} {{bound.Name}} {
                    get {
                        return {{thisRef}}{{bound.Name}}Prop.Value;
                    }
                    set {
                        {{thisRef}}{{bound.Name}}Prop.Value = value;
                    }
                }
                """);
        }
    }

    static string RefTypeDisplayName(ITypeSymbol? type, string fallbackName)
    {
        if (type is null)
            return fallbackName;
        var s = new FullType(type).TypeWithNamespace;
        if (type is { IsValueType: true, NullableAnnotation: NullableAnnotation.Annotated }
            && !s.EndsWith("?", StringComparison.Ordinal))
            return s + "?";
        return s;
    }

    static string ValueSymbolToInitExpression(IQMValueSymbol sym) => sym switch
    {
        QMValueSymbol<ITypeSymbol?> v => v.ValueInFinalCode,
        QMRangeSymbol => throw new NotSupportedException("Range values are not supported as a ref default initializer."),
        QMNestedValuesSymbol<ITypeSymbol?> => throw new NotSupportedException("Nested markup is not supported as a ref default initializer."),
        QMNodeSymbol<ITypeSymbol?> => throw new NotSupportedException("Tag values are not supported as a ref default initializer."),
        QMForNodeSymbol<ITypeSymbol> => throw new NotSupportedException("For-loop values are not supported as a ref default initializer."),
        _ => throw new NotSupportedException($"Unsupported value symbol for ref codegen: {sym.GetType().Name}"),
    };
}
