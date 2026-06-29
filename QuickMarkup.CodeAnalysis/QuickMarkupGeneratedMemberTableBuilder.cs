using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis;

static class QuickMarkupGeneratedMemberTableBuilder
{
    public static QuickMarkupGeneratedTypeMembers? BuildTypeMembers(
        QuickMarkupParsedAttribute markup,
        Compilation compilation,
        CancellationToken ct = default)
    {
        var target = markup.Target;
        if (!target.TryGetTypeSymbol(compilation, out var typeSymbol, out _))
            return null;

        var resolver = new CodeTypeResolver(compilation, markup.AST.Usings, target.Namespace);
        var binder = new QuickMarkupBinder(resolver, Binder.FailFast);
        var refs = binder.BindRefDeclarations(markup.AST.Refs, typeSymbol);
        var properties = new Dictionary<string, QuickMarkupGeneratedPropertySymbol>();
        var unknownTypes = typeSymbol.TypeParameters.Length > 0;
        var componentKind = resolver.GetComponentKind(typeSymbol, out var componentOutputType);

        foreach (var @ref in refs)
        {
            AddGeneratedProperty(
                properties,
                new QuickMarkupGeneratedPropertySymbol(
                    @ref.Name,
                    unknownTypes ? null : TypeName(@ref.RefType),
                    @ref.IsPrivate,
                    @ref.IsComputedDeclaration
                        ? QuickMarkupGeneratedPropertyKind.ComputedValue
                        : QuickMarkupGeneratedPropertyKind.RefValue));

            var backingName = @ref.IsComputedDeclaration
                ? $"{@ref.Name}Comp"
                : $"{@ref.Name}Prop";
            var backingType = unknownTypes
                ? null
                : ConstructBackingTypeName(@ref.RefType, @ref.IsComputedDeclaration);

            AddGeneratedProperty(
                properties,
                new QuickMarkupGeneratedPropertySymbol(
                    backingName,
                    backingType,
                    @ref.IsPrivate,
                    @ref.IsComputedDeclaration
                        ? QuickMarkupGeneratedPropertyKind.ComputedBacking
                        : QuickMarkupGeneratedPropertyKind.RefBacking));

            ct.ThrowIfCancellationRequested();
        }

        if (componentKind is not QMComponentKind.None && HasComponentRootOutput(markup.AST.Template, componentKind))
        {
            var outputTypeName = unknownTypes
                ? null
                : componentKind is QMComponentKind.Fragment
                    ? $"global::QuickMarkup.Infra.FragmentBlock<{TypeName(componentOutputType) ?? "object"}>"
                    : TypeName(componentOutputType);
            AddGeneratedProperty(
                properties,
                new QuickMarkupGeneratedPropertySymbol(
                    CodeTypeResolver.ComponentOutputPropertyName,
                    outputTypeName,
                    false,
                    QuickMarkupGeneratedPropertyKind.ComponentOutput));
        }

        var initMode = typeSymbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared)
            ? QuickMarkupInitializationMode.BackwardCompatible
            : QuickMarkupInitializationMode.DeferredInit;

        var constructorMethod = typeSymbol.GetMembers().OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.GetAttributes().Any(a =>
                a.AttributeClass?.Name is "QuickMarkupConstructorAttribute"));

        List<QuickMarkupConstructorParameter>? ctorParams = null;
        if (constructorMethod is { Parameters.Length: > 0 })
        {
            ctorParams = [];
            foreach (var p in constructorMethod.Parameters)
            {
                ctorParams.Add(new QuickMarkupConstructorParameter(
                    p.Type.FullName() ?? "object",
                    p.Name));
            }
        }

        return new QuickMarkupGeneratedTypeMembers(
            target.FullTypeName, properties, initMode,
            constructorMethod?.Name, ctorParams);
    }

    static void AddGeneratedProperty(
        Dictionary<string, QuickMarkupGeneratedPropertySymbol> properties,
        QuickMarkupGeneratedPropertySymbol property)
    {
        if (!properties.ContainsKey(property.Name))
            properties.Add(property.Name, property);
    }

    static string? ConstructBackingTypeName(ITypeSymbol? valueType, bool isComputed)
    {
        var valueTypeName = TypeName(valueType);
        if (valueTypeName is null)
            return null;

        return isComputed
            ? $"global::QuickMarkup.Infra.Computed<{valueTypeName}>"
            : $"global::QuickMarkup.Infra.Reference<{valueTypeName}>";
    }

    static string? TypeName(ITypeSymbol? type)
        => type?.FullName();

    public static bool HasComponentRootOutput(QuickMarkupParsedTag? template, QMComponentKind componentKind = QMComponentKind.None)
    {
        if (template is null) return false;
        if (componentKind is not QMComponentKind.None && template.TagStart.TagName is not "root")
            return true;
        return template.Children?.Any(static child => child is not QuickMarkupParsedTag { TagStart: QuickMarkupPropertyTagStart }) ?? false;
    }
}
