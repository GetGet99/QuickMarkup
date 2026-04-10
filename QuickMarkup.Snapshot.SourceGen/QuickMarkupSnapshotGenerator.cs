using System.Text;
using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Snapshot.SourceGen.Binders;

namespace QuickMarkup.Snapshot.SourceGen;

[Generator]
partial class QuickMarkupSnapshotGenerator : IIncrementalGenerator
{
    protected void OnInitialize(IncrementalGeneratorPostInitializationContext context) { }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(OnInitialize);
        var nonErrorMarkups = context.SyntaxProvider.ForAllQuickMarkupSuccessfulParse();
        
        // REFS
        {
            var refs = nonErrorMarkups.Select(
                (x, _) =>
                {
                    return (x.Target, x.AST.Usings, x.AST.Refs);
                }
            );

            var withCompilation = refs.Combine(context.CompilationProvider);

            // remaining codes to be done
        }
    }
    public static string? GetExpandedLineText(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        if (!location.IsInSource)
            return null; // or throw, depending on your use case

        var sourceTree = location.SourceTree;
        var sourceText = sourceTree.GetText();

        var span = location.SourceSpan;

        // Get line numbers
        var startLine = sourceText.Lines.GetLineFromPosition(span.Start);
        var endLine = sourceText.Lines.GetLineFromPosition(span.End);

        // Expand to full lines (including line breaks)
        var expandedStart = startLine.Start;
        var expandedEnd = endLine.EndIncludingLineBreak;

        var expandedSpan = TextSpan.FromBounds(expandedStart, expandedEnd);

        return sourceText.ToString(expandedSpan);
    }

    static INamedTypeSymbol? TryResolveTypeMetadataName(Compilation compilation, string typeDisplayString)
    {
        var searchTypeName = typeDisplayString.StartsWith("global::", StringComparison.Ordinal)
            ? typeDisplayString["global::".Length..]
            : typeDisplayString;
        var idx = searchTypeName.IndexOf('<');
        if (idx >= 0)
            searchTypeName = searchTypeName[..idx];
        return compilation.GetTypeByMetadataName(searchTypeName);
    }
}
