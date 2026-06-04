using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;
using LspDiagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;

namespace QuickMarkup.LanguageServer.Diagnostics;

public class QmuiDiagnosticService : IQmuiDiagnosticService
{
    readonly IRoslynWorkspaceManager _workspaceManager;
    readonly QuickMarkupWorkspaceCatalog _catalog;
    readonly IFileProvider _fileProvider;
    QuickMarkupGeneratedMemberTable? _generatedMemberTable;

    public QmuiDiagnosticService(
        IRoslynWorkspaceManager workspaceManager,
        QuickMarkupWorkspaceCatalog catalog,
        IFileProvider fileProvider)
    {
        _workspaceManager = workspaceManager;
        _catalog = catalog;
        _fileProvider = fileProvider;
    }

    public Task<IReadOnlyList<LspDiagnostic>> GetDiagnosticsAsync(
        string filePath, string content, CancellationToken ct)
    {
        var (sfc, parseErrors) = QuickMarkupProviderExtension.ParseWithErrors(content);
        var compilation = GetEnrichedCompilation(ct);

        if (sfc is null)
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>([]);

        if (compilation is null)
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
                LspDiagnosticConverter.ConvertParseErrors(parseErrors, content));

        var generatedMembers = GetOrBuildGeneratedMemberTable(compilation);

        var ns = sfc.Namespace?.Name ?? "";
        var typeName = sfc.ClassDeclaration?.Name ?? "";
        if (string.IsNullOrEmpty(typeName))
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
                LspDiagnosticConverter.ConvertParseErrors(parseErrors, content));

        var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
        var typeSym = compilation.GetTypeByMetadataName(fullName);
        if (typeSym is not null)
        {
            var binder = Bind(compilation, sfc, typeSym, ns, generatedMembers);
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
                LspDiagnosticConverter.ConvertAll(binder.Diagnostics, parseErrors, content));
        }
        if (sfc.ClassDeclaration is { } classDecl)
        {
            // Use the shared enricher to create a dummy class with file-scoped namespace
            var target = new QuickMarkupTargetContext(
                Namespace: ns,
                TypeName: typeName,
                FullTypeName: fullName,
                FileName: filePath,
                AttributeLocation: default,
                AttributeLineSpan: default);

            var compilationWithDummy = QuickMarkupCompilationEnricher.EnsureTypeSymbolInCompilation(target, sfc, compilation);

            var dummyTypeSym = compilationWithDummy.GetTypeByMetadataName(fullName);
            if (dummyTypeSym is null)
            {
                // Fallback to just parse errors if we still can't find the type (should not happen)
                goto fallback;
            }

            var dummyBinder = Bind(compilationWithDummy, sfc, dummyTypeSym, ns, generatedMembers);
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
                LspDiagnosticConverter.ConvertAll(dummyBinder.Diagnostics, parseErrors, content));
        }
    fallback:
        return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
            LspDiagnosticConverter.ConvertParseErrors(parseErrors, content));
    }

    private Compilation? GetEnrichedCompilation(CancellationToken ct)
    {
        var compilation = _workspaceManager.Compilation;
        if (compilation is null)
            return null;

        // If catalog is empty and we have a workspace root, rebuild it
        if (_catalog.Entries.Length == 0 && _workspaceManager.CurrentProjectPath is not null)
        {
            var workspaceRoot = Path.GetDirectoryName(_workspaceManager.CurrentProjectPath);
            if (workspaceRoot is not null)
            {
                _catalog.Rebuild(compilation, workspaceRoot, _fileProvider);
            }
        }

        // Enrich compilation with all catalog .qmui entries
        foreach (var entry in _catalog.Entries)
        {
            if (entry.Kind == QuickMarkupDefinitionKind.QmuiFile && !string.IsNullOrEmpty(entry.FilePath))
            {
                try
                {
                    var content = _fileProvider.ReadAllText(entry.FilePath);
                    var sfc = QuickMarkupProviderExtension.Parse(content);
                    if (sfc is not null && sfc.ClassDeclaration is not null)
                    {
                        var target = new QuickMarkupTargetContext(
                            Namespace: entry.Namespace,
                            TypeName: entry.ShortName,
                            FullTypeName: entry.FullTypeName,
                            FileName: entry.FilePath,
                            AttributeLocation: default,
                            AttributeLineSpan: default);

                        compilation = QuickMarkupCompilationEnricher.EnsureTypeSymbolInCompilation(target, sfc, compilation);
                    }
                }
                catch (Exception)
                {
                    // Skip problematic files
                }
            }
        }

        return compilation;
    }

    private QuickMarkupGeneratedMemberTable GetOrBuildGeneratedMemberTable(Compilation compilation)
    {
        if (_generatedMemberTable is not null)
            return _generatedMemberTable;

        _generatedMemberTable = GeneratedMemberTableBuilder.Build(_catalog, _fileProvider, compilation);
        return _generatedMemberTable;
    }

    static QuickMarkupBinder Bind(
        Compilation compilation,
        QuickMarkupSFC sfc,
        INamedTypeSymbol typeSym,
        string ns,
        QuickMarkupGeneratedMemberTable generatedMembers)
    {
        var resolver = new CodeTypeResolver(compilation, sfc.Usings, ns, generatedMembers);
        var binder = new QuickMarkupBinder(resolver, failFast: false);

        if (sfc.Template is not null)
        {
            try
            {
                binder.Bind(sfc.Template, typeSym);
            }
            catch (Exception e)
            {
                binder.Diagnostics.Add(new QMBinderError(
                    sfc.Template, $"Internal error during binding: {e.Message}"));
            }
        }

        try
        {
            binder.BindRefDeclarations(sfc.Refs, typeSym);
        }
        catch (Exception e)
        {
            binder.Diagnostics.Add(new QMBinderError(
                sfc, $"Internal error during ref binding: {e.Message}"));
        }

        return binder;
    }
}
