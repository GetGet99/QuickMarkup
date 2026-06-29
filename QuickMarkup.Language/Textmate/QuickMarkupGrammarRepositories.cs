namespace QuickMarkup.Textmate;

/// <summary>
/// TextMate repository keys and top-level include order for QuickMarkup.
/// </summary>
public static class QuickMarkupGrammarRepositories
{
    public const string Comments = "comments";
    public const string Strings = "strings";
    public const string Tags = "tags";
    public const string Main = "main";

    /// <summary>VS Code <c>embeddedLanguages</c> meta scope → language id.</summary>
    public const string EmbeddedCSharpMetaScope = "meta.embedded.csharp";

    /// <summary>TextMate include target for C# embedded regions.</summary>
    public const string CSharpGrammarScope = "source.cs";

    public static readonly string[] IncludeOrder = [Comments, Strings, Tags, Main];
}
