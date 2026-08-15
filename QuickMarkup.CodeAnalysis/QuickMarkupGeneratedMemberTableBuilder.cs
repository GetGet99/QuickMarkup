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

        var hasRequired = false;
        foreach (var @ref in refs)
        {
            var isRequired = @ref.IsRequired;
            if (isRequired) hasRequired = true;

            var memberTableKind = @ref.Kind switch
            {
                RefDeclarationKind.Computed => QuickMarkupGeneratedPropertyKind.ComputedValue,
                RefDeclarationKind.AsyncComputed => QuickMarkupGeneratedPropertyKind.AsyncComputedValue,
                _ => QuickMarkupGeneratedPropertyKind.RefValue
            };

            AddGeneratedProperty(
                properties,
                new QuickMarkupGeneratedPropertySymbol(
                    @ref.Name,
                    unknownTypes ? null : @ref.TypeName,
                    @ref.Accessibility,
                    memberTableKind,
                    IsRequired: isRequired,
                    IsNullableAware: true
                ));

            var backingName = @ref.BackingName;
            
            AddGeneratedProperty(
                properties,
                new QuickMarkupGeneratedPropertySymbol(
                    backingName,
                    unknownTypes ? null : @ref.BackingTypeName,
                    @ref.Accessibility,
                    @ref.Kind switch
                    {
                        RefDeclarationKind.Ref => QuickMarkupGeneratedPropertyKind.RefBacking,
                        RefDeclarationKind.Computed => QuickMarkupGeneratedPropertyKind.ComputedBacking,
                        RefDeclarationKind.AsyncComputed => QuickMarkupGeneratedPropertyKind.AsyncComputedBacking,
                        RefDeclarationKind.Provide => QuickMarkupGeneratedPropertyKind.ProvideValue,
                        RefDeclarationKind.Inject or RefDeclarationKind.InjectOptional => QuickMarkupGeneratedPropertyKind.InjectValue,
                        _ => throw new NotImplementedException()
                    },
                    IsRequired: false,
                    IsNullableAware: true
                ));

            if (@ref.Kind is RefDeclarationKind.AsyncComputed)
            {
                AddGeneratedProperty(
                    properties,
                    new QuickMarkupGeneratedPropertySymbol(
                        $"{@ref.Name}Status",
                        unknownTypes ? null : "global::QuickMarkup.Infra.AsyncComputedState",
                        @ref.Accessibility,
                        QuickMarkupGeneratedPropertyKind.AsyncComputedStatus,
                    IsRequired: false,
                    IsNullableAware: true
                ));

                AddGeneratedProperty(
                    properties,
                    new QuickMarkupGeneratedPropertySymbol(
                        $"{@ref.Name}Failure",
                        unknownTypes ? null : "global::System.Exception?",
                        @ref.Accessibility,
                        QuickMarkupGeneratedPropertyKind.AsyncComputedFailure,
                    IsRequired: false,
                    IsNullableAware: true
                ));
            }

            ct.ThrowIfCancellationRequested();
        }

        if (componentKind is not QMComponentKind.None && HasComponentRootOutput(markup.AST.Template, componentKind))
        {
            var outputTypeName = unknownTypes
                ? null
                : componentKind is QMComponentKind.Fragment
                    ? $"global::QuickMarkup.Infra.FragmentBlock<{ReferenceExtension.RefTypeDisplayName(componentOutputType, "object")}>"
                    : ReferenceExtension.RefTypeDisplayName(componentOutputType, "object");
            AddGeneratedProperty(
                properties,
                new QuickMarkupGeneratedPropertySymbol(
                    CodeTypeResolver.ComponentOutputPropertyName,
                    outputTypeName,
                    ResolvedAccessibility.Public,
                    QuickMarkupGeneratedPropertyKind.ComponentOutput,
                    IsRequired: false,
                    IsNullableAware: true
                ));
        }

        var initMode = hasRequired
            ? QuickMarkupInitializationMode.DeferredInit
            : typeSymbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared && !x.GetAttributes().Any(a => a.AttributeClass?.Name == "QuickMarkupGeneratedConstructorAttribute"))
                ? QuickMarkupInitializationMode.BackwardCompatible
                : QuickMarkupInitializationMode.DeferredInit;

        var supportsContext = initMode is QuickMarkupInitializationMode.DeferredInit
            || typeSymbol.AllInterfaces.Any(i =>
                i.Name == "IQuickMarkupContextAware" &&
                i.ContainingNamespace?.ToDisplayString() == "QuickMarkup.Infra");

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
            supportsContext,
            constructorMethod?.Name, ctorParams);
    }

    static void AddGeneratedProperty(
        Dictionary<string, QuickMarkupGeneratedPropertySymbol> properties,
        QuickMarkupGeneratedPropertySymbol property)
    {
        if (!properties.ContainsKey(property.Name))
            properties.Add(property.Name, property);
    }

    public static bool HasComponentRootOutput(QuickMarkupParsedTag? template, QMComponentKind componentKind = QMComponentKind.None)
    {
        if (template is null) return false;
        if (componentKind is not QMComponentKind.None && template.TagStart.TagName is not "root")
            return true;
        return template.Children?.Any(static child => child is not QuickMarkupParsedTag { TagStart: QuickMarkupPropertyTagStart }) ?? false;
    }
}
