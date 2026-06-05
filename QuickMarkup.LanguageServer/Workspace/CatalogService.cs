using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Workspace;

sealed class CatalogService : ICatalogService
{
    readonly QuickMarkupWorkspaceCatalog _catalog = new();

    public QuickMarkupWorkspaceCatalog Catalog => _catalog;

    public async Task InitializeAsync(string workspaceRoot, Project project, IFileProvider fileProvider, IQmuiDocumentStore documentStore)
    {
        bool hasAdditionalDocs = false;

        foreach (var doc in project.AdditionalDocuments)
        {
            if (!doc.Name.EndsWith(".qmui", StringComparison.OrdinalIgnoreCase))
                continue;

            hasAdditionalDocs = true;
            try
            {
                var filePath = doc.FilePath ?? doc.Name;
                Console.Error.WriteLine($"[QuickMarkup] Parsing {filePath}");
                var content = await GetQmuiContentAsync(filePath, documentStore, fileProvider);
                _catalog.AddOrUpdateQmuiFile(filePath, content);
            }
            catch
            {
            }
        }

        if (!hasAdditionalDocs && !string.IsNullOrEmpty(workspaceRoot) && fileProvider.DirectoryExists(workspaceRoot))
        {
            var qmuiFiles = fileProvider.GetFiles(workspaceRoot, "*.qmui", recursive: true);
            foreach (var file in qmuiFiles)
            {
                try
                {
                    Console.Error.WriteLine($"[QuickMarkup] Parsing {file}");
                    var content = await GetQmuiContentAsync(file, documentStore, fileProvider);
                    _catalog.AddOrUpdateQmuiFile(file, content);
                }
                catch
                {
                }
            }
        }
    }

    static async Task<string> GetQmuiContentAsync(string filePath, IQmuiDocumentStore documentStore, IFileProvider fileProvider)
    {
        var inMemory = await documentStore.GetTextAsync(filePath);
        return inMemory ?? fileProvider.ReadAllText(filePath);
    }

    public void OnContentChanged(string filePath, string newContent)
    {
        Console.Error.WriteLine($"[QuickMarkup] Parsing {filePath}");
        _catalog.AddOrUpdateQmuiFile(filePath, newContent);
    }

    public void OnFileDeleted(string filePath)
    {
        _catalog.RemoveQmuiFile(filePath);
    }
}
