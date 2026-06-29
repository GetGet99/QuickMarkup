using Get.EasyCSharp.GeneratorTools;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using QuickMarkup.SourceGen;

namespace QuickMarkup.CodeAnalysis.Helpers;

delegate void QuickMarkupAnalysisActionCallback(SyntaxNodeAnalysisContext context, QuickMarkupAttributeInString markup, QuickMarkupSourceCodeLocationProvider locationProvider);


static partial class QuickMarkupProviderExtension
{
    public static void RegisterQuickMarkupAttributeInStringSyntaxAction(this AnalysisContext analysisContext, QuickMarkupAnalysisActionCallback callback)
    {
        analysisContext.RegisterSyntaxNodeAction((context) =>
        {
            var syntaxNode = (TypeDeclarationSyntax)context.Node;
            // Filter out everything which has no attribute
            if (syntaxNode.AttributeLists.Count is 0) return;

            var compilation = context.Compilation;
            // Get Symbol
            if (context.SemanticModel.GetDeclaredSymbol(syntaxNode) is not ITypeSymbol typeSym)
                return;

            // Get Attributes
            var Class = compilation.GetTypeByMetadataName(FullQuickMarkupAttributeName);
            if (Class is null) return;

            var attribute = (
                from x in typeSym.GetAttributes()
                where x.AttributeClass?.IsSubclassFrom(Class) ?? false
                select x
            ).FirstOrDefault();
            if (attribute is null) return;
            if (attribute.ConstructorArguments[0].Value is not string markup) return;
            var locationProvider = new QuickMarkupSourceCodeLocationProvider(attribute, typeSym, context.CancellationToken);
            callback(context, new(
                QuickMarkupTargetContext.FromSyntaxAndSymbol(
                    typeSym,
                    attribute.ApplicationSyntaxReference,
                    context.CancellationToken
                ),
                markup
            ), locationProvider);
        }, syntaxKinds: SyntaxKind.ClassDeclaration);
    }
}


class QuickMarkupSourceCodeLocationProvider : IQuickMarkupLocationProvider
{
    readonly AttributeStringLocationMapper _mapper;
    Location _fallback;

    public QuickMarkupSourceCodeLocationProvider(AttributeData attribute, ITypeSymbol typeSym, CancellationToken ct)
    {
        _mapper = new AttributeStringLocationMapper(attribute, ct);

        var syn = attribute.ApplicationSyntaxReference;
        _fallback = syn is null ? typeSym.Locations[0] : Location.Create(syn.SyntaxTree, syn.Span);

        if (syn?.GetSyntax(ct) is AttributeSyntax attrSyntax)
            _fallback = Location.Create(syn.SyntaxTree, attrSyntax.Name.Span);
    }

    public Location Fallback => _fallback;

    public Location GetLocation(Position start, Position end)
    {
        if (!_mapper.IsValid)
            return _fallback;
        return _mapper.GetLocation(start, end);
    }

    public Location GetLocation(AST.AST? ast)
        => ast is null ? Fallback : GetLocation(ast.Start, ast.End);
}