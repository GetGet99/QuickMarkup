using Microsoft.CodeAnalysis;

namespace QuickMarkup.LanguageServer.Contracts;

interface IRoslynWorkspaceManager
{
    bool IsLoaded { get; }
    Compilation? Compilation { get; }
    event Action? CompilationChanged;
    Task<bool> TryLoadAsync(string projectPath);
    void WatchProjectChanges(string csprojPath);
}
