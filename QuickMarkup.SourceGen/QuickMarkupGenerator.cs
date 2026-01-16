using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Get.Lexer;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QuickMarkup.AST;
using QuickMarkup.Parser;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        var ns = $"""
                {sfc.Usings.RawScript}
                namespace {args.Symbol.ContainingNamespace}.QUICKMARKUP_TEMP_NAMESPACE;
                """;
        StringBuilder generatedProperties = new();
        generatedProperties.AppendLine("global::System.Collections.Generic.List<global::QuickMarkup.Infra.RefEffect> QUICKMARKUP_EFFECTS { get; } = [];");
        string generatedMethod;
        if (args.Symbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared))
            generatedMethod = $$"""
            private void Init() {
                {{sfc.Scirpt?.RawScript}}
                {{GenerateChildren(new(args, generatedProperties, ns, IsConstuctor: false), sfc.Template, ref counter, out _, new(args.Symbol, "this")).IndentWOF()}}
            }
            """;
        else
            generatedMethod = $$"""
            public {{args.Symbol.Name}}() {
                {{sfc.Scirpt?.RawScript}}
                {{GenerateChildren(new(args, generatedProperties, ns, IsConstuctor: true), sfc.Template, ref counter, out _, new(args.Symbol, "this")).IndentWOF()}}
            }
            """;
        return $"""
            {generatedProperties}
            {generatedMethod}
            """;
    }
    record GenerateChildrenArgs(OnPointVisitArguments OnPointVisitArguments, StringBuilder MembersBuilder, string Usings, bool IsConstuctor);
    record TargetField(ITypeSymbol Type, string Field);
    string GenerateChildren(GenerateChildrenArgs args, QuickMarkupQMNode node, ref int counterRef, out string varNameOut, TargetField? target = null)
    {
        string varName;
        var code = new StringBuilder();
        ITypeSymbol typeOfCurrent;
        if (target is not null)
        {
            typeOfCurrent = target.Type;
            varName = varNameOut = target.Field;
        }
        else if (string.IsNullOrWhiteSpace(node.Name))
        {
            varName = varNameOut = $"QUICKMARKUP_NODE_{counterRef++}";
            typeOfCurrent = GetTypeSymbol(args.OnPointVisitArguments.GenContext.SemanticModel.Compilation, node.TypeName, args.Usings)!;
            code.AppendLine($"var {varName} = new {(typeOfCurrent is null ? node.TypeName : new Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members.FullType(typeOfCurrent))}();");
        }
        else
        {
            varName = varNameOut = node.Name!;
            typeOfCurrent = GetTypeSymbol(args.OnPointVisitArguments.GenContext.SemanticModel.Compilation, node.TypeName, args.Usings)!;
            if (args.IsConstuctor)
            {
                args.MembersBuilder.AppendLine($"private readonly {new FullType(typeOfCurrent)} {varName};");
            } else
            {
                args.MembersBuilder.AppendLine($"private {new FullType(typeOfCurrent)} {varName} = null!;");
            }
                code.AppendLine($"{node.Name} = new {(typeOfCurrent is null ? node.TypeName : new Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members.FullType(typeOfCurrent))}();");
        }
        void AddProperty(QuickMarkupQMPropertiesKeyValue prop, ref int counterRef, ITypeSymbol typeOfCurrent)
        {
            if (prop is QuickMarkupQMPropertiesKeyForeign foreign)
            {
                if (foreign.Key is null)
                {
                    code.AppendLine($"""
                        ((global::System.Action<{new FullType(typeOfCurrent)}>)({foreign.ForeignAsString})).Invoke({varName});
                        """);
                    return;
                }
                if (foreign.IsEventMode)
                {
                    code.AppendLine($$"""
                    {{varName}}.{{prop.Key}} += {{foreign.ForeignAsString}};
                    """);

                }
                else if (foreign.IsBindBack)
                {
                    var propSym = FindProperty(typeOfCurrent, $"{foreign.Key}Property");
                    if (propSym?.Type.Name is "DependencyProperty" && propSym.IsStatic)
                    {
                        code.AppendLine($$"""
                        {{foreign.ForeignAsString}} = {{varName}}.{{prop.Key}};
                        {{varName}}.RegisterPropertyChangedCallback(
                            {{new FullType(propSym.ContainingType)}}.{{propSym.Name}},
                            (_, _) => {
                                {{foreign.ForeignAsString}} = {{varName}}.{{prop.Key}};
                            }
                        );
                        """);
                    }
                    else
                    {
                        propSym = FindProperty(typeOfCurrent, foreign.Key);
                        code.AppendLine($$"""
                        QUICKMARKUP_EFFECTS.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange{{(
                            propSym is null ? "" : $"<{new FullType(propSym.Type)}>"
                        )}} (() => {
                            return {{varName}}.{{prop.Key}};
                        }, x => {
                            {{foreign.ForeignAsString}} = x;
                        }));
                        """);
                    }
                }
                else
                {
                    var propSym = FindProperty(typeOfCurrent, foreign.Key);
                    code.AppendLine($$"""
                    QUICKMARKUP_EFFECTS.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange{{(
                            propSym is null ? "" : $"<{new FullType(propSym.Type)}>"
                        )}} (() => {
                        return {{foreign.ForeignAsString}};
                    }, x => {
                        {{varName}}.{{prop.Key}} = x;
                    }));
                    """);
                }
            }
            else if (prop is QuickMarkupQMPropertiesKeyQM markup)
            {
                code.AppendLine($"""
                    {GenerateChildren(args, markup.Value, ref counterRef, out var childVarName)}
                    {varName}.{prop.Key} = {childVarName};
                    """);
            }
            else if (prop is QuickMarkupQMPropertiesKeyQMs propChildren)
            {
                var newFakeNode = new QuickMarkupQMNode("", []);
                var propSym = FindProperty(typeOfCurrent, propChildren.Key!);
                newFakeNode.Add(propChildren.Value);
                code.AppendLine($"""
                    {GenerateChildren(args, newFakeNode, ref counterRef, out _, new TargetField(propSym!.Type, $"{varName}.{propChildren.Key!}"))}
                    """);
            }
            else if (prop is QuickMarkupQMPropertiesKeyEnum kEnum)
            {
                var propSym = FindProperty(typeOfCurrent, kEnum.Key!);
                code.AppendLine($"""
                {varName}.{prop.Key} = {(propSym is null ? prop.Key : new FullType(propSym.Type))}.{kEnum.EnumMember};
                """);
            }
            else if (prop is QuickMarkupQMPropertiesBoolOrExtension extension)
            {
                var propSym = FindProperty(typeOfCurrent, extension.ExtensionMethod);
                if (propSym is not null)
                {
                    code.AppendLine($"""
                    {varName}.{extension.ExtensionMethod} = true;
                    """);
                }
                else
                {
                    code.AppendLine($"""
                    {varName}.{extension.ExtensionMethod}();
                    """);
                }
            }
            else
            {
                var value = prop switch
                {
                    QuickMarkupQMPropertiesKeyString str => $"\"{SymbolDisplay.FormatLiteral(str.Value, false)}\"",
                    QuickMarkupQMPropertiesKeyBoolean boolean => boolean.Value ? "true" : "false",
                    QuickMarkupQMPropertiesKeyInt32 int32 => int32.Value.ToString(),
                    QuickMarkupQMPropertiesKeyDouble @double => @double.Value.ToString(),
                    _ => throw new NotImplementedException()
                };
                if (FindProperty(typeOfCurrent, prop.Key!) is { } property)
                {
                    var fullname = property.Type.FullName();
                    if (fullname is "double" or "string" or "int" or "bool" or "object" || (fullname.StartsWith("global::System.") && fullname.LastIndexOf('.') == "global::System.".LastIndexOf('.')))
                    {
                        code.AppendLine($"""
                        {varName}.{prop.Key} = {value};
                        """);
                    }
                    else
                    {
                        code.AppendLine($"""
                        {varName}.{prop.Key} = new({value});
                        """);
                    }
                }
                else
                {
                    code.AppendLine($"""
                        {varName}.{prop.Key} = {value};
                        """);
                }
            }
        }
        foreach (var prop in node.Properties)
        {
            AddProperty(prop, ref counterRef, typeOfCurrent!);
        }
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupQMPropertiesKeyValue prop)
                AddProperty(prop, ref counterRef, typeOfCurrent!);
        }
        bool isMultipleNode = false;
        IPropertySymbol? content = null;
        bool hasContentProperty = typeOfCurrent is null ? false : TryGetContentProperty(typeOfCurrent, out content, out isMultipleNode);
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupQMNode childNode)
            {
                code.AppendLine(GenerateChildren(args, childNode, ref counterRef, out var childNodeName));
                if (hasContentProperty && !isMultipleNode)
                {
                    code.AppendLine($"{varName}.{content!.Name} = {childNodeName};");
                }
                else if (hasContentProperty)
                {
                    code.AppendLine($"{varName}.{content!.Name}.Add({childNodeName});");
                } else
                {
                    code.AppendLine($"{varName}.Add({childNodeName});");
                }
            }
            else if (child is QuickMarkupForNode forNode)
            {
                GenerateForNode(forNode, ref counterRef);
                void GenerateForNode(QuickMarkupForNode forNode, ref int counterRef)
                {
                    if (forNode.ListExpression is QuickMarkupForNodeListRangeExpression range)
                    {
                        var i = forNode.TargetVariable;
                        code.AppendLine($$"""
                        for ({{forNode.VarType}} {{i}} = {{range.Start}}; {{i}} < {{range.End}}; {{i}}++) {
                            global::QuickMarkup.Infra.ReactiveHelpers.Closure({{i}}, {{i}} => {
                        """);
                        GenerateForInnerChild(forNode.Children, ref counterRef);
                        code.AppendLine("""
                            });
                        }
                        """);
                    }
                    else if (forNode.ListExpression is QuickMarkupForNodeListForeignExpression foreignExpression)
                    {
                        var x = forNode.TargetVariable;
                        code.AppendLine($$"""
                        foreach ({{forNode.VarType}} {{forNode.TargetVariable}} in ({{foreignExpression.ForeignAsString}})) {
                        """);
                        GenerateForInnerChild(forNode.Children, ref counterRef);
                        code.AppendLine("""
                        }
                        """);
                    }
                }
                void GenerateForInnerChild(ListAST<IQMNodeChild> children, ref int counterRef)
                {
                    foreach (var c in children)
                    {
                        if (c is QuickMarkupQMNode childNode2)
                        {
                            code.AppendLine(GenerateChildren(args, childNode2, ref counterRef, out var childNodeName).Indent());
                            if (content?.Name is { } name)
                            {
                                code.AppendLine($"{varName}.{name}.Add({childNodeName});".Indent());
                            }
                            else
                            {
                                code.AppendLine($"{varName}.Add({childNodeName});".Indent());
                            }
                        }
                        else if (c is QuickMarkupForNode forNode2)
                        {
                            GenerateForNode(forNode2, ref counterRef);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
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
        if (FindContentAttirbute(symbol) is { } result)
        {
            IPropertySymbol? property = null;
            if (result.ConstructorArguments.Length > 0)
                property = FindProperty(symbol, result.ConstructorArguments[0].Value as string);
            else if (result.NamedArguments.Length > 0)
                property = FindProperty(symbol, result.NamedArguments[0].Value.Value as string);
            propertySymbol = property;
            isMultipleNode = FindMethod(propertySymbol?.Type, "Add") is not null;
            return propertySymbol is not null;
        }
        isMultipleNode = false;
        var content = symbol.GetMembers("Child");
        if (content.Length is 0)
            content = symbol.GetMembers("Content");
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

    static AttributeData? FindContentAttirbute(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type;
             current != null;
             current = current.BaseType)
        {
            foreach (var attr in current.GetAttributes())
            {
                if (attr.AttributeClass?.FullName() is "global::Windows.UI.Xaml.Markup.ContentPropertyAttribute")
                {
                    return attr;
                }
            }
        }
        return null;
    }


    static IPropertySymbol? FindProperty(ITypeSymbol type, string property)
    {
        for (ITypeSymbol? current = type;
             current != null;
             current = current.BaseType)
        {
            foreach (var prop in current.GetMembers(property))
            {
                if (prop is IPropertySymbol sym)
                {
                    return sym;
                }
            }
        }
        return null;
    }

    static IMethodSymbol? FindMethod(ITypeSymbol? type, string method)
    {
        for (ITypeSymbol? current = type;
             current != null;
             current = current.BaseType)
        {
            foreach (var prop in current.GetMembers(method))
            {
                if (prop is IMethodSymbol sym)
                {
                    return sym;
                }
            }
        }
        return null;
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
