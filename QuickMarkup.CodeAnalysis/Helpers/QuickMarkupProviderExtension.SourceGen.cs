using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickMarkup.AST;
using System.Collections.Immutable;

namespace QuickMarkup.CodeAnalysis.Helpers;


static partial class QuickMarkupProviderExtension
{
    public static IncrementalValuesProvider<QuickMarkupAttributeInString> ForAllQuickMarkupAttributeInString(this SyntaxValueProvider syntaxValueProvider)
    {
        var temp = syntaxValueProvider.ForAttributeWithMetadataName(
            FullQuickMarkupAttributeName,
            static (syntaxNode, cancelationToken)
                => syntaxNode is TypeDeclarationSyntax,
            static (ctx, ct) =>
            {
                var type = (ITypeSymbol)ctx.TargetSymbol;
                var syn = ctx.Attributes[0].ApplicationSyntaxReference;
                
                return new QuickMarkupAttributeInString(
                    Target: QuickMarkupTargetContext.FromSyntaxAndSymbol(type, syn, ct),
                    MarkupString: (ctx.Attributes[0].ConstructorArguments[0].Value as string)!
                );
            }
        );
        return temp.Where(static x => x.MarkupString is not null);
    }
    public static IncrementalValuesProvider<QuickMarkupAttributeInString> ForAllQuickRefsAttributeInString(this SyntaxValueProvider syntaxValueProvider)
    {
        var temp = syntaxValueProvider.ForAttributeWithMetadataName(
            FullQuickRefsAttributeName,
            static (syntaxNode, cancelationToken)
                => syntaxNode is TypeDeclarationSyntax,
            static (ctx, ct) =>
            {
                var type = (ITypeSymbol)ctx.TargetSymbol;
                var list = new List<QuickMarkupAttributeInString>(ctx.Attributes.Length);
                foreach (var attr in ctx.Attributes)
                {
                    if (attr.ConstructorArguments.Length is 0) continue;
                    if (attr.ConstructorArguments[0].Value is not string markupString) continue;
                    list.Add(new QuickMarkupAttributeInString(
                        Target: QuickMarkupTargetContext.FromSyntaxAndSymbol(type, attr.ApplicationSyntaxReference, ct),
                        MarkupString: markupString
                    ));
                }
                return ImmutableArray.CreateRange(list);
            }
        );
        return temp.SelectMany(static (x, _) => x);
    }
    /// <summary>
    /// Gets all parsed QuickMarkup attributes, includes both scucessful and failed items during parsing stage
    /// </summary>
    /// <param name="syntaxValueProvider"></param>
    /// <returns></returns>
    public static QuickMarkupAllParsedResult ForAllParsedQuickMarkup(this SyntaxValueProvider syntaxValueProvider)
    {
        var parsed = syntaxValueProvider.ForAllQuickMarkupAttributeInString().TryParse();
        return new(parsed.GetAllSuccessfulParse(), parsed.GetAllFailedParse());
    }
    /// <summary>
    /// Gets all parsed QuickMarkup attributes, includes both scucessful and failed items during parsing stage
    /// </summary>
    /// <param name="syntaxValueProvider"></param>
    /// <returns></returns>
    public static IncrementalValuesProvider<QuickMarkupParsedAttribute> ForAllQuickMarkupSuccessfulParse(this SyntaxValueProvider syntaxValueProvider)
        => syntaxValueProvider.ForAllQuickMarkupAttributeInString().TryParse().GetAllSuccessfulParse();
    public static QuickMarkupParsedAttributeResult TryParse(this QuickMarkupAttributeInString stringAttribute)
    {
        QuickMarkupSFC? markup = null;
        string? error = null;
        try
        {
            markup = Parse(stringAttribute.MarkupString);
        }
        catch (Exception e)
        {
            error = $"""
                        Exception Occured during Parsing: {e.GetType().FullName} {e.Message}
                        Messsage: {e.Message}
                        Stack Trace:
                            {e.StackTrace.IndentWOF(1)}
                        """;
        }
        return new(stringAttribute.Target, markup, error);
    }
    public static IncrementalValuesProvider<QuickMarkupParsedAttributeResult> TryParse(this IncrementalValuesProvider<QuickMarkupAttributeInString> stringAttributes)
        => stringAttributes.Select(static (x, _) => x.TryParse());
    public static (IncrementalValuesProvider<QuickMarkupParsedAttribute> Successful, IncrementalValuesProvider<QuickMarkupParseError> Errors) ForAllParsedQuickRefs(this SyntaxValueProvider syntaxValueProvider)
    {
        var parsed = syntaxValueProvider.ForAllQuickRefsAttributeInString().TryParse();
        return new(parsed.GetAllSuccessfulParse(), parsed.GetAllFailedParse());
    }
    public static IncrementalValuesProvider<QuickMarkupMergedType> MergeMarkupAndRefs(
        this IncrementalValuesProvider<QuickMarkupParsedAttribute> markups,
        IncrementalValuesProvider<QuickMarkupParsedAttribute> refs)
    {
        return markups.Collect().Combine(refs.Collect()).SelectMany(static (x, _) =>
        {
            var (markupList, refsList) = x;
            var groups = new Dictionary<string, (QuickMarkupTargetContext Target, QuickMarkupSFC? Markup, List<QuickMarkupSFC> Refs)>();
            foreach (var m in markupList)
            {
                if (!groups.TryGetValue(m.Target.FullTypeName, out var g))
                {
                    g = (m.Target, null, new());
                    groups[m.Target.FullTypeName] = g;
                }
                g = (g.Target, m.AST, g.Refs);
                groups[m.Target.FullTypeName] = g;
            }
            foreach (var r in refsList)
            {
                if (!groups.TryGetValue(r.Target.FullTypeName, out var g))
                {
                    g = (r.Target, null, new());
                    groups[r.Target.FullTypeName] = g;
                }
                g.Refs.Add(r.AST);
                groups[r.Target.FullTypeName] = g;
            }
            var result = new List<QuickMarkupMergedType>(groups.Count);
            foreach (var g in groups.Values)
                result.Add(new QuickMarkupMergedType(g.Target, g.Markup, ImmutableArray.CreateRange(g.Refs)));
            return ImmutableArray.CreateRange(result);
        });
    }
    public static IncrementalValuesProvider<QuickMarkupParsedAttribute> GetAllSuccessfulParse(this IncrementalValuesProvider<QuickMarkupParsedAttributeResult> parsedAttributes)
        => parsedAttributes.Where(static x => x.Result is not null).Select(static (x, _) =>
        {
            return new QuickMarkupParsedAttribute(x.Target, x.Result!);
        });
    public static IncrementalValuesProvider<QuickMarkupParseError> GetAllFailedParse(this IncrementalValuesProvider<QuickMarkupParsedAttributeResult> parsedAttributes)
        => parsedAttributes.Where(static x => x.Error is not null).Select(static (x, _) =>
        {
            return new QuickMarkupParseError(x.Target, x.Error!);
        });
    public static void AddSource(this SourceProductionContext sourceProductionContext, QuickMarkupTargetContext target, string hintNameSuffix, string code, string usings = "", string typeModifiers = "partial")
    {
        sourceProductionContext.AddSource($"{target.TypeNameSourceGenOutputFriendlyFileName}.{hintNameSuffix}.g.cs", $$"""
            {{usings}}
            #nullable enable
            namespace {{target.Namespace}};
            
            {{typeModifiers}} class {{target.TypeName}} {
                {{code.IndentWOF()}}
            }
            """);
    }
    public static void AddSource(this SourceProductionContext sourceProductionContext, QuickMarkupTargetContext target, string hintNameSuffix, string code, string usings, string typeModifiers, string? baseTypes)
    {
        var baseClause = string.IsNullOrEmpty(baseTypes) ? "" : $" : {baseTypes}";
        sourceProductionContext.AddSource($"{target.TypeNameSourceGenOutputFriendlyFileName}.{hintNameSuffix}.g.cs", $$"""
            {{usings}}
            #nullable enable
            namespace {{target.Namespace}};
            
            {{typeModifiers}} class {{target.TypeName}}{{baseClause}} {
                {{code.IndentWOF()}}
            }
            """);
    }
}

