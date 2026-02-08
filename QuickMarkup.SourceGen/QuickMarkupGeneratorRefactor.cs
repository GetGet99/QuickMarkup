//using Get.EasyCSharp.GeneratorTools;
//using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
//using Get.Lexer;
//using Get.PLShared;
//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using QuickMarkup.AST;
//using QuickMarkup.Parser;
//using System.Text;

//namespace QuickMarkup.SourceGen;

//[AddAttributeConverter(typeof(QuickMarkupAttribute), ParametersAsString = "\"\"")]
//partial class QuickMarkupGeneratorRefactor : IIncrementalGenerator
//{
//    protected string? OnPointVisit(OnPointVisitArguments args)
//    {
//        var markup = args.AttributeDatas[0].Wrapper.markup;
//        var sfc = Parse(markup);
//        args.CancellationToken.ThrowIfCancellationRequested();
//        var ns = $"""
//            {sfc.Usings}
//            namespace {args.Symbol.ContainingNamespace}.QUICKMARKUP_TEMP_NAMESPACE;
//            """;
//        args.Usings.Add(sfc.Usings);
//        StringBuilder generatedProperties = new();
//        StringBuilder codeBuilder = new();
//        generatedProperties.AppendLine("global::System.Collections.Generic.List<global::QuickMarkup.Infra.RefEffect> QUICKMARKUP_EFFECTS { get; } = [];");
//        var isConstructorMode = !args.Symbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared);
//        args.CancellationToken.ThrowIfCancellationRequested();
//        var rgen = new RefsGenContext(
//            new(args.GenContext.SemanticModel.Compilation, ns),
//            generatedProperties,
//            args.Symbol.Name
//        );
//        rgen.CGenWrite(sfc.Refs);
//        args.CancellationToken.ThrowIfCancellationRequested();
//        if (sfc.Template is not null)
//        {
//            var cgen = new CodeGenContext(
//                new(args.GenContext.SemanticModel.Compilation, ns),
//                generatedProperties,
//                codeBuilder,
//                isConstructorMode
//            );
//            cgen.CGenWrite(sfc.Template, new(args.Symbol, "this"));
//            args.CancellationToken.ThrowIfCancellationRequested();
//        }
//        string generatedMethod;
//        if (isConstructorMode)
//            generatedMethod = $$"""
//            public {{args.Symbol.Name}}() {
//                {{sfc.Scirpt?.RawScript}}
//                {{codeBuilder.ToString().IndentWOF()}}
//            }
//            """;
//        else
//            generatedMethod = $$"""
//            private void Init() {
//                {
//                    // in case of re-initialize, cleanup all previous effects
//                    foreach (global::QuickMarkup.Infra.RefEffect QUICKMARKUP_EFFECT in QUICKMARKUP_EFFECTS) {
//                        QUICKMARKUP_EFFECT.Dispose();
//                    }
//                    QUICKMARKUP_EFFECTS.Clear();
//                }
//                {{sfc.Scirpt?.RawScript}}
//                {{codeBuilder.ToString().IndentWOF()}}
//            }
//            """;
//        return $"""
//            {generatedProperties}
//            {generatedMethod}
//            """;
//    }

//    IEnumerable<IToken<QuickMarkupLexer.Tokens>> Lex(string code)
//    {
//        return new QuickMarkupLexer(new StringTextSeeker(code)).GetTokens();
//    }
//    ThreadLocal<QuickMarkupParser> ParserPerThread { get; } = new(() => new QuickMarkupParser());
//    QuickMarkupSFC Parse(IEnumerable<IToken<QuickMarkupLexer.Tokens>> tokens)
//    {
//        return ParserPerThread.Value.Parse(tokens);
//    }
//    QuickMarkupSFC Parse(string code)
//    {
//        return Parse(Lex(code));
//    }

//    static readonly string FullAttributeName;
//    static QuickMarkupGeneratorRefactor()
//    {
//        FullAttributeName = typeof(QuickMarkupAttribute).FullName;
//    }

//    protected void OnInitialize(IncrementalGeneratorPostInitializationContext context) { }

//    public void Initialize(IncrementalGeneratorInitializationContext context)
//    {
//        context.RegisterPostInitializationOutput(OnInitialize);
//        var markupStrings = context.SyntaxProvider.ForAttributeWithMetadataName(
//            FullAttributeName,
//            static (syntaxNode, cancelationToken)
//                => syntaxNode is TypeDeclarationSyntax,
//            (ctx, cancel) => (type: new FullType((ITypeSymbol)ctx.TargetSymbol).TypeWithNamespace, markup: ctx.Attributes[0].ConstructorArguments[0].Value as string)
//        );
//        var markups = markupStrings.Where(static ctx => ctx.markup is not null).Select(
//            (x, _) =>
//            {
//                QuickMarkupSFC? markup = null;
//                string error = "";
//                try
//                {
//                    markup = Parse(x.markup!);
//                } catch (Exception e)
//                {
//                    error = $"""
//                        Exception Occured: {e.GetType().FullName}{e.Message}
//                        Messsage: {e.Message}
//                        Stack Trace:
//                            {e.StackTrace.IndentWOF(1)}
//                        """;
//                }
//                return (x.type, markup, error);
//            }
//        );

//        var refs = markups.Where(static ctx => ctx.markup is not null).Select(
//            (x, _) =>
//            {
//                return (x.type, x.markup!.Usings, x.markup!.Refs);
//            }
//        );
//        var withCompilation = context.CompilationProvider.Combine(filtered.Collect());

//        withCompilation.Select((tuple, cancellationToken) =>
//        {
//            var (comp, items) = tuple;
//            foreach (var item in items)
//            {
//                cancellationToken.ThrowIfCancellationRequested();
//            }
//            return x;
//        });


//        context.RegisterSourceOutput(markupStrings, (sourceProductionContext, value) =>
//        {
//            foreach (var diag in value.Item3)
//                sourceProductionContext.ReportDiagnostic(diag);

//            sourceProductionContext.AddSource(value.FileName!.Replace("?", "Nullable"), value.Content!);
//        });
//    }
//    protected record struct OnPointVisitArguments(
//        TypeDeclarationSyntax SyntaxNode,
//        INamedTypeSymbol Symbol,
//        (AttributeData Original, QuickMarkupAttributeWarpper Wrapper)[] AttributeDatas,
//        List<Diagnostic> Diagnostics,
//        CancellationToken CancellationToken,
//        UsingsRef Usings
//    );
//    protected class UsingsRef()
//    {
//        public string AllUsings { get; private set; } = "";
//        public void Add(string usings)
//        {
//            if (string.IsNullOrWhiteSpace(usings))
//                return;
//            AllUsings = $"""
//                {AllUsings}
//                {usings}
//                """;
//        }
//    }
//    protected record struct PreTransformReturnValues(
//        //GeneratorSyntaxContext GenContext,
//        TypeDeclarationSyntax SyntaxNode,
//        INamedTypeSymbol Symbol,
//        AttributeData Original,
//        QuickMarkupSFC? QuickMarkupSFC,
//        List<Diagnostic> Diagnostics,
//        CancellationToken CancellationToken
//    );
//    PreTransformReturnValues? Transform(GeneratorAttributeSyntaxContext genContext, CancellationToken cancelationToken)
//    {
//#if DEBUG
//        //System.Diagnostics.Debugger.Launch();
//        DateTime TransformBegin = DateTime.Now;
//#endif
//        var syntaxNode = (TypeDeclarationSyntax)genContext.TargetNode;
//        // Filter out everything which has no attribute
//        if (syntaxNode.AttributeLists.Count is 0) return null;

//        // Get Symbol
//        if (genContext.SemanticModel.GetDeclaredSymbol(syntaxNode) is not INamedTypeSymbol symbol)
//            return null;

//        // Get Attributes
//        var Class = genContext.SemanticModel.Compilation.GetTypeByMetadataName(FullAttributeName);
//        var attribute = (
//            from x in symbol.GetAttributes()
//            where x.AttributeClass?.IsSubclassFrom(Class) ?? false
//            select (RealAttr: x, WrapperAttr: AttributeDataToQuickMarkupAttribute(x, genContext.SemanticModel.Compilation))
//        ).Where(x => x.RealAttr is not null && x.WrapperAttr is not null).FirstOrDefault();
//        if (attribute.WrapperAttr is null) return null;

//        cancelationToken.ThrowIfCancellationRequested();

//        QuickMarkupSFC? sfc;
//        try
//        {
//            sfc = Parse(attribute.WrapperAttr.markup);
//        } catch
//        {
//            sfc = null;
//        }
//        return new(
//            //genContext,
//            syntaxNode,
//            symbol,
//            attribute.RealAttr,
//            sfc,
//            [],
//            cancelationToken
//        );
//    }
//    void A() {
//        UsingsRef usings = new();
//        string? output;
//#if DEBUG
//        DateTime BeforeProcess = DateTime.Now;
//#endif
//        // All conditions satistfy except for actual running generator
//        List<Diagnostic> diagnostics = [];
//        try
//        {
//            output = OnPointVisit(new(genContext, syntaxNode, symbol, attribute, diagnostics, cancelationToken, usings));
//            if (output is null) return (null, null, [.. diagnostics]);
//        }
//        catch (Exception e)
//        {
//            // Log the exception
//            output = $"""
//            /*
//                Exception Occured: {e.GetType().FullName}{e.Message}
//                Messsage: {e.Message}
//                Stack Trace:
//                    {e.StackTrace.IndentWOF(2)}
//            */
//            """;
//        }
//#if DEBUG
//        DateTime ProcessCompleted = DateTime.Now;
//#endif
//        // All conditions satisfy
//        var containingClass = symbols[0] is INamedTypeSymbol nts ? nts : symbols[0].ContainingType;
//        var genericParams = containingClass.TypeParameters;
//        var classHeader =
//            genericParams.Length is 0 ?
//                containingClass.Name :
//                $"{containingClass.Name}<{string.Join(", ", from x in genericParams select x.Name)}>";
//#if DEBUG
//        TimeSpan EntireProcess = ProcessCompleted - TransformBegin;
//        TimeSpan SubProcess = ProcessCompleted - BeforeProcess;
//#endif
//        return ($"{string.Join(" ", from x in symbols select x.ToString().Replace('<', '[').Replace('>', ']'))}.g.cs",
//#if DEBUG
//            //$"""
//            //// This Generator took {EntireProcess.TotalMilliseconds}ms ({EntireProcess.Ticks} ticks) in total
//            //// SubProcess took {SubProcess.TotalMilliseconds}ms ({SubProcess.Ticks} ticks)
//            //""" + Extension.InSourceNewLine +
//#endif
//            $$"""
//            {{usings.AllUsings}}
//            #nullable enable
//            // Autogenerated for {{string.Join(", ", symbols)}}
            
//            namespace {{containingClass.ContainingNamespace}}
//            {
//                partial {{containingClass.TypeKind switch
//            {
//                TypeKind.Interface => "interface",
//                TypeKind.Struct => "struct",
//                TypeKind.Class or _ => "class",
//            }}} {{classHeader}}
//                {
//                    {{
//                    // Original
//                    /*
//                    {{syntaxNode.ToString().IndentWOF(2)}}
//                    */
//                    ""}}
                    
//                    {{output.IndentWOF(2)}}
//                }
//            }
//            """, [.. diagnostics]);
//    }
//}
