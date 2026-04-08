using Get.EasyCSharp.GeneratorTools;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

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


class QuickMarkupSourceCodeLocationProvider
{
    Location fallback;
    SyntaxTree? syntaxTree = null;
    TextLineCollection textLines = null!;
    int startLine = 0;
    int startIndent = 0;
    bool ok;
    public QuickMarkupSourceCodeLocationProvider(AttributeData attribute, ITypeSymbol typeSym, CancellationToken ct)
    {
        var syn = attribute.ApplicationSyntaxReference;
        syntaxTree = syn?.SyntaxTree;
        fallback = syn is null ? typeSym.Locations[0] : Location.Create(syn.SyntaxTree, syn.Span);
        if (syn is null) return;
        if (syntaxTree is null) return;
        if (syn.GetSyntax(ct) is not AttributeSyntax attrSyntax) return;
        // move fallback to just the attribute name
        fallback = Location.Create(syn.SyntaxTree, attrSyntax.Name.Span);
        // TO USE
        if (attrSyntax.ArgumentList?.Arguments[0].Expression is not LiteralExpressionSyntax strLitSyntax) return;
        var strLitSpan = strLitSyntax.Span;
        var text = syn!.SyntaxTree.GetText(ct);
        textLines = text.Lines;
        var lpspan = text.Lines.GetLinePositionSpan(strLitSpan);
        startLine = lpspan.Start.Line;
        var endLine = lpspan.End.Line;
        if (startLine == endLine)
        {
            // let's just not deal with """ single line """
            return;
        }
        // skip line with starting """
        startLine++;
        var startLineSpan = text.Lines[startLine].Span;
        // skip empty lines, they don't count towards string literal
        for (int i = startLineSpan.Start; i < startLineSpan.End; i++)
        {
            if (!char.IsWhiteSpace(text[i])) goto skipIncrement;
        }
        // skip first empty line
        startLine++;
    skipIncrement:
        // end line consists of whitespaces and """ charcater and whatever after it
        var endLineSpan = text.Lines[endLine].Span;
        // get the index of first " as the indent start
        int indent = 0;
        while (indent < endLineSpan.End - endLineSpan.Start && text[endLineSpan.Start + indent] is ' ' or '\t')
        {
            indent++;
        }
        startIndent = indent;
        ok = true;
    }
    public Location Fallback => fallback;
    public Location GetLocation(Position start, Position end)
    {
        if (!ok)
            return fallback;
        var startPos = textLines.GetPosition(new LinePosition(startLine + start.Line, startIndent + start.Char));
        var endPos = textLines.GetPosition(new LinePosition(startLine + end.Line, startIndent + end.Char + 1));
        return Location.Create(syntaxTree!, new TextSpan(startPos, endPos - startPos));
    }
    public Location GetLocation(AST.AST? ast)
        => ast is null ? Fallback : GetLocation(ast.Start, ast.End);
}