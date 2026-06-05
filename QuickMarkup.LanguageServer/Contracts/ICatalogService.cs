using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;

namespace QuickMarkup.LanguageServer.Contracts;

public interface ICatalogService
{
    QuickMarkupWorkspaceCatalog Catalog { get; }

    Task InitializeAsync(string workspaceRoot, Project project, IFileProvider fileProvider, IQmuiDocumentStore documentStore);

    void OnContentChanged(string filePath, string newContent);

    void OnFileDeleted(string filePath);
}
