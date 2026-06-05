using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Workspace;

sealed class CompilationService : ICompilationService
{
    static readonly object _msBuildLock = new();
    static bool _msBuildRegistered;

    readonly ICatalogService _catalog;
    readonly IFileProvider _fileProvider;
    readonly IQmuiDocumentStore _documentStore;

    Compilation? _compilation;
    Compilation? _enrichedCompilation;
    volatile bool _enrichmentDirty;

    public Compilation? Compilation => _compilation;
    public Compilation? EnrichedCompilation => _enrichedCompilation;

    public CompilationService(ICatalogService catalog, IFileProvider fileProvider, IQmuiDocumentStore documentStore)
    {
        _catalog = catalog;
        _fileProvider = fileProvider;
        _documentStore = documentStore;
    }

    static void EnsureMSBuildRegistered()
    {
        if (_msBuildRegistered) return;
        lock (_msBuildLock)
        {
            if (_msBuildRegistered) return;
            MSBuildLocator.RegisterDefaults();
            _msBuildRegistered = true;
        }
    }

    public async Task<bool> LoadProjectAsync(string csprojPath, string workspaceRoot, CancellationToken ct = default)
    {
        EnsureMSBuildRegistered();

        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(csprojPath, cancellationToken: ct);
        _compilation = await project.GetCompilationAsync(ct);
        if (_compilation is null)
            return false;

        _enrichedCompilation = null;
        _enrichmentDirty = true;

        await _catalog.InitializeAsync(workspaceRoot, project, _fileProvider, _documentStore);

        return true;
    }

    public async ValueTask<Compilation?> GetEnrichedCompilationAsync(CancellationToken ct = default)
    {
        if (_compilation is null)
            return null;

        if (_enrichedCompilation is not null && !_enrichmentDirty)
            return _enrichedCompilation;

        return await RebuildEnrichedCompilationAsync(ct);
    }

    async Task<Compilation?> RebuildEnrichedCompilationAsync(CancellationToken ct)
    {
        var compilation = _compilation!;
        var astSnapshot = _catalog.Catalog.CachedAst;

        foreach (var (filePath, sfc) in astSnapshot)
        {
            ct.ThrowIfCancellationRequested();

            if (sfc.ClassDeclaration is null)
                continue;

            try
            {
                var ns = sfc.Namespace?.Name ?? "";
                var typeName = sfc.ClassDeclaration.Name;
                var fullTypeName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";

                var target = new QuickMarkupTargetContext(
                    ns, typeName, fullTypeName, filePath, default, default);

                compilation = QuickMarkupCompilationEnricher.EnsureTypeSymbolInCompilation(target, sfc, compilation);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[QuickMarkup] Failed to enrich type in compilation: {ex.Message}");
            }
        }

        _enrichedCompilation = compilation;
        _enrichmentDirty = false;
        CompilationUpdated?.Invoke(this, EventArgs.Empty);
        return compilation;
    }

    public void InvalidateEnrichment()
    {
        _enrichmentDirty = true;
    }

    public event EventHandler? CompilationUpdated;

    public void Dispose()
    {
    }
}
