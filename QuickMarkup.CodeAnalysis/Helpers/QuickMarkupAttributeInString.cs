using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using System.Collections.Immutable;

namespace QuickMarkup.CodeAnalysis.Helpers;

readonly record struct QuickMarkupAttributeInString(
    QuickMarkupTargetContext Target,
    string MarkupString
);

readonly record struct QuickMarkupParsedAttributeResult(
    QuickMarkupTargetContext Target,
    QuickMarkupSFC? Result,
    string? Error
);

readonly record struct QuickMarkupParsedAttribute(
    QuickMarkupTargetContext Target,
    QuickMarkupSFC AST
);
readonly record struct QuickMarkupParseError(
    QuickMarkupTargetContext Target,
    string Error
);

record struct QuickMarkupAllParsedResult(IncrementalValuesProvider<QuickMarkupParsedAttribute> Successful, IncrementalValuesProvider<QuickMarkupParseError> Errors);

public readonly record struct QuickMarkupMergedType(
    QuickMarkupTargetContext Target,
    QuickMarkupSFC? MarkupSource,
    ImmutableArray<QuickMarkupSFC> RefsSources
)
{
    public bool HasQuickMarkup => MarkupSource is not null;
    public string MergedUsings
    {
        get
        {
            var lines = new List<string>();
            var seen = new HashSet<string>();
            foreach (var s in AllSources().SelectMany(static x => x.Usings.Split('\n')))
            {
                var line = s.Trim();
                if (line.Length is 0 || !seen.Add(line)) continue;
                lines.Add(line);
            }
            return string.Join("\n", lines);
        }
    }
    IEnumerable<QuickMarkupSFC> AllSources()
    {
        if (MarkupSource is not null)
            yield return MarkupSource;
        foreach (var sfc in RefsSources)
            yield return sfc;
    }
    public List<RefDeclaration> MergedRefs()
    {
        var list = new List<RefDeclaration>();
        if (MarkupSource is not null)
            list.AddRange(MarkupSource.Refs);
        foreach (var sfc in RefsSources)
            list.AddRange(sfc.Refs);
        return list;
    }
    public QuickMarkupParsedTag? MergedTemplate => MarkupSource?.Template;
}