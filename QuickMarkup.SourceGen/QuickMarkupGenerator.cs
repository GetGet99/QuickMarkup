using Get.EasyCSharp.GeneratorTools;
using Get.Lexer;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickMarkup.AST;
using QuickMarkup.Parser;
using System.Text;

namespace QuickMarkup.SourceGen;

[Generator]
[AddAttributeConverter(typeof(QuickMarkupAttribute), ParametersAsString = "\"\"")]
partial class QuickMarkupGenerator : AttributeBaseGenerator<QuickMarkupAttribute, QuickMarkupGenerator.QuickMarkupAttributeWarpper, TypeDeclarationSyntax, INamedTypeSymbol>
{
    protected override string? OnPointVisit(OnPointVisitArguments args)
    {
        var markup = args.AttributeDatas[0].Wrapper.markup;
        var sfc = Parse(markup);
        args.CancellationToken.ThrowIfCancellationRequested();
        var ns = $"""
            {sfc.Usings}
            namespace {args.Symbol.ContainingNamespace}.QUICKMARKUP_TEMP_NAMESPACE;
            """;
        args.Usings.Add(sfc.Usings);
        StringBuilder generatedProperties = new();
        StringBuilder codeBuilder = new();
        generatedProperties.AppendLine("global::System.Collections.Generic.List<global::QuickMarkup.Infra.RefEffect> QUICKMARKUP_EFFECTS { get; } = [];");
        var isConstructorMode = !args.Symbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared);
        args.CancellationToken.ThrowIfCancellationRequested();
        var rgen = new RefsGenContext(
            new(args.GenContext.SemanticModel.Compilation, ns),
            generatedProperties,
            args.Symbol.Name
        );
        rgen.CGenWrite(sfc.Refs);
        args.CancellationToken.ThrowIfCancellationRequested();
        if (sfc.Template is not null)
        {
            var cgen = new CodeGenContext(
                new(args.GenContext.SemanticModel.Compilation, ns),
                generatedProperties,
                codeBuilder,
                isConstructorMode
            );
            cgen.CGenWrite(sfc.Template, new(args.Symbol, "this"));
            args.CancellationToken.ThrowIfCancellationRequested();
        }
        string generatedMethod;
        if (isConstructorMode)
            generatedMethod = $$"""
            public {{args.Symbol.Name}}() {
                {{sfc.Scirpt?.RawScript}}
                {{codeBuilder.ToString().IndentWOF()}}
            }
            """;
        else
            generatedMethod = $$"""
            private void Init() {
                {
                    // in case of re-initialize, cleanup all previous effects
                    foreach (global::QuickMarkup.Infra.RefEffect QUICKMARKUP_EFFECT in QUICKMARKUP_EFFECTS) {
                        QUICKMARKUP_EFFECT.Dispose();
                    }
                    QUICKMARKUP_EFFECTS.Clear();
                }
                {{sfc.Scirpt?.RawScript}}
                {{codeBuilder.ToString().IndentWOF()}}
            }
            """;
        return $"""
            {generatedProperties}
            {generatedMethod}
            """;
    }

    protected override QuickMarkupAttributeWarpper? TransformAttribute(AttributeData attributeData, Compilation compilation)
        => AttributeDataToQuickMarkupAttribute(attributeData, compilation);
    IEnumerable<IToken<QuickMarkupLexer.Tokens>> Lex(string code)
    {
        return new QuickMarkupLexer(new StringTextSeeker(code)).GetTokens();
    }
    ThreadLocal<QuickMarkupParser> ParserPerThread { get; } = new(() => new QuickMarkupParser());
    QuickMarkupSFC Parse(IEnumerable<IToken<QuickMarkupLexer.Tokens>> tokens)
    {
        return ParserPerThread.Value.Parse(tokens);
    }
    QuickMarkupSFC Parse(string code)
    {
        return Parse(Lex(code));
    }
}
