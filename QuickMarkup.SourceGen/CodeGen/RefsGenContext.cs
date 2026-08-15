using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.Language.Symbols;
using System.Text;

namespace QuickMarkup.SourceGen.CodeGen;

class RefsGenContext(StringBuilder membersBuilder, string nameHint)
{
    public void CGenWrite(IReadOnlyList<QMRefDeclarationSymbol<ITypeSymbol?>> refs, CancellationToken tok)
    {
        // Emit the Context property from IQuickMarkupContextAware
        membersBuilder.AppendLine("public global::QuickMarkup.Infra.QuickMarkupContext? Context { get; set; }");
        foreach (var @ref in refs)
        {
            CGenWrite(@ref);
            tok.ThrowIfCancellationRequested();
        }
    }

    public void CGenWrite(QMRefDeclarationSymbol<ITypeSymbol?> bound)
    {
        // Phase 1: compile-time attributes on the bound symbol are intentionally not emitted here.
        var typeName = bound.TypeName;
        var defaultValue = bound.DefaultValue is null
            ? "default"
            : ValueSymbolToInitExpression(bound.DefaultValue);
        if (bound.ShouldSuppressNullOnCreate)
        {
            defaultValue = $"({defaultValue})!";
        }
        var thisRef = bound.IsStatic ? "" : "this.";

        var accessibility = bound.Accessibility switch
        {
            ResolvedAccessibility.Public => "public",
            ResolvedAccessibility.Protected => "protected",
            ResolvedAccessibility.Private => "private",
            _ => throw new NotImplementedException()
        };
        if (bound.IsStatic)
            accessibility += " static";

        string backingType = bound.BackingTypeName;
        string backingName = bound.BackingName;

        string backingDefaultValue;
        backingDefaultValue = bound.Kind switch
            {
                RefDeclarationKind.Ref or RefDeclarationKind.Provide => $"""
                    => field ??= new {backingType}({defaultValue}, "{nameHint}.{bound.Name}")
                    """,
                RefDeclarationKind.Computed => $"""
                    => field ??= new {backingType}(() => {defaultValue}, "{nameHint}.{bound.Name}")
                    """,
                RefDeclarationKind.AsyncComputed => $"""
                    => field ??= new {backingType}(() => {defaultValue}, "{nameHint}.{bound.Name}")
                    """,
                RefDeclarationKind.Inject => "= null!",
                RefDeclarationKind.InjectOptional => "= null",
                _ => throw new NotImplementedException()
            };

        string backingDecl = $"{accessibility} {backingType} {backingName} {backingDefaultValue};";
        membersBuilder.AppendLine(backingDecl);

        if (bound.Kind is RefDeclarationKind.AsyncComputed)
        {
            string backing2 = $"{thisRef}{bound.BackingName}";

            string asyncPropertyHead = $"{accessibility} {typeName} {bound.Name}";
            membersBuilder.AppendLine($$"""
                {{asyncPropertyHead}} {
                    get => {{backing2}}.Value;
                }
                """);

            string statusPropertyHead = $"{accessibility} global::QuickMarkup.Infra.AsyncComputedState {bound.Name}Status";
            membersBuilder.AppendLine($$"""
                {{statusPropertyHead}} {
                    get => {{backing2}}.State;
                }
                """);

            string failurePropertyHead = $"{accessibility} global::System.Exception? {bound.Name}Failure";
            membersBuilder.AppendLine($$"""
                {{failurePropertyHead}} {
                    get => {{backing2}}.Failure;
                }
                """);

            return;
        }

        string propertyHead = $"{accessibility} {typeName} {bound.Name}";
        if (bound.IsRequired)
            propertyHead = $"""
                [global::QuickMarkup.SourceGen.QuickMarkupRequiredProperty]
                {propertyHead}
                """;

        string backing = $"{thisRef}{bound.BackingName}";
        string backingValue = $"{backing}.Value";

        string getter;
        if (bound.Kind is not RefDeclarationKind.InjectOptional)
        {
            getter = $"get => {backingValue};";
        } else
        {
            getter = $"get => {backing} is not null ? {backingValue} : default({typeName});";
        }

        string setter;
        if (bound.Kind is RefDeclarationKind.Computed)
        {
            setter = "// Computed variables do not emit setter";
        } else if (bound.Kind is RefDeclarationKind.InjectOptional)
        {
            setter = $$"""
                set {
                    if ({{backing}} is not null) {
                        {{backingValue}} = value;
                    }
                }
                """;
        }
        else
        {
            setter = $"set => {backingValue} = value;";
        }

        membersBuilder.AppendLine($$"""
            {{propertyHead}} {
                {{getter}}
                {{setter}}
            }
            """);
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
