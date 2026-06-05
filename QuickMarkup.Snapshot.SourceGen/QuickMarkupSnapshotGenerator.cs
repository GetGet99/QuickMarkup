using System.Collections.Immutable;
using System.Text;
using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Snapshot.SourceGen.Binders;

namespace QuickMarkup.Snapshot.SourceGen;

[Generator]
partial class QuickMarkupSnapshotGenerator : IIncrementalGenerator
{
    const string SnapshotComponentAttributeMetadataName = "QuickMarkup.Snapshot.SnapshotComponentAttribute";
    const string SnapshotIncludeAttributeMetadataName = "QuickMarkup.Snapshot.SnapshotIncludeAttribute";
    const string SnapshotIgnoreAttributeMetadataName = "QuickMarkup.Snapshot.SnapshotIgnoreAttribute";
    const string SnapshotManualAttributeMetadataName = "QuickMarkup.Snapshot.SnapshotManualAttribute";
    const string SnapshotFormatterMetadataName = "QuickMarkup.Snapshot.ISnapshotFormatter`1";
    const string SnapshotMarkerInterfaceMetadataName = "QuickMarkup.Snapshot.ISnapshotComponent";

    static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    static readonly DiagnosticDescriptor snapshotError = new(
        "QMSNP001",
        "Snapshot source generation error",
        "{0}",
        "QuickMarkupSnapshotGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor snapshotWarning = new(
        "QMSNP002",
        "Snapshot source generation warning",
        "{0}",
        "QuickMarkupSnapshotGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    protected void OnInitialize(IncrementalGeneratorPostInitializationContext context) { }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(OnInitialize);

        var nonErrorMarkups = context.SyntaxProvider.ForAllQuickMarkupSuccessfulParse();

        var snapshotInterfaces = context.SyntaxProvider.ForAttributeWithMetadataName(
            SnapshotComponentAttributeMetadataName,
            static (node, _) => node is InterfaceDeclarationSyntax,
            static (ctx, ct) => CreateSnapshotInterfaceCandidate(ctx, ct)
        );

        var combined = snapshotInterfaces
            .Collect()
            .Combine(nonErrorMarkups.Collect())
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (sourceProductionContext, value) =>
        {
            var ((interfaces, markups), compilation) = value;
            Execute(sourceProductionContext, compilation, interfaces, markups);
        });
    }

    static SnapshotInterfaceCandidate CreateSnapshotInterfaceCandidate(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        var syntaxReference = ctx.Attributes[0].ApplicationSyntaxReference;
        return new(
            QuickMarkupTargetContext.FromSyntaxAndSymbol(symbol, syntaxReference, ct),
            symbol,
            ctx.Attributes[0]
        );
    }

    static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<SnapshotInterfaceCandidate> interfaceCandidates,
        ImmutableArray<QuickMarkupParsedAttribute> markups)
    {
        var snapshotInterfaces = BindSnapshotInterfaces(context, compilation, interfaceCandidates);
        if (snapshotInterfaces.Count == 0)
            return;

        var snapshotInterfaceList = snapshotInterfaces.ToImmutableArray();
        var implementers = BindImplementers(context, compilation, markups, snapshotInterfaceList);

        foreach (var snapshotInterface in snapshotInterfaceList)
        {
            var members = implementers
                .Where(x => !x.HasFatalError && SymbolEqualityComparer.Default.Equals(x.SnapshotInterface.Symbol, snapshotInterface.Symbol))
                .OrderBy(static x => x.Discriminator, StringComparer.Ordinal)
                .ToArray();

            if (members.Length == 0)
                continue;

            EmitSnapshotInterface(context, snapshotInterface, members);
        }

        foreach (var implementer in implementers)
        {
            if (implementer.HasFatalError || !implementer.ShouldGenerateMembers)
                continue;
            EmitSnapshotClass(context, implementer, snapshotInterfaceList);
        }
    }

    static List<SnapshotInterfaceBinding> BindSnapshotInterfaces(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<SnapshotInterfaceCandidate> interfaceCandidates)
    {
        var bindings = new List<SnapshotInterfaceBinding>(interfaceCandidates.Length);
        var snapshotMarkerInterface = compilation.GetTypeByMetadataName(SnapshotMarkerInterfaceMetadataName);
        var snapshotFormatterInterface = compilation.GetTypeByMetadataName(SnapshotFormatterMetadataName);

        foreach (var candidate in interfaceCandidates)
        {
            var symbol = candidate.Symbol;
            if (symbol.TypeKind is not TypeKind.Interface)
            {
                ReportError(context, candidate.Target, "Snapshot component attribute can only be applied to interfaces.");
                continue;
            }

            if (snapshotMarkerInterface is not null
                && !SymbolEqualityComparer.Default.Equals(symbol, snapshotMarkerInterface)
                && !symbol.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x, snapshotMarkerInterface)))
            {
                ReportError(context, candidate.Target, $"{symbol.ToDisplayString()} must implement {snapshotMarkerInterface.ToDisplayString()}.");
                continue;
            }

            if (candidate.Attribute.ConstructorArguments.Length < 3)
            {
                ReportError(context, candidate.Target, "Snapshot component attribute is missing required constructor arguments.");
                continue;
            }

            if (candidate.Attribute.ConstructorArguments[0].Value is not INamedTypeSymbol formatterType)
            {
                ReportError(context, candidate.Target, "Snapshot formatter type could not be resolved.");
                continue;
            }

            var typeKey = candidate.Attribute.ConstructorArguments[1].Value as string;
            if (string.IsNullOrWhiteSpace(typeKey))
            {
                ReportError(context, candidate.Target, "Snapshot discriminator key must be a non-empty string.");
                continue;
            }

            if (candidate.Attribute.ConstructorArguments[2].Value is not int presetValue)
            {
                ReportError(context, candidate.Target, "Snapshot preset could not be resolved.");
                continue;
            }

            if (snapshotFormatterInterface is null)
            {
                ReportError(context, candidate.Target, $"Could not resolve {SnapshotFormatterMetadataName}.");
                continue;
            }

            var nodeType = FindSnapshotNodeType(formatterType, snapshotFormatterInterface);
            if (nodeType is null)
            {
                ReportError(
                    context,
                    candidate.Target,
                    $"{formatterType.ToDisplayString()} must implement {snapshotFormatterInterface.ToDisplayString()}."
                );
                continue;
            }

            var preset = (SnapshotPresetMode)presetValue;
            var configuration = BuildConfiguration(candidate.Attribute, preset);

            bindings.Add(new(
                candidate.Target,
                symbol,
                formatterType,
                nodeType,
                typeKey!,
                configuration
            ));
        }

        return bindings;
    }

    static List<SnapshotImplementerBinding> BindImplementers(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<QuickMarkupParsedAttribute> markups,
        ImmutableArray<SnapshotInterfaceBinding> snapshotInterfaces)
    {
        var implementers = new List<SnapshotImplementerBinding>();
        var byInterfaceAndDiscriminator = new Dictionary<(INamedTypeSymbol Interface, string Discriminator), List<SnapshotImplementerBinding>>(new SnapshotDiscriminatorComparer());

        foreach (var markup in markups)
        {
            if (!markup.Target.TryGetTypeSymbol(compilation, out var typeSymbol, out var failureReason))
            {
                ReportError(
                    context,
                    markup.Target,
                    $"Exception occurred during implementer type resolving: {failureReason.GetType().FullName} {failureReason.Message}"
                );
                continue;
            }

            if (typeSymbol.TypeKind is not TypeKind.Class)
                continue;

            var matches = snapshotInterfaces
                .Where(x => SymbolEqualityComparer.Default.Equals(typeSymbol, x.Symbol) || typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, x.Symbol)))
                .ToArray();

            if (matches.Length == 0)
                continue;

            if (matches.Length > 1)
            {
                ReportError(
                    context,
                    markup.Target,
                    $"{typeSymbol.ToDisplayString()} implements multiple snapshot interfaces. This is not supported by the current generator."
                );
                continue;
            }

            var snapshotInterface = matches[0];
            var implementerMode = GetImplementerMode(typeSymbol);
            if (implementerMode.Error is not null)
            {
                ReportError(context, markup.Target, implementerMode.Error);
                continue;
            }
            if (implementerMode.Mode is SnapshotImplementerMode.Ignored)
                continue;

            if (typeSymbol.IsAbstract)
            {
                ReportError(context, markup.Target, $"{typeSymbol.ToDisplayString()} must be concrete so the snapshot loader can instantiate it.");
                continue;
            }

            if (!HasAccessibleParameterlessConstructor(typeSymbol))
            {
                ReportError(context, markup.Target, $"{typeSymbol.ToDisplayString()} must have an accessible parameterless constructor for snapshot loading.");
                continue;
            }

            if (snapshotInterface.Configuration.DiagnosticMode.HasFlag(SnapshotDiagnosticMode.NoName)
                && !implementerMode.HasExplicitDiscriminator)
            {
                ReportWarning(
                    context,
                    markup.Target,
                    $"This snapshot discriminator may unintentionally change after renaming the class. Please include [{implementerMode.AttributeName}(\"{typeSymbol.Name}\")]."
                );
            }

            var hasFatalError = false;
            IReadOnlyList<SnapshotField> fields = [];
            if (implementerMode.Mode is SnapshotImplementerMode.Generated)
            {
                var resolver = new CodeTypeResolver(compilation, markup.AST.Usings, markup.Target.Namespace);
                var binder = new SnapshotBinder(resolver, Binder.Collect);
                fields = binder.Bind(markup.AST.Refs, snapshotInterface.Configuration);
                foreach (var diagnostic in binder.Diagnostics)
                {
                    if (diagnostic is QMBinderError)
                    {
                        hasFatalError = true;
                        ReportError(context, markup.Target, diagnostic.Message);
                    }
                    else
                    {
                        ReportWarning(context, markup.Target, diagnostic.Message);
                    }
                }
            }

            var implementer = new SnapshotImplementerBinding(
                markup.Target,
                typeSymbol,
                snapshotInterface,
                implementerMode.Discriminator,
                fields,
                hasFatalError,
                shouldGenerateMembers: implementerMode.Mode is SnapshotImplementerMode.Generated
            );

            implementers.Add(implementer);

            var key = (snapshotInterface.Symbol, implementerMode.Discriminator);
            if (!byInterfaceAndDiscriminator.TryGetValue(key, out var list))
            {
                list = [];
                byInterfaceAndDiscriminator[key] = list;
            }
            list.Add(implementer);
        }

        foreach (var duplicates in byInterfaceAndDiscriminator.Values.Where(static x => x.Count > 1))
        {
            foreach (var duplicate in duplicates)
            {
                duplicate.HasFatalError = true;
                ReportError(
                    context,
                    duplicate.Target,
                    $"Duplicate snapshot discriminator \"{duplicate.Discriminator}\" for {duplicate.SnapshotInterface.Symbol.ToDisplayString()}."
                );
            }
        }

        return implementers;
    }

    static void EmitSnapshotInterface(
        SourceProductionContext context,
        SnapshotInterfaceBinding snapshotInterface,
        IReadOnlyList<SnapshotImplementerBinding> implementers)
    {
        var interfaceType = ToFullName(snapshotInterface.Symbol);
        var formatterType = ToFullName(snapshotInterface.FormatterType);
        var nodeType = ToFullName(snapshotInterface.NodeType);

        var switchCases = string.Join(
            Extension.InSourceNewLine,
            implementers.Select(x => $"                {EscapeStringLiteral(x.Discriminator)} => new {ToFullName(x.Symbol)}(),")
        );

        var code = $$"""
            #nullable enable
            namespace {{snapshotInterface.Target.Namespace}};

            partial interface {{snapshotInterface.Target.TypeName}} : global::QuickMarkup.Snapshot.ISnapshotComponent<{{formatterType}}, {{nodeType}}>
            {
                public static {{interfaceType}} LoadFromSnapshot({{formatterType}} formatter, {{nodeType}} jsonNode)
                {
                    {{interfaceType}} component = formatter.ReadKey<string>(jsonNode, {{EscapeStringLiteral(snapshotInterface.TypeKey)}}) switch
                    {
            {{switchCases}}
                        _ => throw new global::System.NotImplementedException()
                    };
                    component.LoadSnapshot(formatter, jsonNode);
                    return component;
                }
            }
            """;

        context.AddSource($"{snapshotInterface.Target.TypeNameSourceGenOutputFriendlyFileName}.SNAPSHOT_INTERFACE.g.cs", code);
    }

    static void EmitSnapshotClass(
        SourceProductionContext context,
        SnapshotImplementerBinding implementer,
        ImmutableArray<SnapshotInterfaceBinding> snapshotInterfaces)
    {
        var formatterType = ToFullName(implementer.SnapshotInterface.FormatterType);
        var nodeType = ToFullName(implementer.SnapshotInterface.NodeType);
        var saveBody = new StringBuilder();
        var loadBody = new StringBuilder();

        saveBody.AppendLine($"var kv = formatter.NewKVNode();");
        saveBody.AppendLine($"formatter.AppendKey(kv, {EscapeStringLiteral(implementer.SnapshotInterface.TypeKey)}, {EscapeStringLiteral(implementer.Discriminator)});");

        foreach (var field in implementer.Fields)
        {
            var nestedResult = FindSnapshotInterfaceForField(field.FieldType, implementer.SnapshotInterface, snapshotInterfaces);
            if (nestedResult.Error is not null)
            {
                ReportError(context, implementer.Target, nestedResult.Error);
                return;
            }

            if (nestedResult.Interface is null)
            {
                saveBody.AppendLine($"formatter.AppendKey(kv, {EscapeStringLiteral(field.JsonName)}, {field.FieldName});");
                loadBody.AppendLine($"{field.FieldName} = formatter.ReadKey<{ToTypeSyntax(field.FieldType)}>(saved, {EscapeStringLiteral(field.JsonName)});");
                continue;
            }

            var nestedSnapshotInterface = nestedResult.Interface;
            saveBody.AppendLine($"formatter.AppendKey(kv, {EscapeStringLiteral(field.JsonName)}, {field.FieldName}.SaveSnapshot(formatter));");
            loadBody.AppendLine(
                $"{field.FieldName} = {ToFullName(nestedSnapshotInterface.Symbol)}.LoadFromSnapshot(formatter, formatter.ReadKey<{ToFullName(nestedSnapshotInterface.NodeType)}>(saved, {EscapeStringLiteral(field.JsonName)}));"
            );
        }

        saveBody.AppendLine("return kv;");

        context.AddSource(
            implementer.Target,
            "SNAPSHOT",
            $$"""
            public void LoadSnapshot({{formatterType}} formatter, {{nodeType}} saved)
            {
                {{loadBody.ToString().IndentWOF()}}
            }

            public {{nodeType}} SaveSnapshot({{formatterType}} formatter)
            {
                {{saveBody.ToString().IndentWOF()}}
            }
            """
        );
    }

    static NestedSnapshotResolution FindSnapshotInterfaceForField(
        ITypeSymbol? fieldType,
        SnapshotInterfaceBinding currentInterface,
        ImmutableArray<SnapshotInterfaceBinding> snapshotInterfaces)
    {
        if (fieldType is null)
            return new(null, null);

        SnapshotInterfaceBinding? match = null;
        foreach (var snapshotInterface in snapshotInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(fieldType, snapshotInterface.Symbol)
                || fieldType.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x, snapshotInterface.Symbol)))
            {
                if (match is not null)
                {
                    return new(
                        null,
                        $"{fieldType.ToDisplayString()} matches multiple snapshot interfaces. Nested snapshot dispatch is ambiguous."
                    );
                }
                match = snapshotInterface;
            }
        }

        if (match is null)
            return new(null, null);

        if (!SymbolEqualityComparer.Default.Equals(match.FormatterType, currentInterface.FormatterType)
            || !SymbolEqualityComparer.Default.Equals(match.NodeType, currentInterface.NodeType))
        {
            return new(
                null,
                $"{fieldType.ToDisplayString()} uses snapshot interface {match.Symbol.ToDisplayString()}, but its formatter/node types do not match {currentInterface.Symbol.ToDisplayString()}."
            );
        }

        return new(match, null);
    }

    static SnapshotImplementerModeResult GetImplementerMode(INamedTypeSymbol typeSymbol)
    {
        var attributes = typeSymbol.GetAttributes();
        var includeMatches = attributes.Where(x => MatchesAttribute(x, SnapshotIncludeAttributeMetadataName) && !MatchesAttribute(x, SnapshotManualAttributeMetadataName)).ToArray();
        var manualMatches = attributes.Where(x => MatchesAttribute(x, SnapshotManualAttributeMetadataName)).ToArray();
        var ignoreMatches = attributes.Where(x => MatchesAttribute(x, SnapshotIgnoreAttributeMetadataName)).ToArray();

        if (ignoreMatches.Length > 0)
            return new(SnapshotImplementerMode.Ignored, "", false, nameof(SnapshotIgnoreAttribute), null);

        if (manualMatches.Length > 1)
            return new(SnapshotImplementerMode.Generated, "", false, nameof(SnapshotManualAttribute), $"Only one [SnapshotManual] may be applied to {typeSymbol.ToDisplayString()}.");
        if (includeMatches.Length > 1)
            return new(SnapshotImplementerMode.Generated, "", false, nameof(SnapshotIncludeAttribute), $"Only one [SnapshotInclude] may be applied to {typeSymbol.ToDisplayString()}.");
        if (manualMatches.Length > 0 && includeMatches.Length > 0)
            return new(SnapshotImplementerMode.Generated, "", false, nameof(SnapshotManualAttribute), $"[{nameof(SnapshotManualAttribute)}] and [SnapshotInclude] cannot both be applied to {typeSymbol.ToDisplayString()}.");

        if (manualMatches.Length > 0)
            return CreateImplementerModeResult(typeSymbol, manualMatches[0], SnapshotImplementerMode.Manual, nameof(SnapshotManualAttribute));
        if (includeMatches.Length > 0)
            return CreateImplementerModeResult(typeSymbol, includeMatches[0], SnapshotImplementerMode.Generated, nameof(SnapshotIncludeAttribute));

        return new(SnapshotImplementerMode.Generated, typeSymbol.Name, false, nameof(SnapshotIncludeAttribute), null);
    }

    static SnapshotImplementerModeResult CreateImplementerModeResult(
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SnapshotImplementerMode mode,
        string attributeName)
    {
        var value = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;

        if (string.IsNullOrWhiteSpace(value))
            return new(mode, typeSymbol.Name, false, attributeName, null);

        return new(mode, value!, true, attributeName, null);
    }

    static bool MatchesAttribute(AttributeData attribute, string metadataName)
    {
        for (var current = attribute.AttributeClass; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == metadataName)
                return true;
        }
        return false;
    }

    static SnasphostConfiguration BuildConfiguration(AttributeData attribute, SnapshotPresetMode preset)
    {
        var config = preset switch
        {
            SnapshotPresetMode.Persistence => new SnasphostConfiguration(SnapshotStateMode.AllExplicit, SnapshotDiagnosticMode.NoName),
            SnapshotPresetMode.Runtime => new SnasphostConfiguration(SnapshotStateMode.IncludesPublic, SnapshotDiagnosticMode.None),
            _ => new SnasphostConfiguration(SnapshotStateMode.AllExplicit, SnapshotDiagnosticMode.None),
        };

        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key is "SnapshotMode" && named.Value.Value is int snapshotMode)
            {
                config = config with { SnapshotMode = (SnapshotStateMode)snapshotMode };
            }
            else if (named.Key is "DiagnosticMode" && named.Value.Value is int diagnosticMode)
            {
                config = config with { DiagnosticMode = (SnapshotDiagnosticMode)diagnosticMode };
            }
        }

        return config;
    }

    static ITypeSymbol? FindSnapshotNodeType(INamedTypeSymbol formatterType, INamedTypeSymbol snapshotFormatterInterface)
    {
        if (SymbolEqualityComparer.Default.Equals(formatterType.OriginalDefinition, snapshotFormatterInterface) && formatterType.TypeArguments.Length == 1)
            return formatterType.TypeArguments[0];

        foreach (var iface in formatterType.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, snapshotFormatterInterface) && iface.TypeArguments.Length == 1)
                return iface.TypeArguments[0];
        }
        return null;
    }

    static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol typeSymbol)
    {
        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            if (ctor.IsImplicitlyDeclared && ctor.Parameters.Length == 0)
                return true;
            if (ctor.Parameters.Length != 0)
                continue;
            if (ctor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)
                return true;
        }
        return false;
    }

    static string ToFullName(ITypeSymbol symbol) => symbol.ToDisplayString(FullyQualifiedTypeFormat);

    static string ToTypeSyntax(ITypeSymbol? symbol) => symbol?.ToDisplayString(FullyQualifiedTypeFormat) ?? "object";

    static string EscapeStringLiteral(string value)
        => "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t") + "\"";

    static void ReportError(SourceProductionContext context, QuickMarkupTargetContext target, string message)
        => context.ReportDiagnostic(Diagnostic.Create(snapshotError, ToLocation(target), message));

    static void ReportWarning(SourceProductionContext context, QuickMarkupTargetContext target, string message)
        => context.ReportDiagnostic(Diagnostic.Create(snapshotWarning, ToLocation(target), message));

    static Location ToLocation(QuickMarkupTargetContext target)
    {
        if (target.FileName is not null)
            return Location.Create(target.FileName, target.AttributeLocation, target.AttributeLineSpan);
        return Location.None;
    }

    public static string? GetExpandedLineText(Location location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        if (!location.IsInSource)
            return null;

        var sourceTree = location.SourceTree;
        var sourceText = sourceTree.GetText();

        var span = location.SourceSpan;
        var startLine = sourceText.Lines.GetLineFromPosition(span.Start);
        var endLine = sourceText.Lines.GetLineFromPosition(span.End);
        var expandedStart = startLine.Start;
        var expandedEnd = endLine.EndIncludingLineBreak;
        var expandedSpan = TextSpan.FromBounds(expandedStart, expandedEnd);

        return sourceText.ToString(expandedSpan);
    }

    static INamedTypeSymbol? TryResolveTypeMetadataName(Compilation compilation, string typeDisplayString)
    {
        var searchTypeName = typeDisplayString.StartsWith("global::", StringComparison.Ordinal)
            ? typeDisplayString["global::".Length..]
            : typeDisplayString;
        var idx = searchTypeName.IndexOf('<');
        if (idx >= 0)
            searchTypeName = searchTypeName[..idx];
        return compilation.GetTypeByMetadataName(searchTypeName);
    }

    readonly record struct SnapshotInterfaceCandidate(
        QuickMarkupTargetContext Target,
        INamedTypeSymbol Symbol,
        AttributeData Attribute
    );

    sealed record SnapshotInterfaceBinding(
        QuickMarkupTargetContext Target,
        INamedTypeSymbol Symbol,
        INamedTypeSymbol FormatterType,
        ITypeSymbol NodeType,
        string TypeKey,
        SnasphostConfiguration Configuration
    );

    sealed class SnapshotImplementerBinding
    {
        public SnapshotImplementerBinding(
            QuickMarkupTargetContext target,
            INamedTypeSymbol symbol,
            SnapshotInterfaceBinding snapshotInterface,
            string discriminator,
            IReadOnlyList<SnapshotField> fields,
            bool hasFatalError,
            bool shouldGenerateMembers)
        {
            Target = target;
            Symbol = symbol;
            SnapshotInterface = snapshotInterface;
            Discriminator = discriminator;
            Fields = fields;
            HasFatalError = hasFatalError;
            ShouldGenerateMembers = shouldGenerateMembers;
        }

        public QuickMarkupTargetContext Target { get; }
        public INamedTypeSymbol Symbol { get; }
        public SnapshotInterfaceBinding SnapshotInterface { get; }
        public string Discriminator { get; }
        public IReadOnlyList<SnapshotField> Fields { get; }
        public bool HasFatalError { get; set; }
        public bool ShouldGenerateMembers { get; }
    }

    readonly record struct NestedSnapshotResolution(SnapshotInterfaceBinding? Interface, string? Error);

    enum SnapshotImplementerMode
    {
        Generated,
        Manual,
        Ignored,
    }

    readonly record struct SnapshotImplementerModeResult(
        SnapshotImplementerMode Mode,
        string Discriminator,
        bool HasExplicitDiscriminator,
        string AttributeName,
        string? Error
    );

    sealed class SnapshotDiscriminatorComparer : IEqualityComparer<(INamedTypeSymbol Interface, string Discriminator)>
    {
        public bool Equals((INamedTypeSymbol Interface, string Discriminator) x, (INamedTypeSymbol Interface, string Discriminator) y)
            => SymbolEqualityComparer.Default.Equals(x.Interface, y.Interface)
                && StringComparer.Ordinal.Equals(x.Discriminator, y.Discriminator);

        public int GetHashCode((INamedTypeSymbol Interface, string Discriminator) obj)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + SymbolEqualityComparer.Default.GetHashCode(obj.Interface);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(obj.Discriminator);
                return hash;
            }
        }
    }
}
