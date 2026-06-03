using QuickMarkup.CodeAnalysis.Helpers;

namespace QuickMarkup.LanguageServer;

/// <summary>
/// Real file system implementation of <see cref="IFileProvider"/> for use in the Language Server.
/// </summary>
public class FileSystemProvider : IFileProvider
{
    public string ReadAllText(string path) => File.ReadAllText(path);

    public string[] GetFiles(string directory, string pattern, bool recursive)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(directory, pattern, searchOption);
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);
}
