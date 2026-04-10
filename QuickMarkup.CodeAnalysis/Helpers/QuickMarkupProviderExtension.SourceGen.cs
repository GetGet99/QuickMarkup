using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickMarkup.AST;

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
    public static void AddSource(this SourceProductionContext sourceProductionContext, QuickMarkupTargetContext target, string hintNameSuffix, string code, string usings = "")
    {
        sourceProductionContext.AddSource($"{target.TypeNameSourceGenOutputFriendlyFileName}.{hintNameSuffix}.g.cs", $$"""
            {{usings}}
            #nullable enable
            namespace {{target.Namespace}};
            
            partial class {{target.TypeName}} {
                {{code.IndentWOF()}}
            }
            """);
    }
}

