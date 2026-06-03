using System.Xml.Linq;

namespace QuickMarkup.LanguageServer.Workspace;

static class SolutionParser
{
    public static List<string> ParseProjects(string solutionPath)
    {
        var ext = Path.GetExtension(solutionPath);
        return ext switch
        {
            ".slnx" => ParseSlnx(solutionPath),
            ".sln" => ParseSln(solutionPath),
            _ => [],
        };
    }

    static List<string> ParseSlnx(string slnxPath)
    {
        var dir = Path.GetDirectoryName(slnxPath)!;
        var doc = XDocument.Load(slnxPath);
        return doc.Descendants("Project")
            .Attributes("Path")
            .Select(a => Path.GetFullPath(Path.Combine(dir, a.Value)))
            .Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static List<string> ParseSln(string slnPath)
    {
        var dir = Path.GetDirectoryName(slnPath)!;
        var results = new List<string>();
        foreach (var line in File.ReadLines(slnPath))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Project(")) continue;

            var parts = trimmed.Split(',');
            if (parts.Length < 2) continue;

            var projPath = parts[1].Trim().Trim('"');
            if (!projPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) continue;

            var fullPath = Path.GetFullPath(Path.Combine(dir, projPath));
            if (!results.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                results.Add(fullPath);
        }
        return results;
    }
}
