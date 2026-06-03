namespace QuickMarkup.LanguageServer.Workspace;

static class ProjectFinder
{
    public static string? FindCsproj(string? workspaceRoot)
    {
        if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return null;

        var csprojs = Directory.GetFiles(workspaceRoot, "*.csproj", SearchOption.TopDirectoryOnly);
        return csprojs.Length == 1 ? csprojs[0] : null;
    }
}
