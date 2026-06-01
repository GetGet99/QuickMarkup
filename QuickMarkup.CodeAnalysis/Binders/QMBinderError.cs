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

record class QMBinderMultipleComponentInterfacesError(AST Node, string TypeName)
    : QMBinderError(Node, $"Type \"{TypeName}\" implements multiple QuickMarkup component interfaces. A type may implement at most one of IQuickMarkupComponent<T> or IQuickMarkupFragmentComponent<T>.");

record class QMBinderAbstractComponentError(AST Node, string TypeName, string Reason)
    : QMBinderError(Node, $"Type \"{TypeName}\" cannot be a QuickMarkup component: {Reason}");

record class QMBinderComponentRootSingleNoChildrenError(AST Node, string TypeName)
    : QMBinderError(Node, $"Single-output component \"{TypeName}\" requires exactly one content child in <root>, but none were found.");

record class QMBinderFragmentComponentAsValueError(AST Node, string TypeName)
    : QMBinderError(Node, $"Fragment component \"{TypeName}\" cannot be used as a property value. Fragment components can only appear in additive child collection contexts.");

record class QMBinderResolvedComponentTypeError(AST Node, string TypeName)
    : QMBinderError(Node, $"Component interface type parameter could not be resolved for type \"{TypeName}\".");

record class QMBinderPropertyUnknownError(AST Node, string TypeName, string PropertyName)
    : QMBinderWarning(Node, $"'{TypeName}' does not have a definition for '{PropertyName}'")
{
    public override string ToString() => base.ToString();
}

record class QMBinderEnumMemberUnknownError(AST Node, string TypeName, string MemberName)
    : QMBinderWarning(Node, $"'{TypeName}' does not contain a definition for '{MemberName}'")
{
    public override string ToString() => base.ToString();
}