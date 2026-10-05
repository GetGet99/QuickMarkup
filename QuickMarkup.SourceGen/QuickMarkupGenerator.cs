using System.Text;
using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using QuickMarkup.SourceGen.CodeGen;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;
using QuickMarkup.AST;
using Microsoft.CodeAnalysis.CSharp;

namespace QuickMarkup.SourceGen;

[Generator]
partial class QuickMarkupGenerator : IIncrementalGenerator
{
    protected void OnInitialize(IncrementalGeneratorPostInitializationContext context) { }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(OnInitialize);
        var (nonErrorMarkups, errorMarkups) = context.SyntaxProvider.ForAllParsedQuickMarkup();
        var (nonErrorRefs, errorRefs) = context.SyntaxProvider.ForAllParsedQuickRefs();
        var merged = nonErrorMarkups.MergeMarkupAndRefs(nonErrorRefs);
        var generatedMemberTable = merged
            .Combine(context.CompilationProvider)
            .Select((x, ct) =>
            {
                var (merge, compilation) = x;
                try
                {
                    return QuickMarkupGeneratedMemberTableBuilder.BuildTypeMembers(merge, compilation, ct);
                }
                catch (Exception ex)
                {
                    System.Console.Error.WriteLine($"[QuickMarkup] Member table building failed for {merge.Target.FullTypeName}: {ex.Message}");
                    return null;
                }
            })
            .Collect()
            .Select((items, _) => new QuickMarkupGeneratedMemberTable(items.Where(x => x is not null).Select(x => x!.Value)));
        
        // INIT (SETUP + MARKUP) — only types with [QuickMarkup]; [QuickRefs]-only types do not take over constructors
        {
            var sfcs = merged.Where(static x => x.HasQuickMarkup).Select(
                (x, _) =>
                {
                    var combined = CombineMarkupTags(x.MarkupSource!.MarkupTags);
                    return (x.Target, x.MergedUsings, x.MarkupSource!.Scirpt, combined, x.MergedRefs());
                }
            );

            var sources = sfcs.Combine(context.CompilationProvider).Combine(generatedMemberTable).Select(
                (x, ct) =>
                {
                    var (((target, usings, scriptAst, template, allRefs), compilation), generatedMembers) = x;
                    try
                    {
                        return GenerateInitSource(target, usings, template, scriptAst, compilation, generatedMembers, allRefs, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        return (target, usings, "", $"Exception Occured during Bindings or Codegen: {ex.GetType().FullName} {ex.Message}", false, false);
                    }
                }
            );

            context.RegisterSourceOutput(sources, (spc, value) =>
            {
                var (ctx, usings, code, error, isComponent, emitDisposeEffects) = value;
                var typeModifiers = isComponent ? "sealed partial" : "partial";
                usings = $"""
                    using QuickMarkup.Infra.Markup;
                    {usings}
                    """;
                var baseTypes = emitDisposeEffects ? EffectsDisposableInterface : null;
                EmitInitSource(spc, ctx, usings, code, error, typeModifiers, baseTypes);
            });
        }

        // REFS — all merged types ([QuickMarkup], [QuickRefs], or both)
        {
            var withCompilation = merged.Combine(context.CompilationProvider).Combine(generatedMemberTable);

            var lines = withCompilation.Select((x, tok) =>
            {
                var ((merge, compilation), generatedMembers) = x;
                try
                {
                    var (code, isComponent) = GenerateRefsSource(merge, compilation, generatedMembers, tok);
                    return (merge.Target, merge.MergedUsings, code, isComponent);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Console.Error.WriteLine($"[QuickMarkup] Refs generation failed for {merge.Target.FullTypeName}: {ex.Message}");
                    return (merge.Target, merge.MergedUsings, $"/* Refs generation failed: {ex.Message.Replace("*/", "*_/")} */", false);
                }
            });

            context.RegisterSourceOutput(lines, (spc, value) =>
            {
                var (ctx, usings, refsCode, isComponent) = value;
                var typeModifiers = isComponent ? "sealed partial" : "partial";
                usings = $"""
                    using QuickMarkup.Infra.Markup;
                    {usings}
                    """;
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
            context.RegisterSourceOutput(errorRefs, (spc, value) =>
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
                new(new QuickMarkupConstructor(new("root")), new()),
                [.. tags.Select(static t => (IQMNodeChild)t)],
                new("root"), IsSelfClosing: false
            );
    }

    static (QuickMarkupTargetContext Target, string Usings, string Code, string? Error, bool IsComponent, bool EmitDisposeEffects)
        GenerateInitSource(
            QuickMarkupTargetContext target,
            string usings,
            QuickMarkupParsedTag? template,
            QuickMarkupScript? scriptAst,
            Compilation compilation,
            QuickMarkupGeneratedMemberTable? generatedMembers,
            IEnumerable<RefDeclaration> allRefs,
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
            return (target, usings, "", error, false, false);
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
                return (target, usings, "", error, componentKind is not QMComponentKind.None, false);
            }

            var outputType = componentKind is QMComponentKind.Fragment
                ? $"global::QuickMarkup.Infra.FragmentBlock<{componentOutputType?.FullName() ?? "object"}>"
                : componentOutputType?.FullName() ?? "object";
            generatedProperties.AppendLine($"public {outputType} {CodeTypeResolver.ComponentOutputPropertyName} {{ get; private set; }} = null!;");
        }
        ct.ThrowIfCancellationRequested();

        var typeMembers = generatedMembers?.FindTypeMembers(typeSymbol);
        var initMode = typeMembers?.InitMode
            ?? QuickMarkupInitializationMode.DeferredInit;

        // Check for [QuickMarkupNewLifecycle] assembly attribute
        var newLifecycleAttr = compilation.Assembly.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "QuickMarkupNewLifecycleAttribute");
        if (newLifecycleAttr is not null && initMode is QuickMarkupInitializationMode.BackwardCompatible)
        {
            var error = $"Type {target.FullTypeName} must use the new QuickMarkup lifecycle because the assembly has [QuickMarkupNewLifecycle]. Remove explicit constructors or add a [QuickMarkupConstructor] method.";
            return (target, usings, "", error, componentKind is not QMComponentKind.None, false);
        }

        // Check for provide/inject with backward compatible mode
        var hasProvideOrInject = allRefs.Any(r => r.Kind is RefDeclarationKind.Provide or RefDeclarationKind.Inject or RefDeclarationKind.InjectOptional);
        if (hasProvideOrInject && initMode is QuickMarkupInitializationMode.BackwardCompatible)
        {
            var error = $"Type {target.FullTypeName} uses provide/inject but has BackwardCompatible init mode (has explicit constructors). Provide/Inject requires the new lifecycle. Remove explicit constructors or add a [QuickMarkupConstructor] method.";
            return (target, usings, "", error, componentKind is not QMComponentKind.None, false);
        }

        // Provide/inject init code builder
        StringBuilder provideInjectInit = new();

        // Bind all declarations and generate init code for provide/inject
        var binder = new QuickMarkupBinder(componentInfoResolver, Binder.FailFast);
        var boundRefs = binder.BindRefDeclarations(allRefs, typeSymbol);

        foreach (var bound in boundRefs)
        {
            var ctxName = bound.ContextName is null ? null : SymbolDisplay.FormatLiteral(bound.ContextName, false); 
            if (bound.Kind is RefDeclarationKind.Provide)
            {
                var typeName = TypeSymbolName(bound.RefType);
                provideInjectInit.AppendLine($"Context.Provide<{typeName}>(\"{ctxName}\", {bound.BackingName});");
            }
            else if (bound.Kind is RefDeclarationKind.Inject or RefDeclarationKind.InjectOptional)
            {
                var typeName = TypeSymbolName(bound.RefType);
                if (bound.Kind is RefDeclarationKind.InjectOptional)
                    provideInjectInit.AppendLine($"{bound.BackingName} = Context.TryInject<{typeName}>(\"{ctxName}\");");
                else
                    provideInjectInit.AppendLine($"{bound.BackingName} = Context.Inject<{typeName}>(\"{ctxName}\");");
            }
        }

        try
        {
            if (template is not null)
            {
                var analyzer = new QuickMarkupBinder(componentInfoResolver, Binder.FailFast);
                var output = analyzer.Bind(template, typeSymbol);
                ct.ThrowIfCancellationRequested();
                var hasExplicitConstructors = typeSymbol.InstanceConstructors.Any(x => !x.IsImplicitlyDeclared);
                var cgen = new CodeGenContext(
                    generatedProperties,
                    codeBuilder,
                    initMode,
                    hasExplicitConstructors,
                    frameworkConfig
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
            return (target, usings, "", error, componentKind is not QMComponentKind.None, false);
        }

        var requiredRefs = GetRequiredRefs(typeMembers);
        if (requiredRefs.Count > 0 && initMode is QuickMarkupInitializationMode.BackwardCompatible)
        {
            var error = $"Type {target.FullTypeName} has required properties but uses BackwardCompatible init mode (has explicit constructors). To use required properties, remove explicit constructors or add a [QuickMarkupConstructor] method to control initialization.";
            return (target, usings, "", error, componentKind is not QMComponentKind.None, false);
        }

        var emitDisposeEffects = !typeSymbol.GetMembers("DisposeEffects")
            .OfType<IMethodSymbol>()
            .Any(m => m.Parameters.Length == 0);
        if (emitDisposeEffects)
            generatedProperties.AppendLine(BuildDisposeEffectsMembers(boundRefs));

        string generatedMethod = GenerateInitMethod(typeSymbol, initMode, typeMembers, scriptAst?.RawScript, scriptAst?.IsAsync == true, codeBuilder, requiredRefs, provideInjectInit.ToString(), emitDisposeEffects);
        return (target, usings, $$"""
                    {{generatedProperties}}
                    {{generatedMethod}}
                    """, default(string), componentKind is not QMComponentKind.None, emitDisposeEffects);
    }

    static string BuildDisposeEffectsMembers(
        IReadOnlyList<QuickMarkup.Language.Symbols.QMRefDeclarationSymbol<ITypeSymbol?>> boundRefs)
    {
        var backingDisposal = new StringBuilder();
        foreach (var bound in boundRefs)
        {
            if (bound.Kind is not (RefDeclarationKind.Computed or RefDeclarationKind.AsyncComputed) || bound.IsStatic)
                continue;

            backingDisposal.AppendLine($"{bound.BackingFieldName}?.Dispose();");
            backingDisposal.AppendLine($"{bound.BackingFieldName} = null;");
        }

        return $$"""
            private bool QUICKMARKUP_EFFECTS_DISPOSED;
            public void DisposeEffects() {
                if (QUICKMARKUP_EFFECTS_DISPOSED) {
                    return;
                }
                QUICKMARKUP_EFFECTS_DISPOSED = true;
                foreach (global::System.IDisposable QUICKMARKUP_DISPOSABLE in QUICKMARKUP_DISPOSABLES) {
                    QUICKMARKUP_DISPOSABLE.Dispose();
                }
                QUICKMARKUP_DISPOSABLES.Clear();
                {{backingDisposal.ToString().IndentWOF(1).TrimEnd()}}
            }
            """;
    }

    static string TypeSymbolName(ITypeSymbol? type)
        => type?.FullName() ?? "object";

    static List<(string TypeName, string Name)> GetRequiredRefs(QuickMarkupGeneratedTypeMembers? typeMembers)
    {
        if (!typeMembers.HasValue) return [];
        var result = new List<(string TypeName, string Name)>();
        foreach (var kvp in typeMembers.Value.Properties)
        {
            // TypeName may be null for unresolved types; skip — we can't emit a typed constructor parameter without it
            if (kvp.Value.Kind is QuickMarkupGeneratedPropertyKind.RefValue && kvp.Value.IsRequired && kvp.Value.TypeName is not null)
            {
                result.Add((kvp.Value.TypeName, kvp.Key));
            }
        }
        return result;
    }

    static string GenerateInitMethod(
        ITypeSymbol typeSymbol,
        QuickMarkupInitializationMode initMode,
        QuickMarkupGeneratedTypeMembers? typeMembers,
        string? script,
        bool isAsync,
        StringBuilder codeBuilder,
        List<(string TypeName, string Name)> requiredRefs,
        string provideInjectInitCode,
        bool emitDisposeEffects)
    {
        var typeName = typeSymbol.Name;
        var scriptBody = script ?? "// No raw scripts was provided";
        var initBody = codeBuilder.ToString();

        var effectsDisposedReset = emitDisposeEffects ? "QUICKMARKUP_EFFECTS_DISPOSED = false;" : "";
        var cleanupBlock = $$"""
            {
                // in case of re-initialize, cleanup all previous generated disposables
                {{effectsDisposedReset}}
                foreach (global::System.IDisposable QUICKMARKUP_DISPOSABLE in QUICKMARKUP_DISPOSABLES) {
                    QUICKMARKUP_DISPOSABLE.Dispose();
                }
                QUICKMARKUP_DISPOSABLES.Clear();
            }
            """;

        const string initMethodName = "Init";

        BuildConstructorSignatures(
            typeName, initMethodName, typeMembers, requiredRefs, isAsync,
            out var initParamSig, out var actionParamSig, out var primaryParamSig,
            out var reqAssignmentsBlock, out var userCtorCall);

        var initMethodSignature = isAsync
            ? $"private async global::System.Threading.Tasks.Task {initMethodName}({initParamSig})"
            : $"private void {initMethodName}({initParamSig})";

        var initMethod = $$"""
            {{initMethodSignature}} {
                {{cleanupBlock.IndentWOF()}}
                {{scriptBody.IndentWOF()}}
                {{initBody.IndentWOF()}}
            }
            """;

        if (initMode is QuickMarkupInitializationMode.BackwardCompatible)
        {
            return initMethod;
        }

        var actionBody = $"""
            quickMarkupInitializer(this);
            Context ??= new global::QuickMarkup.Infra.QuickMarkupContext();
            {provideInjectInitCode}
            {userCtorCall}
            """;

        var primaryBody = $"""
            {reqAssignmentsBlock}
            Context = QUICKMARKUP_CONTEXT ?? new global::QuickMarkup.Infra.QuickMarkupContext();
            {provideInjectInitCode}
            {userCtorCall}
            """;

        return $"""
        {GenerateCtor(typeName, primaryParamSig, primaryBody)}

        {GenerateCtor(typeName, actionParamSig, actionBody)}

        {initMethod}
        """;
    }

    static void BuildConstructorSignatures(
        string typeName,
        string initMethodName,
        QuickMarkupGeneratedTypeMembers? typeMembers,
        List<(string TypeName, string Name)> requiredRefs,
        bool isAsync,
        out StringBuilder initParamSig,
        out StringBuilder actionParamSig,
        out StringBuilder primaryParamSig,
        out StringBuilder reqAssignmentsBlock,
        out StringBuilder userCtorCall)
    {
        var userCtorMethodName = typeMembers?.QuickMarkupConstructorMethodName;
        var userCtorParams = typeMembers?.ConstructorParameters;

        initParamSig = new();
        actionParamSig = new();
        primaryParamSig = new();
        reqAssignmentsBlock = new();

        if (userCtorMethodName is null)
            userCtorCall = isAsync
                ? new($"_ = {initMethodName}();")
                : new($"{initMethodName}();");
        else
        {
            userCtorCall = new();
            userCtorCall.Append(userCtorMethodName);
            userCtorCall.Append('(');
            if (userCtorParams is { Count: > 0 })
            {
                for (int i = 0; i < userCtorParams.Count; i++)
                {
                    var (type, name) = userCtorParams[i];
                    if (i is not 0)
                    {
                        initParamSig.Append(", ");
                        actionParamSig.Append(", ");
                        primaryParamSig.Append(", ");
                        userCtorCall.Append(", ");
                    }
                    var typeAndName = $"{type} {name}";
                    initParamSig.Append(typeAndName);
                    actionParamSig.Append(typeAndName);
                    primaryParamSig.Append(typeAndName);
                    userCtorCall.Append(name);
                }
            }
            userCtorCall.Append(");");
        }

        if (requiredRefs is { Count: > 0 })
        {
            for (int i = 0; i < requiredRefs.Count; i++)
            {
                var (type, name) = requiredRefs[i];
                if (i is not 0)
                {
                    primaryParamSig.Append(", ");
                    reqAssignmentsBlock.AppendLine();
                }
                else if (userCtorParams is { Count: > 0 })
                    primaryParamSig.Append(", ");
                var typeAndName = $"{type} {name}";
                primaryParamSig.Append(typeAndName);
                reqAssignmentsBlock.Append($"this.{name} = {name};");
            }
        }

        if (actionParamSig.Length > 0)
            actionParamSig.Append(", ");
        actionParamSig.Append($"global::System.Action<{typeName}> quickMarkupInitializer");

        if (primaryParamSig.Length > 0)
            primaryParamSig.Append(", ");
        primaryParamSig.Append($"global::QuickMarkup.Infra.QuickMarkupContext? QUICKMARKUP_CONTEXT = null");
    }

    static string GenerateCtor(string typeName, StringBuilder paramSig, string body)
    {
        return $$"""
        [global::QuickMarkup.SourceGen.QuickMarkupGeneratedConstructor]
        public {{typeName}}({{paramSig}}) {
            {{body.IndentWOF()}}
        }
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

    static (string Code, bool IsComponent) GenerateRefsSource(
        QuickMarkupMergedType merged,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable? generatedMembers,
        CancellationToken ct)
    {
        var frameworkConfig = FrameworkConfigurationReader.ReadFromCompilation(compilation) ?? FrameworkConfiguration.Default;
        var analysis = QuickMarkupFileAnalyzer.AnalyzeMerged(
            merged, compilation,
            generatedMembers ?? QuickMarkupGeneratedMemberTable.Empty, frameworkConfig, failFast: true);

        StringBuilder sb = new();
        var rgen = new RefsGenContext(sb, merged.Target.FullTypeName);
        rgen.CGenWrite(analysis.RefDeclarations, ct);
        return (sb.ToString(), analysis.IsComponent);
    }

    const string ContextAwareInterface = "global::QuickMarkup.Infra.IQuickMarkupContextAware";
    const string EffectsDisposableInterface = "global::QuickMarkup.Infra.IQuickMarkupEffectsDisposable";

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
        // All generated components implement IQuickMarkupContextAware for context propagation
        var combinedBaseTypes = CombineBaseTypes(baseTypes, ContextAwareInterface);
        spc.AddSource(ctx, "INIT", code, usings, typeModifiers, combinedBaseTypes);
    }

    static void EmitRefsSource(SourceProductionContext spc, QuickMarkupTargetContext ctx, string usings, string refsCode, string typeModifiers, string? baseTypes = null)
    {
        var combinedBaseTypes = CombineBaseTypes(baseTypes, ContextAwareInterface);
        spc.AddSource(ctx, "REFS", refsCode, usings, typeModifiers, combinedBaseTypes);
    }

    static string? CombineBaseTypes(string? existing, string additional)
    {
        if (string.IsNullOrEmpty(existing))
            return additional;
        if (existing!.Contains(additional))
            return existing;
        return $"{existing}, {additional}";
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
