namespace QuickMarkup.LanguageServer.Workspace;

static class ProjectFinder
{
    public static string? FindDefaultProject(string? workspaceRoot)
    {
        if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return null;

        var csprojs = Directory.GetFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories);
        if (csprojs.Length == 0)
            return null;
        if (csprojs.Length == 1)
            return csprojs[0];

        var nonTest = csprojs.Where(c => !c.Contains(".Test", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (nonTest.Length == 1)
            return nonTest[0];
        if (nonTest.Length > 1)
            return nonTest[0];

        return csprojs[0];
    }

    public static List<string> FindSolutionProjects(string? workspaceRoot)
    {
        if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return [];

        var slnx = Directory.GetFiles(workspaceRoot, "*.slnx", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (slnx is not null)
            return SolutionParser.ParseProjects(slnx);

        var sln = Directory.GetFiles(workspaceRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (sln is not null)
            return SolutionParser.ParseProjects(sln);

        return [];
    }

    public static string? FindProjectForFile(string qmuiFilePath, string workspaceRoot, List<string>? solutionProjects = null)
    {
        if (string.IsNullOrEmpty(qmuiFilePath) || string.IsNullOrEmpty(workspaceRoot))
            return null;

        var dir = Path.GetDirectoryName(qmuiFilePath);
        while (dir is not null && dir.Length >= workspaceRoot.Length)
        {
            var csprojs = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
            if (csprojs.Length > 0)
                return csprojs[0];

            var parent = Path.GetDirectoryName(dir);
            if (string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase)) break;
            dir = parent;
        }

        if (solutionProjects is not null && solutionProjects.Count > 0)
        {
            var nonTest = solutionProjects.Where(p => !p.Contains(".Test", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (nonTest.Length > 0)
                return nonTest[0];
            return solutionProjects[0];
        }

        return FindDefaultProject(workspaceRoot);
    }
}
