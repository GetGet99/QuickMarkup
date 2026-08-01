using Get.Parser;
using Get.PLShared;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.AST;

public record class AST : ISpanSetter
{
    public Position Start { get; set; }
    public Position End { get; set; }
}
public record class QuickMarkupSFC(
    string Usings,
    ListAST<RefDeclaration> Refs,
    PositionedIdentifier? Namespace = null,
    ClassDeclaration? ClassDeclaration = null
) : AST
{
    public QuickMarkupScript? Scirpt { get; set; } = null;
    public ListAST<QuickMarkupParsedTag> MarkupTags { get; set; } = new();
    public QuickMarkupParsedTag? Template => MarkupTags.Count == 0 ? null : MarkupTags[^1];
    public void Add(ISFCTag tag)
    {
        switch (tag)
        {
            case QuickMarkupScript scirpt:
                Scirpt = scirpt;
                break;
            case QuickMarkupParsedTag parsedTag:
                MarkupTags.Add(parsedTag);
                break;
            default:
                throw new NotImplementedException();
        }
    }
}

public record class QuickMarkupParsedTagHeader(
    ITagStart TagStart,
    ListAST<QuickMarkupInlineMember> InlineMembers
) : QuickMarkupValue, ISFCTag;

public record class QuickMarkupParsedTag(
    QuickMarkupParsedTagHeader Header,
    ListAST<IQMNodeChild>? Children,
    PositionedIdentifier? EndTagName,
    bool IsSelfClosing
) : QuickMarkupValue, ISFCTag
{
    public PositionedIdentifier? Name { get; set; }
    public bool IsRef { get; set; } = false;
    public bool HasMismatchedEndTag => !(IsSelfClosing || (EndTagName is not null && Header.TagStart.DoesMatch(EndTagName.Name)));
    public ITagStart TagStart => Header.TagStart;
    public ListAST<QuickMarkupInlineMember> InlineMembers => Header.InlineMembers;
}

public record class PositionedIdentifier(string Name) : AST
{
    public override string ToString() => Name;
}
public record class QuickMarkupInlineMember : AST;
public record class QuickMarkupParsedProperty(
    string Key,
    ParsedPropertyOperator Operator,
    QuickMarkupValue? Value,
    bool IsAttachedPropertyKey = false,
    bool IsKeyForeign = false
) : QuickMarkupInlineMember
{
    public QuickMarkupParsedProperty(
        string Key,
        ParsedPropertyOperator Operator,
        bool Value,
        bool IsKeyForeign = false
    ) : this(Key, Operator, new QuickMarkupBoolean(Value), IsKeyForeign: IsKeyForeign) { }
}

public record class QuickMarkupCallback(string Code) : QuickMarkupInlineMember, IQMNodeChild;

public enum ParsedPropertyOperator
{
    None,          // Extension or "True" boolean
    Assign,        // =
    BindBack,      // =>
    BindBackDelegate, // +=>
    BindTwoWay,    // <=>
    AddAssign      // +=
}

public record class QuickMarkupParsedForNode(
    TypeDeclaration? VarType,
    string VarName,
    QuickMarkupValue Iterable,
    IQMNodeChild Body,
    string? IndexVarName = null,
    QuickMarkupValue? Key = null
) : AST, IQMNodeChild;

public record class QuickMarkupParsedTemplateNode(
    TypeDeclaration? VarType,
    string VarName,
    IQMNodeChild Body
) : QuickMarkupValue;

public enum AwaitBranchKind { With, Catch, Then }

public record class QuickMarkupParsedAwaitBranch(
    AwaitBranchKind Kind,
    TypeDeclaration? VarType,
    string? VarName,
    IQMNodeChild Body
) : AST;

public record class QuickMarkupParsedAwaitNode(
    QuickMarkupValue AsyncExpression,
    ListAST<QuickMarkupParsedAwaitBranch> Branches
) : AST, IQMNodeChild;

public record class QuickMarkupParsedIfNode(
    QuickMarkupValue Condition,
    IQMNodeChild BodyWhenTrue,
    IQMNodeChild? BodyWhenFalse
) : AST, IQMNodeChild;

public record class QuickMarkupParsedFragmentNode(
    ListAST<IQMNodeChild> Children
) : AST, IQMNodeChild;

public record class QuickMarkupConstructor(PositionedIdentifier TagIdentifier, ListAST<QuickMarkupValue> Parameters) : AST, ITagStart
{
    public QuickMarkupConstructor(PositionedIdentifier TagIdentifier) : this(TagIdentifier, []) { }
    public string TagName => TagIdentifier.Name;
    public AST TagIdentifierAST => TagIdentifier;
    public bool DoesMatch(string EndTag)
    {
        return EndTag == TagIdentifier.Name;
    }
}

public record class QuickMarkupPropertyTagStart(string TagName) : AST, ITagStart
{
    public AST TagIdentifierAST => this;
    public bool DoesMatch(string EndTag)
    {
        return EndTag == $".{TagName}";
    }
}

public record class QuickMarkupAttachedPropertyTagStart(string TypeName, string PropertyName) : AST, ITagStart
{
    public AST TagIdentifierAST => this;
    public string TagName => $"{TypeName}.{PropertyName}";
    public bool DoesMatch(string EndTag)
    {
        return EndTag == $"{TypeName}.{PropertyName}";
    }
}

public record class QuickMarkupUsings(string RawScript) : AST, ISFCTag;
public record class QuickMarkupScript(string RawScript, bool IsAsync = false) : AST, ISFCTag;
public interface IQMNodeChild;
public interface ITagStart
{
    public AST TagIdentifierAST { get; }
    public string TagName { get; }
    bool DoesMatch(string EndTag);
}


public abstract record class QuickMarkupForNodeListExpression : AST;
public record class QuickMarkupForNodeListRangeExpression(int RangeStart, int RangeEnd) : QuickMarkupForNodeListExpression;
public record class QuickMarkupForNodeListForeignExpression(string ForeignAsString) : QuickMarkupForNodeListExpression;
public record class TypeDeclaration(string Type, bool IsTypeNullable = false);
public record class QMAttributeNamedArgument(PositionedIdentifier Name, QuickMarkupValue Value);
public record class QMCompileTimeAttributeArguments(
    ListAST<QuickMarkupValue> Positionals,
    ListAST<QMAttributeNamedArgument> Named) : AST
{
    public QMCompileTimeAttributeArguments() : this(new(), new()) {}
    public QMCompileTimeAttributeArguments(ListAST<QMAttributeNamedArgument> Named) : this(new(), Named) {}
    public void Add(QuickMarkupValue value) => Positionals.Add(value);
}
public record class QMAttribute(
    PositionedIdentifier? TargetSpecifier,
    PositionedIdentifier AttributeName,
    QMCompileTimeAttributeArguments Arguments)
{
    public QMAttribute(PositionedIdentifier AttributeName, QMCompileTimeAttributeArguments Arguments)
        : this(null, AttributeName, Arguments) { }
}
public record class RefDeclarationName(
    PositionedIdentifier Name,
    PositionedIdentifier? AsAllias = null
);
public record class RefDeclaration(
    ListAST<QMAttribute> Attributes,
    Accessibility Accessibility,
    RefDeclarationKind Kind,
    bool IsStatic,
    bool IsRequired,
    TypeDeclaration Type,
    RefDeclarationName Name,
    RefDeclarationDefaultValue? DefaultValue
) : AST;

public record class RefDeclarationDefaultValue(
    QuickMarkupValue? Value,
    DefaultValueKind Kind
) : AST;

public enum DefaultValueKind
{
    Assignment,
    Computed,
    AsyncComputed
}
public interface ISFCTag;
public abstract record class QuickMarkupValue() : AST, IQMNodeChild;

public enum ClassKind
{
    Subclass,
    Component,
    FragmentComponent
}

public enum Accessibility
{
    Default,
    Public,
    Private,
    Protected
}

public record ClassDeclaration(
    string Name,
    ClassKind Kind,
    string? BaseTypes
);
public record class QuickMarkupRange(int RangeStart, int RangeEnd) : QuickMarkupValue();
public record class QuickMarkupInt32(int Value) : QuickMarkupValue();
public record class QuickMarkupDouble(double Value) : QuickMarkupValue();
public record class QuickMarkupBoolean(bool Value) : QuickMarkupValue();
public record class QuickMarkupString(string Value) : QuickMarkupValue();
public record class QuickMarkupDefault(bool IsExplicitlyNull) : QuickMarkupValue();
public record class QuickMarkupForeign(string Code) : QuickMarkupValue(), IQMNodeChild;
public record class QuickMarkupIdentifier(string Identifier) : QuickMarkupValue();
public record class QuickMarkupValueList(ListAST<IQMNodeChild> Value) : QuickMarkupValue();
