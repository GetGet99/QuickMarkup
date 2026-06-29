using Get.LangSupport;

namespace QuickMarkup.Textmate;

public enum TagMatchKind
{
    Open,
    Close,
    Setup
}

public class QuickMarkupCommentScopeAttribute : TextmateCommentScopeAttribute
{
    public QuickMarkupCommentScopeAttribute() => RepositoryKey = QuickMarkupGrammarRepositories.Comments;
}

public class QuickMarkupStringQuotedScopeAttribute : TextmateStringQuotedScopeAttribute
{
    public QuickMarkupStringQuotedScopeAttribute(StringQuotedType type) : base(type) =>
        RepositoryKey = QuickMarkupGrammarRepositories.Strings;
}

/// <summary>
/// Backtick or <c>/- -/</c> foreign C# regions.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class QuickMarkupEmbeddedCSharpScopeAttribute : TextmateScopeAttribute
{
    public QuickMarkupEmbeddedCSharpScopeAttribute(string begin, string end)
        : base(QuickMarkupGrammarRepositories.EmbeddedCSharpMetaScope)
    {
        Begin = begin;
        End = end;
        EmbeddedLanguage = "csharp";
        EmbeddedGrammarScope = QuickMarkupGrammarRepositories.CSharpGrammarScope;
        RepositoryKey = QuickMarkupGrammarRepositories.Strings;
    }
}

/// <summary>
/// QuickMarkup tag begin/end rules; tag names are scoped as <c>entity.name.type.class</c>.
/// </summary>
public class TextmateTagScopeAttribute : TextmateScopeAttribute
{
    private const string TagNameScope = "entity.name.type.class";
    private const string SetupTagNameScope = "keyword.declaration";
    private const string TagBeginPunct = "punctuation.definition.tag.begin";
    private const string TagEndPunct = "punctuation.definition.tag.end";

    public TextmateTagScopeAttribute(TagMatchKind kind) : base("meta.tag.quickmarkup")
    {
        RepositoryKey = kind == TagMatchKind.Setup
            ? QuickMarkupGrammarRepositories.Strings
            : QuickMarkupGrammarRepositories.Tags;

        switch (kind)
        {
            case TagMatchKind.Open:
                Begin = @"(<)(?![/!?])([a-zA-Z_][a-zA-Z0-9_]*)";
                BeginCaptures = new Dictionary<string, string>
                {
                    ["1"] = TagBeginPunct,
                    ["2"] = TagNameScope
                };
                End = @"(/>|>)";
                EndCaptures = new Dictionary<string, string> { ["1"] = TagEndPunct };
                InsideIncludes =
                [
                    $"#{QuickMarkupGrammarRepositories.Strings}",
                    $"#{QuickMarkupGrammarRepositories.Main}"
                ];
                break;
            case TagMatchKind.Close:
                Begin = @"(</)([a-zA-Z_][a-zA-Z0-9_]*)";
                BeginCaptures = new Dictionary<string, string>
                {
                    ["1"] = TagBeginPunct,
                    ["2"] = TagNameScope
                };
                End = "(>)";
                EndCaptures = new Dictionary<string, string> { ["1"] = TagEndPunct };
                break;
            case TagMatchKind.Setup:
                Begin = @"(<)(setup)";
                BeginCaptures = new Dictionary<string, string>
                {
                    ["1"] = TagBeginPunct,
                    ["2"] = SetupTagNameScope
                };
                End = @"(</)(setup)(>)";
                EndCaptures = new Dictionary<string, string>
                {
                    ["1"] = TagBeginPunct,
                    ["2"] = SetupTagNameScope,
                    ["3"] = TagEndPunct
                };
                EmbeddedLanguage = "csharp";
                EmbeddedGrammarScope = QuickMarkupGrammarRepositories.CSharpGrammarScope;
                break;
        }
    }
}
