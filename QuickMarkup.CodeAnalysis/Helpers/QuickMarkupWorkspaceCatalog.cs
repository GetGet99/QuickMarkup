using System.Collections.Immutable;
using Get.PLShared;
using Microsoft.CodeAnalysis;

namespace QuickMarkup.CodeAnalysis.Helpers;

/// <summary>
/// Workspace-scoped catalog of QuickMarkup types (.qmui files and [QuickMarkup] attributes).
/// Built once per workspace and invalidated on file changes.
/// </summary>
public class QuickMarkupWorkspaceCatalog
{
    private ImmutableArray<QuickMarkupTypeEntry> _entries = ImmutableArray<QuickMarkupTypeEntry>.Empty;
    private readonly object _lock = new();

    /// <summary>
    /// Gets all catalog entries.
    /// </summary>
    public ImmutableArray<QuickMarkupTypeEntry> Entries => _entries;

    /// <summary>
    /// Rebuilds the catalog from scratch using the provided compilation and workspace root.
    /// </summary>
    /// <param name="compilation">The Roslyn compilation to scan for [QuickMarkup] attributes.</param>
    /// <param name="workspaceRoot">Root directory to scan for .qmui files.</param>
    /// <param name="fileProvider">File system abstraction for reading files (required for Roslyn Analyzer compatibility).</param>
    public void Rebuild(Compilation compilation, string workspaceRoot, IFileProvider fileProvider)
    {
        lock (_lock)
        {
            var entries = ImmutableArray.CreateBuilder<QuickMarkupTypeEntry>();

            // 1. Scan .qmui files under workspace
            if (!string.IsNullOrEmpty(workspaceRoot) && fileProvider.DirectoryExists(workspaceRoot))
            {
                var qmuiFiles = fileProvider.GetFiles(workspaceRoot, "*.qmui", recursive: true);
                foreach (var file in qmuiFiles)
                {
                    try
                    {
                        var content = fileProvider.ReadAllText(file);
                        var sfc = QuickMarkupProviderExtension.Parse(content);
                        if (sfc != null && sfc.ClassDeclaration != null)
                        {
                            var entry = new QuickMarkupTypeEntry(
                                FullTypeName: GetFullTypeName(sfc.Namespace?.Name ?? "", sfc.ClassDeclaration.Name),
                                ShortName: sfc.ClassDeclaration.Name,
                                Namespace: sfc.Namespace?.Name ?? "",
                                Usings: string.Join(" ", sfc.Usings),
                                Kind: QuickMarkupDefinitionKind.QmuiFile,
                                FilePath: file,
                                NameSpan: null
                            );
                            entries.Add(entry);
                        }
                    }
                    catch (Exception)
                    {
                        // Skip unparsable files
                    }
                }
            }

            // 2. Scan [QuickMarkup] attributes in Roslyn syntax trees
            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var quickMarkupAttributeSymbol = compilation.GetTypeByMetadataName("QuickMarkup.Infra.QuickMarkupAttribute");
                if (quickMarkupAttributeSymbol == null) continue;

                var classesWithAttribute = tree.GetRoot()
                    .DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                    .Where(c => c.AttributeLists
                        .SelectMany(a => a.Attributes)
                        .Any(a => SymbolEqualityComparer.Default.Equals(
                            semanticModel.GetSymbolInfo(a).Symbol,
                            quickMarkupAttributeSymbol)));

                foreach (var classDecl in classesWithAttribute)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(classDecl);
                    if (typeSymbol == null) continue;

                    string filePath = tree.FilePath ?? "";
                    Position? nameSpan = null;

                    var identifierToken = classDecl.Identifier;
                    if (identifierToken != null)
                    {
                        // Simplified approach - leave span null for now
                    }

                    var entry = new QuickMarkupTypeEntry(
                        FullTypeName: typeSymbol.ToDisplayString(),
                        ShortName: typeSymbol.Name,
                        Namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace ? "" : typeSymbol.ContainingNamespace.ToDisplayString(),
                        Usings: "",
                        Kind: QuickMarkupDefinitionKind.CSharpClass,
                        FilePath: filePath,
                        NameSpan: nameSpan
                    );
                    entries.Add(entry);
                }
            }

            _entries = entries.ToImmutable();
        }
    }

    /// <summary>
    /// Tries to find a type entry by its full type name.
    /// </summary>
    public bool TryGetEntry(string fullTypeName, out QuickMarkupTypeEntry entry)
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
    /// Gets all entries that match a short type name (useful for name resolution).
    /// </summary>
    public IEnumerable<QuickMarkupTypeEntry> GetEntriesByShortName(string shortName)
    {
        lock (_lock)
        {
            return _entries.Where(e => e.ShortName == shortName);
        }
    }

    private static string GetFullTypeName(string @namespace, string typeName)
    {
        if (string.IsNullOrEmpty(@namespace))
            return typeName;
        return $"{@namespace}.{typeName}";
    }
}

/// <summary>
/// Defines the kind of QuickMarkup definition.
/// </summary>
public enum QuickMarkupDefinitionKind
{
    QmuiFile,
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
