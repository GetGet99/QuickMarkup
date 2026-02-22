using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.SourceGen.Analyzers;

using AST = AST.AST;
record class QMBinderError(AST Node);
record class QMBinderTagMismatchedError(AST Node, string TagStart, string TagEnd) : QMBinderError(Node)
{
    public override string ToString() => $"{Node.Start}-{Node.End} Mismatched Ending tag: <{TagStart}>...</{TagEnd}>";
}
record class QMBinderTagUnexpectedError(AST Node, string TagName, string ExpectedTag) : QMBinderError(Node)
{
    public override string ToString() => $"{Node.Start}-{Node.End} Expecting <{ExpectedTag} />, but got <{TagName} />";
}
record class QMBinderTypeUnknownError(AST Node, string TypeName) : QMBinderError(Node)
{
    public override string ToString() => $"{Node.Start}-{Node.End} Unknown type \"{TypeName}\"";
}
record class QMBinderChildrenTooMany(AST Node, QMBinderTagInfo ParentTagInfo) : QMBinderError(Node)
{
    public string Expecting => ParentTagInfo.ChildrenMode switch
    {
        ChildrenModes.None => "no child elements",
        ChildrenModes.Assignment => "a single child",
        ChildrenModes.Add => "any number of children",
        _ => "unknown number of children"
    };
    public override string ToString() => $"{Node.Start}:{Node.End} Too many children were provided. <{ParentTagInfo.TagType?.FullNameWithoutAnnotation() ?? ParentTagInfo.TagName}> expects {Expecting}.";
}

partial class QMSourceGenBinders
{
    public List<QMBinderError> Errors { get; } = [];
    void Error(QMBinderError error)
    {
        Errors.Add(error);
        if (failFast)
            throw new InvalidOperationException(error.ToString());
    }
    void ErrorTagMismatched(string tagStartName, PositionedIdentifier endTag)
        => Error(new QMBinderTagMismatchedError(endTag, tagStartName, endTag.Name));
    void ErrorTagUnexpected(ITagStart tagStart, string expectedTag)
        => Error(new QMBinderTagMismatchedError((AST)tagStart, tagStart.TagName, expectedTag));
    void ErrorUnknownType(ITagStart tagStart)
        => Error(new QMBinderTypeUnknownError((AST)tagStart, tagStart.TagName));
    void ErrorUnknownType(PositionedIdentifier identifier)
        => Error(new QMBinderTypeUnknownError(identifier, identifier.Name));
    void ErrorChildrenTooMany(AST node, QMBinderTagInfo parentTagInfo)
        => Error(new QMBinderChildrenTooMany(node, parentTagInfo));
}
