using Get.EasyCSharp.GeneratorTools;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis.Binders;
using AST = AST.AST;

abstract record class QMDiagnostic(AST Node, string Message);

record class QMBinderError(AST Node, string Message) : QMDiagnostic(Node, Message)
{
    public override string ToString() => $"{Node.Start}-{Node.End} {Message}";
}
record class QMBinderWarning(AST Node, string Message) : QMDiagnostic(Node, Message)
{
    public override string ToString() => $"{Node.Start}-{Node.End} {Message}";
}
record class QMBinderTagMismatchedError(AST Node, string TagStart, string TagEnd)
    : QMBinderError(Node, $"Mismatched Ending tag: <{TagStart}>...</{TagEnd}>");
record class QMBinderTagUnexpectedError(AST Node, string TagName, string ExpectedTag)
    : QMBinderError(Node, $"Expecting <{ExpectedTag} />, but got <{TagName} />");
record class QMBinderTypeUnknownError(AST Node, string TypeName)
    : QMBinderError(Node, $"Unknown type \"{TypeName}\"");
record class QMBinderChildrenTooMany(AST Node, QMBinderTagInfo ParentTagInfo)
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