using System.Text;
using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using QuickMarkup.SourceGen.CodeGen;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;
using QuickMarkup.AST;

namespace QuickMarkup.SourceGen;

[Generator]
partial class QuickMarkupGenerator : IIncrementalGenerator
{
    protected void OnInitialize(IncrementalGeneratorPostInitializationContext context) { }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(OnInitialize);
        var (nonErrorMarkups, errorMarkups) = context.SyntaxProvider.ForAllParsedQuickMarkup();
        var generatedMemberTable = nonErrorMarkups
            .Combine(context.CompilationProvider)
            .Select((x, ct) =>
            {
                var (markup, compilation) = x;
                return QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(markup, compilation, ct);
            })
            .Collect()
            .Select((items, _) => new QuickMarkupGeneratedMemberTable(items.Where(x => x is not null).Select(x => x!.Value)));
        
        // INIT (SETUP + MARKUP)
        {
            var sfcs = nonErrorMarkups.Select(
                (x, _) =>
                {
                    var combined = CombineMarkupTags(x.AST.MarkupTags);
                    return (x.Target, x.AST.Usings, x.AST.Scirpt?.RawScript, combined);
                }
            );

            var sources = sfcs.Combine(context.CompilationProvider).Combine(generatedMemberTable).Select(
                (x, ct) =>
                {
                    var (((target, usings, script, template), compilation), generatedMembers) = x;
                    return GenerateInitSource(target, usings, template, script, compilation, generatedMembers, ct);
                }
            );

            context.RegisterSourceOutput(sources, (spc, value) =>
            {
                var (ctx, usings, code, error, isComponent) = value;
                var typeModifiers = isComponent ? "sealed partial" : "partial";
                EmitInitSource(spc, ctx, usings, code, error, typeModifiers);
            });
        }

        // REFS
        {
            var refs = nonErrorMarkups.Select(
                (x, _) =>
                {
                    return (x.Target, x.AST);
                }
            );

            var withCompilation = refs.Combine(context.CompilationProvider).Combine(generatedMemberTable);

            var lines = withCompilation.Select((x, tok) =>
            {
                var (((target, sfc), compilation), generatedMembers) = x;
                var (code, isComponent) = GenerateRefsSource(target, sfc, compilation, generatedMembers, tok);
                return (target, sfc.Usings, code, isComponent);
            });

            context.RegisterSourceOutput(lines, (spc, value) =>
            {
                var (ctx, usings, refsCode, isComponent) = value;
                var typeModifiers = isComponent ? "sealed partial" : "partial";
                EmitRefsSource(spc, ctx, usings, refsCode, typeModifiers);
            });
        }

        // ERRORS
        {
            context.RegisterSourceOutput(errorMarkups, (spc, value) =>
            {
                var (target, errors) = value;
                EmitErrorSource(spc, target, "ERROR", errors);
            });
        }

        // .QMUI ADDITIONALFILES PIPELINE
        InitializeQmuiPipeline(context);
    }
    static QuickMarkupParsedTag? CombineMarkupTags(ListAST<QuickMarkupParsedTag> tags)
    {
        if (tags.Count == 0)
            return null;
        else if (tags.Count == 1)
            return tags[0];
        else
            return new QuickMarkupParsedTag(
                new QuickMarkupConstructor(new PositionedIdentifier("root")),
                new ListAST<QuickMarkupInlineMember>(),
                new ListAST<IQMNodeChild>(tags.Select(static t => (IQMNodeChild)t).ToList()),
                null, true, null, false
            );
    }

    static (QuickMarkupTargetContext Target, string Usings, string Code, string? Error, bool IsComponent)
        GenerateInitSource(
            QuickMarkupTargetContext target,
            string usings,
            QuickMarkupParsedTag? template,
            string? script,
            Compilation compilation,
            QuickMarkupGeneratedMemberTable? generatedMembers,
            CancellationToken ct)
    {
        if (!target.TryGetTypeSymbol(compilation, out var typeSymbol, out var failureReason))
        {
            var error = $$"""
                Exception Occured during type resolving: {{failureReason.GetType().FullName}} {{failureReason.Message}}
                Messsage: {{failureReason.Message}}
                Stack Trace:
                    {{failureReason.StackTrace.IndentWOF(1)}}
                """;
            return (target, usings, "", error, false);
        }

        StringBuilder generatedProperties = new();
        StringBuilder codeBuilder = new();
        generatedProperties.AppendLine("global::System.Collections.Generic.List<global::System.IDisposable> QUICKMARKUP_DISPOSABLES { get; } = [];");
        var frameworkConfig = FrameworkConfigurationReader.ReadFromCompilation(compilation) ?? FrameworkConfiguration.Default;
        var componentInfoResolver = new CodeTypeResolver(compilation, usings, target.Namespace, generatedMembers, target.FullTypeName, frameworkConfig);
        var componentKind = componentInfoResolver.GetComponentKind(typeSymbol, out var componentOutputType);
        var shouldGenerateComponentOutput = componentKind is not QMComponentKind.None && QuickMarkupGeneratedMemberTableBuilder.HasComponentRootOutput(template, componentKind);
        if (shouldGenerateComponentOutput)
        {
            if (CodeTypeResolver.FindRoslynProperty(typeSymbol, CodeTypeResolver.ComponentOutputPropertyName) is not null)
            {
                var error = $"Type {target.FullTypeName} already declares {CodeTypeResolver.ComponentOutputPropertyName}, but QuickMarkup needs to generate it from <root> children.";
                return (target, usings, "", error, componentKind is not QMComponentKind.None);
            }

            var outputType = componentKind is QMComponentKind.Fragment
                ? $"global::QuickMarkup.Infra.FragmentBlock<{componentOutputType?.FullName() ?? "object"}>"
                : componentOutputType?.FullName() ?? "object";
            generatedProperties.AppendLine($"public {outputType} {CodeTypeResolver.ComponentOutputPropertyName} {{ get; private set; }} = null!;");
        }
        ct.ThrowIfCancellationRequested();

        var typeMembers = generatedMembers?.FindTypeMembers(typeSymbol);
        var initMode = typeMembers?.InitMode
            ?? (typeSymbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared)
                ? QuickMarkupInitializationMode.BackwardCompatible
                : QuickMarkupInitializationMode.DeferredInit);

        try
        {
            if (template is not null)
            {
                var analyzer = new QuickMarkupBinder(componentInfoResolver, Binder.FailFast);
                var output = analyzer.Bind(template, typeSymbol);
                ct.ThrowIfCancellationRequested();
                var cgen = new CodeGenContext(
                    generatedProperties,
                    codeBuilder,
                    initMode
                );
                cgen.CGenWrite(output, "this");
                ct.ThrowIfCancellationRequested();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            var error = $$"""
                Exception Occured during Bindings or Codegen: {{e.GetType().FullName}} {{e.Message}}
                Messsage: {{e.Message}}
                Stack Trace:
                    {{e.StackTrace.IndentWOF(1)}}
                """;
            return (target, usings, "", error, componentKind is not QMComponentKind.None);
        }

        string generatedMethod = GenerateInitMethod(typeSymbol, initMode, typeMembers, script, codeBuilder);
        return (target, usings, $$"""
                    {{generatedProperties}}
                    {{generatedMethod}}
                    """, default(string), componentKind is not QMComponentKind.None);
    }

    static string GenerateInitMethod(
        ITypeSymbol typeSymbol,
        QuickMarkupInitializationMode initMode,
        QuickMarkupGeneratedTypeMembers? typeMembers,
        string? script,
        StringBuilder codeBuilder)
    {
        var typeName = typeSymbol.Name;
        var scriptBody = script ?? "// No raw scripts was provided";
        var initBody = codeBuilder.ToString();
        var cleanupBlock = $$"""
            {
                // in case of re-initialize, cleanup all previous generated disposables
                foreach (global::System.IDisposable QUICKMARKUP_DISPOSABLE in QUICKMARKUP_DISPOSABLES) {
                    QUICKMARKUP_DISPOSABLE.Dispose();
                }
                QUICKMARKUP_DISPOSABLES.Clear();
            }
            """;

        if (initMode is QuickMarkupInitializationMode.BackwardCompatible)
        {
            return $$"""
            private void Init() {
                {{cleanupBlock.IndentWOF()}}
                {{scriptBody.IndentWOF()}}
                {{initBody.IndentWOF()}}
            }
            """;
        }

        // DeferredInit mode
        var ctorMethodName = typeMembers?.QuickMarkupConstructorMethodName;
        var ctorParams = typeMembers?.ConstructorParameters;
        var paramList = ctorParams is { Count: > 0 }
            ? string.Join(", ", ctorParams.Select(p => $"{p.TypeName} {p.Name}"))
            : "";
        var argList = ctorParams is { Count: > 0 }
            ? string.Join(", ", ctorParams.Select(p => p.Name))
            : "";

        var attrGlobalName = "global::QuickMarkup.SourceGen.QuickMarkupGeneratedConstructor";
        string noParamCtor, actionCtor, internalInit;

        if (ctorParams is { Count: > 0 })
        {
            // User has [QuickMarkupConstructor] with parameters
            var ctorParamSig = string.IsNullOrEmpty(paramList) ? "" : $"({paramList})";
            var actionParamSig = string.IsNullOrEmpty(paramList)
                ? $"(global::System.Action<{typeName}> quickMarkupInitializer)"
                : $"({paramList}, global::System.Action<{typeName}> quickMarkupInitializer)";

            var constructorCall = string.IsNullOrEmpty(argList)
                ? $"{ctorMethodName}();"
                : $"{ctorMethodName}({argList});";

            noParamCtor = $$"""
            [{{attrGlobalName}}]
            public {{typeName}}{{ctorParamSig}} {
                {{constructorCall.IndentWOF()}}
                InternalInit({{argList}});
            }
            """;

            actionCtor = $$"""
            [{{attrGlobalName}}]
            public {{typeName}}{{actionParamSig}} {
                {{constructorCall.IndentWOF()}}
                quickMarkupInitializer(this);
                InternalInit({{argList}});
            }
            """;

            internalInit = $$"""
            private void InternalInit({{paramList}}) {
                {{cleanupBlock.IndentWOF()}}
                {{scriptBody.IndentWOF()}}
                {{initBody.IndentWOF()}}
            }
            """;
        }
        else
        {
            // No user constructor or parameterless [QuickMarkupConstructor]
            var hasCtorMethod = ctorMethodName is not null;

            noParamCtor = $$"""
            [{{attrGlobalName}}]
            public {{typeName}}() {
                {{(hasCtorMethod ? $"{ctorMethodName}();".IndentWOF() : "")}}
                InternalInit();
            }
            """;

            actionCtor = $$"""
            [{{attrGlobalName}}]
            public {{typeName}}(global::System.Action<{{typeName}}> quickMarkupInitializer) {
                {{(hasCtorMethod ? $"{ctorMethodName}();".IndentWOF() : "")}}
                quickMarkupInitializer(this);
                InternalInit();
            }
            """;

            internalInit = $$"""
            private void InternalInit() {
                {{cleanupBlock.IndentWOF()}}
                {{scriptBody.IndentWOF()}}
                {{initBody.IndentWOF()}}
            }
            """;
        }

        return $$"""
        {{noParamCtor}}

        {{actionCtor}}

        {{internalInit}}
        """;
    }

    static (string Code, bool IsComponent) GenerateRefsSource(
        QuickMarkupTargetContext target,
        QuickMarkupSFC sfc,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable? generatedMembers,
        CancellationToken ct)
    {
        var frameworkConfig = FrameworkConfigurationReader.ReadFromCompilation(compilation) ?? FrameworkConfiguration.Default;
        var analysis = QuickMarkupFileAnalyzer.Analyze(
            sfc, target.FileName ?? "", target.Namespace, compilation,
            generatedMembers ?? QuickMarkupGeneratedMemberTable.Empty, frameworkConfig, failFast: true);

        StringBuilder sb = new();
        var rgen = new RefsGenContext(sb, target.FullTypeName);
        rgen.CGenWrite(analysis.RefDeclarations, ct);
        return (sb.ToString(), analysis.IsComponent);
    }

    static void EmitInitSource(SourceProductionContext spc, QuickMarkupTargetContext ctx, string usings, string code, string? error, string typeModifiers, string? baseTypes = null)
    {
        if (error is not null)
        {
            code = $$"""
            /*
                {{error.Replace("*/", "*_/")}}
            */
            {{code}}
            """;
        }
        spc.AddSource(ctx, "INIT", code, usings, typeModifiers, baseTypes);
    }

    static void EmitRefsSource(SourceProductionContext spc, QuickMarkupTargetContext ctx, string usings, string refsCode, string typeModifiers, string? baseTypes = null)
    {
        spc.AddSource(ctx, "REFS", refsCode, usings, typeModifiers, baseTypes);
    }

    static void EmitErrorSource(SourceProductionContext spc, QuickMarkupTargetContext ctx, string hintNameSuffix, string error)
    {
        spc.AddSource(ctx, hintNameSuffix, $$"""
        /*
            {{error.Replace("*/", "*_/")}}
        */
        """);
    }
}
