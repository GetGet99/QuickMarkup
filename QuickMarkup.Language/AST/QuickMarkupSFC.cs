using QuickMarkup.Language.Symbols;

namespace QuickMarkup.AST;

public record class AST;

public record class QuickMarkupSFC(string Usings, ListAST<RefDeclaration> Refs) : AST
{
    public QuickMarkupScript? Scirpt { get; set; } = null;
    public QuickMarkupParsedTag? Template { get; set; } = null;
    public void Add(ISFCTag tag)
    {
        switch (tag)
        {
            case QuickMarkupScript scirpt:
                Scirpt = scirpt;
                break;
            case QuickMarkupParsedTag template:
                if ((template.TagStart as QuickMarkupConstructor)?.TagName is not "root")
                    throw new NotImplementedException();
                Template = template;
                break;
            default:
                throw new NotImplementedException();
        }
    }
}

public record class QuickMarkupParsedTag(
    ITagStart TagStart,
    ListAST<QuickMarkupInlineMember> InlineMembers,
    ListAST<IQMNodeChild>? Children,
    string? EndTagName,
    bool IsSelfClosing,
    string? Name = null
) : QuickMarkupValue, ISFCTag
{
    public bool HasMismatchedEndTag => !(IsSelfClosing || (EndTagName is not null && TagStart.DoesMatch(EndTagName)));
}

public record class QuickMarkupInlineMember : AST;
public record class QuickMarkupParsedProperty(
    string Key,
    ParsedPropertyOperator Operator,
    QuickMarkupValue? Value
) : QuickMarkupInlineMember
{
    public QuickMarkupParsedProperty(
        string Key,
        ParsedPropertyOperator Operator,
        bool Value
    ) : this(Key, Operator, new QuickMarkupBoolean(Value)) { }
}

public record class QuickMarkupCallback(string Code) : QuickMarkupInlineMember, IQMNodeChild;

public enum ParsedPropertyOperator
{
    None,          // Extension or "True" boolean
    Assign,        // =
    BindBack,      // =>
    BindTwoWay,    // <=>
    AddAssign      // +=
}

public record class QuickMarkupParsedForNode(
    TypeDeclaration? VarType,
    string VarName,
    QuickMarkupValue Iterable,
    ListAST<IQMNodeChild> Body
) : AST, IQMNodeChild;

// Not implemented yet
public record class QuickMarkupParsedIfNode(
    QuickMarkupValue Condition,
    ListAST<IQMNodeChild> BodyWhenTrue,
    ListAST<IQMNodeChild>? BodyWhenFalse
) : AST, IQMNodeChild;

public record class QuickMarkupConstructor(string TagName, ListAST<QuickMarkupValue> Parameters) : ITagStart
{
    public QuickMarkupConstructor(string TagName) : this(TagName, []) { }

    public bool DoesMatch(string EndTag)
    {
        return EndTag == TagName;
    }
}

public record class QuickMarkupPropertyTagStart(string TagName) : ITagStart
{
    public bool DoesMatch(string EndTag)
    {
        return EndTag == $".{TagName}";
    }
}


public record class QuickMarkupUsings(string RawScript) : AST, ISFCTag;
public record class QuickMarkupScript(string RawScript) : AST, ISFCTag;
public interface IQMNodeChild;
public interface ITagStart
{
    public string TagName { get; }
    bool DoesMatch(string EndTag);
}


public abstract record class QuickMarkupForNodeListExpression;
public record class QuickMarkupForNodeListRangeExpression(int Start, int End) : QuickMarkupForNodeListExpression;
public record class QuickMarkupForNodeListForeignExpression(string ForeignAsString) : QuickMarkupForNodeListExpression;
public record class TypeDeclaration(string Type, bool IsTypeNullable = false);
public record class RefDeclaration(TypeDeclaration Type, string Name, QuickMarkupValue? DefaultValue, bool IsPrivate, bool IsComputedDeclaration);
public interface ISFCTag;
public abstract record class QuickMarkupValue() : AST, IQMNodeChild;
public record class QuickMarkupRange(int Start, int End) : QuickMarkupValue();
public record class QuickMarkupInt32(int Value) : QuickMarkupValue();
public record class QuickMarkupDouble(double Value) : QuickMarkupValue();
public record class QuickMarkupBoolean(bool Value) : QuickMarkupValue();
public record class QuickMarkupString(string Value) : QuickMarkupValue();
public record class QuickMarkupDefault(bool IsExplicitlyNull) : QuickMarkupValue();
public record class QuickMarkupForeign(string Code) : QuickMarkupValue(), IQMNodeChild;
public record class QuickMarkupIdentifier(string Identifier) : QuickMarkupValue();
public record class QuickMarkupQMs(ListAST<IQMNodeChild> Value) : QuickMarkupValue();