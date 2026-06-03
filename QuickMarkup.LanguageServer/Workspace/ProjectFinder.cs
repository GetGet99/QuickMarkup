namespace QuickMarkup.LanguageServer.Workspace;

static class ProjectFinder
{
    public static string? FindCsproj(string? workspaceRoot)
    {
        if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return null;

        var csprojs = Directory.GetFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories);
        if (csprojs.Length == 0)
            return null;
        if (csprojs.Length == 1)
            return csprojs[0];

        // Prefer non-test projects
        var nonTest = csprojs.Where(c => !c.Contains(".Test", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (nonTest.Length == 1)
            return nonTest[0];
        if (nonTest.Length > 1)
            return nonTest[0];

        // Fallback: first result
        return csprojs[0];
    }
}
