using Get.EasyCSharp.GeneratorTools;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis.Binders;
using AST = AST.AST;

public abstract record class QMDiagnostic(AST Node, string Message);

public record class QMBinderError(AST Node, string Message) : QMDiagnostic(Node, Message)
{
    public override string ToString() => $"{Node.Start}-{Node.End} {Message}";
}
public record class QMBinderWarning(AST Node, string Message) : QMDiagnostic(Node, Message)
{
    public override string ToString() => $"{Node.Start}-{Node.End} {Message}";
}
public record class QMBinderTagMismatchedError(AST Node, string TagStart, string TagEnd)
    : QMBinderError(Node, $"Mismatched Ending tag: <{TagStart}>...</{TagEnd}>");
public record class QMBinderTagUnexpectedError(AST Node, string TagName, string ExpectedTag)
    : QMBinderError(Node, $"Expecting <{ExpectedTag} />, but got <{TagName} />");
public record class QMBinderTypeUnknownError(AST Node, string TypeName)
    : QMBinderError(Node, $"Unknown type \"{TypeName}\"");
public record class QMBinderChildrenTooMany(AST Node, QMBinderTagInfo ParentTagInfo)
    : QMBinderError(Node, $"Too many children were provided. <{ParentTagInfo.TagType?.FullNameWithoutAnnotation() ?? ParentTagInfo.TagName}> expects {ExpectingText(ParentTagInfo.ChildrenMode)}.")
{
    public string Expecting => ExpectingText(ParentTagInfo.ChildrenMode);
    private static string ExpectingText(ChildrenModes mode)
        => mode switch
        {
            ChildrenModes.None => "no child elements",
            ChildrenModes.Assignment => "a single child",
            ChildrenModes.Add => "any number of children",
            _ => "unknown number of children"
        };
}

public record class QMBinderMultipleComponentInterfacesError(AST Node, string TypeName)
    : QMBinderError(Node, $"Type \"{TypeName}\" implements multiple QuickMarkup component interfaces. A type may implement at most one of IQuickMarkupComponent<T> or IQuickMarkupFragmentComponent<T>.");

public record class QMBinderAbstractComponentError(AST Node, string TypeName, string Reason)
    : QMBinderError(Node, $"Type \"{TypeName}\" cannot be a QuickMarkup component: {Reason}");

public record class QMBinderComponentRootSingleNoChildrenError(AST Node, string TypeName)
    : QMBinderError(Node, $"Single-output component \"{TypeName}\" requires exactly one content child in <root>, but none were found.");

public record class QMBinderFragmentComponentAsValueError(AST Node, string TypeName)
    : QMBinderError(Node, $"Fragment component \"{TypeName}\" cannot be used as a property value. Fragment components can only appear in additive child collection contexts.");

public record class QMBinderResolvedComponentTypeError(AST Node, string TypeName)
    : QMBinderError(Node, $"Component interface type parameter could not be resolved for type \"{TypeName}\".");

public record class QMBinderPropertyUnknownError(AST Node, string TypeName, string PropertyName, string[]? Suggestions = null)
    : QMBinderWarning(Node, BuildMessage(TypeName, PropertyName, Suggestions))
{
    static string BuildMessage(string typeName, string propertyName, string[]? suggestions)
    {
        var msg = $"'{typeName}' does not have a definition for '{propertyName}'";
        return QMDiagnosticSuggestion.AppendSuggestions(msg, suggestions);
    }
    public override string ToString() => base.ToString();
}

public record class QMBinderRequiredPropertyMissingError(AST Node, string TypeName, string PropertyName)
    : QMBinderError(Node, $"Required property '{PropertyName}' is not set on '{TypeName}'.");

public record class QMBinderTypeMismatchError(AST Node, string PropertyTypeName, string ValueTypeName)
    : QMBinderError(Node, BuildTypeMismatchMessage(PropertyTypeName, ValueTypeName))
{
    static string BuildTypeMismatchMessage(string propertyTypeName, string valueTypeName)
        => $"Cannot assign value of type '{valueTypeName}' to property of type '{propertyTypeName}'.";
    public override string ToString() => base.ToString();
}

public record class QMBinderEnumMemberUnknownError(AST Node, string TypeName, string MemberName, string[]? Suggestions = null)
    : QMBinderWarning(Node, BuildMessage(TypeName, MemberName, Suggestions))
{
    static string BuildMessage(string typeName, string memberName, string[]? suggestions)
    {
        var msg = $"'{typeName}' does not contain a definition for '{memberName}'";
        return QMDiagnosticSuggestion.AppendSuggestions(msg, suggestions);
    }
    public override string ToString() => base.ToString();
}

static class QMDiagnosticSuggestion
{
    public static string AppendSuggestions(string message, string[]? suggestions)
    {
        if (suggestions is not { Length: > 0 })
            return message;

        return suggestions.Length switch
        {
            1 => $"{message}. Did you mean '{suggestions[0]}'?",
            _ => $"{message}. Did you mean {FormatMultiple(suggestions)}?"
        };
    }
    public static string FormatSuggestions(string[]? suggestions)
    {
        if (suggestions is not { Length: > 0 })
            return "";

        return suggestions.Length switch
        {
            1 => $"did you mean '{suggestions[0]}'?",
            _ => $"did you mean {FormatMultiple(suggestions)}?"
        };
    }

    static string FormatMultiple(string[] suggestions)
        => suggestions.Length == 2
            ? $"'{suggestions[0]}' or '{suggestions[1]}'"
            : $"'{suggestions[0]}', '{suggestions[1]}', or '{suggestions[2]}'";
}