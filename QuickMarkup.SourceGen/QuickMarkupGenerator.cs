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
        var ns = $"""
            {sfc.Usings.RawScript}
            namespace {args.Symbol.ContainingNamespace}.QUICKMARKUP_TEMP_NAMESPACE;
            """;
        StringBuilder generatedProperties = new();
        StringBuilder codeBuilder = new();
        generatedProperties.AppendLine("global::System.Collections.Generic.List<global::QuickMarkup.Infra.RefEffect> QUICKMARKUP_EFFECTS { get; } = [];");
        var isConstructorMode = !args.Symbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared);
        var cgen = new CodeGenContext(
            new(args.GenContext.SemanticModel.Compilation, ns),
            generatedProperties,
            codeBuilder,
            isConstructorMode
        );
        cgen.CGenWrite(sfc.Template, new(args.Symbol, "this"));
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
    QuickMarkupSFC Parse(IEnumerable<IToken<QuickMarkupLexer.Tokens>> tokens)
    {
        return new QuickMarkupParser().Parse(tokens);
    }
    QuickMarkupSFC Parse(string code)
    {
        return Parse(Lex(code));
    }
}
