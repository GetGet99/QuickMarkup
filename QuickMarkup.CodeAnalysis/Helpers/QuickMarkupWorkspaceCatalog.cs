using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Get.Parser;
using Get.PLShared;
using Microsoft.CodeAnalysis;
using QuickMarkup.AST;

namespace QuickMarkup.CodeAnalysis.Helpers;

/// <summary>
/// Workspace-scoped catalog of QuickMarkup types (.qmui files and [QuickMarkup] attributes).
/// Supports full rebuild and incremental single-file updates.
/// Single owner of parsed ASTs — all LSP parsing goes through GetOrParse().
/// </summary>
public class QuickMarkupWorkspaceCatalog
{
    const string QmuiFilePattern = "*.qmui";
    const string QuickMarkupAttributeTypeName = "QuickMarkup.Infra.QuickMarkupAttribute";

    ImmutableArray<QuickMarkupTypeEntry> _entries = ImmutableArray<QuickMarkupTypeEntry>.Empty;
    readonly Dictionary<string, QuickMarkupTypeEntry> _entriesByFilePath = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _cachedContent = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, QuickMarkupSFC> _cachedAst = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, List<ErrorTerminalValue>> _cachedErrors = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, List<QuickMarkupTypeEntry>> _entriesByShortName = new();
    readonly object _lock = new();

    /// <summary>
    /// Gets all catalog entries.
    /// </summary>
    public ImmutableArray<QuickMarkupTypeEntry> Entries
    {
        get { lock (_lock) return _entries; }
    }

    /// <summary>
    /// Gets cached parsed ASTs keyed by file path (read-only snapshot).
    /// </summary>
    public IReadOnlyDictionary<string, QuickMarkupSFC> CachedAst
    {
        get { lock (_lock) return new Dictionary<string, QuickMarkupSFC>(_cachedAst); }
    }

    /// <summary>
    /// Tries to get a cached parsed AST for a file path.
    /// </summary>
    public bool TryGetCachedAst(string filePath, [NotNullWhen(true)] out QuickMarkupSFC? sfc)
    {
        lock (_lock)
        {
            return _cachedAst.TryGetValue(filePath, out sfc);
        }
    }

    /// <summary>
    /// Parses content and caches by content hash. Returns cached result if the same
    /// content was already parsed. Single entry point for all LSP parsing.
    /// </summary>
    public (QuickMarkupSFC? Sfc, List<ErrorTerminalValue> Errors) GetOrParse(string filePath, string content)
    {
        lock (_lock)
        {
            if (_cachedContent.TryGetValue(filePath, out var cachedContent) && string.Equals(cachedContent, content)
                && _cachedAst.TryGetValue(filePath, out var cached))
            {
                _cachedErrors.TryGetValue(filePath, out var errors);
                return (cached, errors ?? []);
            }
        }

        Console.Error.WriteLine($"[QuickMarkup] Parsing {filePath}");
        var (parsedSfc, parseErrors) = QuickMarkupProviderExtension.ParseWithErrors(content);

        if (parsedSfc is not null)
        {
            lock (_lock)
            {
                _cachedContent[filePath] = content;
                _cachedAst[filePath] = parsedSfc;
                _cachedErrors[filePath] = parseErrors;
            }
        }

        return (parsedSfc, parseErrors);
    }

    /// <summary>
    /// Rebuilds the catalog from scratch using the provided compilation and workspace root.
    /// </summary>
    public void Rebuild(Compilation compilation, string workspaceRoot, IFileProvider fileProvider)
    {
        lock (_lock)
        {
            _entriesByFilePath.Clear();
            _cachedAst.Clear();
            _cachedErrors.Clear();
            _cachedContent.Clear();
            _entriesByShortName.Clear();

            var entries = ImmutableArray.CreateBuilder<QuickMarkupTypeEntry>();

            if (!string.IsNullOrEmpty(workspaceRoot) && fileProvider.DirectoryExists(workspaceRoot))
            {
                var qmuiFiles = fileProvider.GetFiles(workspaceRoot, QmuiFilePattern, recursive: true);
                foreach (var file in qmuiFiles)
                {
                    try
                    {
                        var content = fileProvider.ReadAllText(file);
                        var (sfc, errors) = QuickMarkupProviderExtension.ParseWithErrors(content);
                        if (sfc?.ClassDeclaration != null)
                        {
                            AddEntry(entries, file, sfc);
                            _cachedContent[file] = content;
                            _cachedErrors[file] = errors;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[QuickMarkup] Skipping unparsable file {file}: {ex.Message}");
                    }
                }
            }

            var attributeSymbol = compilation.GetTypeByMetadataName(QuickMarkupAttributeTypeName);
            if (attributeSymbol != null)
            {
                foreach (var tree in compilation.SyntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(tree);
                    var classesWithAttribute = tree.GetRoot()
                        .DescendantNodes()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                        .Where(c => c.AttributeLists
                            .SelectMany(a => a.Attributes)
                            .Any(a => SymbolEqualityComparer.Default.Equals(
                                semanticModel.GetSymbolInfo(a).Symbol,
                                attributeSymbol)));

                    foreach (var classDecl in classesWithAttribute)
                    {
                        var typeSymbol = semanticModel.GetDeclaredSymbol(classDecl);
                        if (typeSymbol == null) continue;

                        var entry = new QuickMarkupTypeEntry(
                            FullTypeName: typeSymbol.ToDisplayString(),
                            ShortName: typeSymbol.Name,
                            Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                                ? "" : typeSymbol.ContainingNamespace.ToDisplayString(),
                            Usings: "",
                            Kind: QuickMarkupDefinitionKind.CSharpClass,
                            FilePath: tree.FilePath ?? "",
                            NameSpan: null);

                        entries.Add(entry);
                        IndexEntry(entry);
                    }
                }
            }

            _entries = entries.ToImmutable();
        }
    }

    /// <summary>
    /// Adds or updates a single .qmui entry without rebuilding the entire catalog.
    /// Delegates to GetOrParse for caching consistency.
    /// </summary>
    public void AddOrUpdateQmuiFile(string filePath, string content)
    {
        var (sfc, _) = GetOrParse(filePath, content);
        if (sfc?.ClassDeclaration == null)
            return;

        lock (_lock)
        {
            RemoveEntryInternal(filePath);

            var entries = _entries.ToBuilder();
            AddEntry(entries, filePath, sfc);
            _entries = entries.ToImmutable();
        }
    }

    /// <summary>
    /// Removes a .qmui entry from the catalog.
    /// </summary>
    public void RemoveQmuiFile(string filePath)
    {
        lock (_lock)
        {
            if (!RemoveEntryInternal(filePath))
                return;

            var entries = _entries.ToBuilder();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(entries[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                    break;
                }
            }
            _entries = entries.ToImmutable();
        }
    }

    void AddEntry(ImmutableArray<QuickMarkupTypeEntry>.Builder entries, string filePath, QuickMarkupSFC sfc)
    {
        var ns = sfc.Namespace?.Name ?? "";
        var typeName = sfc.ClassDeclaration!.Name;
        var fullTypeName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";

        var entry = new QuickMarkupTypeEntry(
            FullTypeName: fullTypeName,
            ShortName: typeName,
            Namespace: ns,
            Usings: string.Join(" ", sfc.Usings),
            Kind: QuickMarkupDefinitionKind.QmuiFile,
            FilePath: filePath,
            NameSpan: null);

        entries.Add(entry);
        _cachedAst[filePath] = sfc;
        IndexEntry(entry);
    }

    bool RemoveEntryInternal(string filePath)
    {
        _cachedAst.Remove(filePath);
        _cachedErrors.Remove(filePath);
        _cachedContent.Remove(filePath);

        if (_entriesByFilePath.TryGetValue(filePath, out var existing))
        {
            _entriesByFilePath.Remove(filePath);

            if (_entriesByShortName.TryGetValue(existing.ShortName, out var list))
            {
                list.Remove(existing);
                if (list.Count == 0)
                    _entriesByShortName.Remove(existing.ShortName);
            }
            return true;
        }
        return false;
    }

    void IndexEntry(QuickMarkupTypeEntry entry)
    {
        _entriesByFilePath[entry.FilePath] = entry;

        if (!_entriesByShortName.TryGetValue(entry.ShortName, out var list))
        {
            list = new List<QuickMarkupTypeEntry>();
            _entriesByShortName[entry.ShortName] = list;
        }
        list.Add(entry);
    }

    /// <summary>
    /// Tries to find a type entry by its full type name.
    /// </summary>
    public bool TryGetEntry(string fullTypeName, [NotNullWhen(true)] out QuickMarkupTypeEntry? entry)
    {
        lock (_lock)
        {
            foreach (var e in _entries)
            {
                if (e.FullTypeName == fullTypeName)
                {
                    entry = e;
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }

    /// <summary>
    /// Gets all entries that match a short type name (O(1) lookup).
    /// </summary>
    public IReadOnlyList<QuickMarkupTypeEntry> GetEntriesByShortName(string shortName)
    {
        lock (_lock)
        {
            return _entriesByShortName.TryGetValue(shortName, out var list)
                ? list.ToArray()
                : [];
        }
    }

    /// <summary>
    /// Finds the file path for a given full type name.
    /// </summary>
    public string? FindFilePathForTypeName(string fullTypeName)
    {
        lock (_lock)
        {
            foreach (var e in _entries)
            {
                if (e.FullTypeName == fullTypeName)
                    return e.FilePath;
            }
            return null;
        }
    }
}

/// <summary>
/// Defines the kind of QuickMarkup definition.
/// </summary>
public enum QuickMarkupDefinitionKind
{
    QmuiFile,
    [Obsolete("Builder no longer searches for C# file correctly. Please load definition manually")]
    CSharpClass
}

/// <summary>
/// Represents an entry in the QuickMarkup workspace catalog.
/// </summary>
public record QuickMarkupTypeEntry(
    string FullTypeName,
    string ShortName,
    string Namespace,
    string Usings,
    QuickMarkupDefinitionKind Kind,
    string FilePath,
    Position? NameSpan);
