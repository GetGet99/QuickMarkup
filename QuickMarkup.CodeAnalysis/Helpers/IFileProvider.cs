namespace QuickMarkup.CodeAnalysis.Helpers;

/// <summary>
/// Abstraction for file system operations.
/// This allows CodeAnalysis to be used in Roslyn Analyzers (where file I/O is banned)
/// by providing a stub implementation, while the Language Server provides real file access.
/// </summary>
public interface IFileProvider
{
    string ReadAllText(string path);
    string[] GetFiles(string directory, string pattern, bool recursive);
    bool DirectoryExists(string path);
}
