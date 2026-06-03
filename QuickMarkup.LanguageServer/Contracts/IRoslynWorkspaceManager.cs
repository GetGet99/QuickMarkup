using Microsoft.CodeAnalysis;

namespace QuickMarkup.LanguageServer.Contracts;

public interface IRoslynWorkspaceManager
{
    bool IsLoaded { get; }
    string? CurrentProjectPath { get; }
    Compilation? Compilation { get; }
    event Action? CompilationChanged;
    Task<bool> InitializeAsync(string workspaceRoot);
    Task<bool> TryLoadAsync(string projectPath);
    Task<bool> EnsureProjectForFileAsync(string qmuiFilePath);
}
