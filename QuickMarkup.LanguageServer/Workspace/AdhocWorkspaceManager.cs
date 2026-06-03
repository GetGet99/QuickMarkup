using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.Workspace;

public class AdhocWorkspaceManager : IRoslynWorkspaceManager, IDisposable
{
    public bool IsLoaded { get; private set; }
    public string? CurrentProjectPath { get; private set; }
    public Compilation? Compilation { get; private set; }

    public Task<bool> InitializeAsync(string workspaceRoot)
    {
        return TryLoadAsync("");
    }

    public Task<bool> EnsureProjectForFileAsync(string qmuiFilePath)
    {
        return Task.FromResult(IsLoaded);
    }

    public async Task<bool> TryLoadAsync(string projectPath)
    {
        var dir = Path.GetDirectoryName(projectPath);
        if (dir is null || !Directory.Exists(dir))
        {
            IsLoaded = false;
            Compilation = null;
            return false;
        }

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("QuickMarkupProject", LanguageNames.CSharp);

        foreach (var csFile in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            try
            {
                var text = await File.ReadAllTextAsync(csFile);
                workspace.AddDocument(project.Id, csFile, SourceText.From(text));
            }
            catch
            {
            }
        }

        var corlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var runtime = MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location);
        var console = MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location);

        project = project.AddMetadataReferences([
            corlib, runtime, console
        ]);

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name is not "QuickMarkup.Infra" and not "QuickMarkup.Language")
                continue;

            if (!string.IsNullOrEmpty(asm.Location))
                project = project.AddMetadataReference(
                    MetadataReference.CreateFromFile(asm.Location));
        }

        Compilation = await project.GetCompilationAsync();
        IsLoaded = Compilation is not null;
        return IsLoaded;
    }

    public void WatchProjectChanges(string csprojPath) { }

    public void Dispose() { }
}
