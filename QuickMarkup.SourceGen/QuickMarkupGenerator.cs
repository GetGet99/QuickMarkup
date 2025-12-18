using Get.EasyCSharp.GeneratorTools;
using Get.Lexer;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickMarkup.AST;
using QuickMarkup.Parser;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
        int counter = 0;
        if (args.Symbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared))
            return $$"""
            global::System.Collections.Generic.List<global::QuickMarkup.Infra.RefEffect> QUICKMARKUP_EFFECTS { get; } = [];
            private void Init() {
                {{GenerateChildren(args, sfc.Template, sfc.Usings.RawScript, ref counter, out _, args.Symbol).IndentWOF()}}
            }
            """;
        else
                    return $$"""
            global::System.Collections.Generic.List<global::QuickMarkup.Infra.RefEffect> QUICKMARKUP_EFFECTS { get; } = [];
            public {{args.Symbol.Name}}() {
                {{GenerateChildren(args, sfc.Template, sfc.Usings.RawScript, ref counter, out _, args.Symbol).IndentWOF()}}
            }
            """;
    }

    string GenerateChildren(OnPointVisitArguments args, QuickMarkupXMLNode node, string usings, ref int counterRef, out string varNameOut, ITypeSymbol? typeOfCurrent = null)
    {
        string varName;
        var code = new StringBuilder();
        if (typeOfCurrent is not null)
        {
            varName = varNameOut = $"this";
        }
        else
        {
            varName = varNameOut = $"QUICKMARKUP_NODE_{counterRef++}";
            typeOfCurrent = GetTypeSymbol(args.GenContext.SemanticModel.Compilation, node.Name, usings);
            code.AppendLine($"var {varName} = new {(typeOfCurrent is null ? node.Name : new Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members.FullType(typeOfCurrent))}();");
        }
        void AddProperty(QuickMarkupXMLPropertiesKeyValue prop, ref int counterRef)
        {
            if (prop is QuickMarkupXMLPropertiesKeyForeign foreign)
            {
                code.AppendLine($$"""
                QUICKMARKUP_EFFECTS.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange(() => {
                    return {{foreign.ForeignAsString}};
                }, x => {
                    {{varName}}.{{prop.Key}} = x;
                }));
                """);
            }
            else if (prop is QuickMarkupXMLPropertiesKeyXML xml)
            {
                code.AppendLine($"""
                    {GenerateChildren(args, xml.Value, usings, ref counterRef, out var childVarName)}
                    {varName}.{prop.Key} = {childVarName};
                    """);
            }
            else
            {
                code.AppendLine($"""
                {varName}.{prop.Key} = {prop switch
                {
                    QuickMarkupXMLPropertiesKeyString str => $"\"{SymbolDisplay.FormatLiteral(str.Value, false)}\"",
                    QuickMarkupXMLPropertiesKeyBoolean boolean => boolean.Value ? "true" : "false",
                    QuickMarkupXMLPropertiesKeyInt32 int32 => int32.Value.ToString(),
                    _ => throw new NotImplementedException()
                }};
                """);
            }
        }
        foreach (var prop in node.Properties)
        {
            AddProperty(prop, ref counterRef);
        }
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupXMLPropertiesKeyValue prop)
                AddProperty(prop, ref counterRef);
        }
        bool isMultipleNode = false;
        IPropertySymbol? content = null;
        bool hasContentProperty = typeOfCurrent is null ? false : TryGetContentProperty(typeOfCurrent, out content, out isMultipleNode);
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupXMLNode childNode)
            {
                code.AppendLine(GenerateChildren(args, childNode, usings, ref counterRef, out var childNodeName));
                if (hasContentProperty && !isMultipleNode)
                {
                    code.AppendLine($"{varName}.{content!.Name} = {childNodeName};");
                }
                else
                {
                    code.AppendLine($"{varName}.{content?.Name ?? "Children"}.Add({childNodeName});");
                }
            }
        }
        return code.ToString();
    }

    public static INamedTypeSymbol ResolveTypeSymbol(
    Compilation compilation,
    string typeName,
    string extraUsings = "")
    {

        // Minimal valid source file
        var source = $$"""
            {{extraUsings}}

            class __TypeResolutionDummy
            {
                {{typeName}} __field;
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(source, (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        // IMPORTANT: add the tree to the compilation
        var newCompilation = compilation.AddSyntaxTrees(syntaxTree);
        var semanticModel = newCompilation.GetSemanticModel(syntaxTree);

        var root = syntaxTree.GetRoot();

        var fieldDeclaration = root
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Single();

        var typeSyntax = fieldDeclaration.Declaration.Type;

        var typeInfo = semanticModel.GetTypeInfo(typeSyntax);

        return (INamedTypeSymbol)typeInfo.Type!;
    }


    bool TryGetContentProperty(ITypeSymbol symbol, [MaybeNullWhen(false)] out IPropertySymbol propertySymbol, out bool isMultipleNode)
    {
        isMultipleNode = false;
        var content = symbol.GetMembers("Content");
        if (content.Length is 0)
            content = symbol.GetMembers("Child");
        if (content.Length is 0)
        {
            content = symbol.GetMembers("Children");
            isMultipleNode = true;
        }
        if (content.Length is 0)
        {
            content = symbol.GetMembers("Items");
            isMultipleNode = true;
        }
        if (content.Length is not 1)
        {
            propertySymbol = null;
            return false;
        }
        if (content[0] is not IPropertySymbol prop)
        {
            propertySymbol = null;
            return false;
        }
        propertySymbol = prop;
        return true;
    }

    protected override QuickMarkupAttributeWarpper? TransformAttribute(AttributeData attributeData, Compilation compilation)
        => AttributeDataToQuickMarkupAttribute(attributeData, compilation);
    IEnumerable<IToken<QuickMarkupLexer.Tokens>> Lex(string code)
    {
        return new QuickMarkupLexer(new StreamSeeker(new MemoryStream(Encoding.UTF8.GetBytes(code)))).GetTokens();
    }
    QuickMarkupSFC Parse(IEnumerable<IToken<QuickMarkupLexer.Tokens>> tokens)
    {
        return new QuickMarkupParser().Parse(tokens);
    }
    QuickMarkupSFC Parse(string code)
    {
        return Parse(Lex(code));
    }
    private static INamedTypeSymbol? GetTypeSymbol(
    Compilation compilation,
    string typeName, string usings)
    {
        var parseOptions = (CSharpParseOptions)
            compilation.SyntaxTrees.First().Options;

        var source = $$"""
            {{usings}}

            class QUICKMARKUP__TypeResolutionDummy2
            {
                {{typeName}} __field;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var newCompilation = compilation.AddSyntaxTrees(tree);
        var model = newCompilation.GetSemanticModel(tree);

        var field = tree.GetRoot()
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Single();

        return model.GetTypeInfo(field.Declaration.Type)
            .Type as INamedTypeSymbol;
    }

}
