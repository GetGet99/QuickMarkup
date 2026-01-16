namespace QuickMarkup.AST;

public record class AST;

public record class QuickMarkupSFC : AST
{
    public QuickMarkupUsings Usings { get; set; } = null!;
    public QuickMarkupProps Props { get; set; } = null!;
    public QuickMarkupScript Scirpt { get; set; } = null!;
    public QuickMarkupTemplate Template { get; set; } = null!;
    public void Add(ISFCTag tag)
    {
        switch (tag)
        {
            case QuickMarkupUsings usings:
                Usings = usings;
                break;
            case QuickMarkupProps props:
                Props = props;
                break;
            case QuickMarkupScript scirpt:
                Scirpt = scirpt;
                break;
            case QuickMarkupTemplate template:
                Template = template;
                break;
        }
    }
}
public record class QuickMarkupUsings(string RawScript) : AST, ISFCTag;
public record class QuickMarkupProps(string RawScript) : AST, ISFCTag;
public record class QuickMarkupScript(string RawScript) : AST, ISFCTag;
public interface IQMNodeChild;
public record class QuickMarkupTemplate(ListAST<QuickMarkupQMPropertiesKeyValue> Properties) : QuickMarkupQMNode("template", Properties), ISFCTag
{
    public QuickMarkupTemplate(ListAST<QuickMarkupQMPropertiesKeyValue> Properties, ListAST<IQMNodeChild> Children) : this(Properties) {
        foreach (var child in Children)
        {
            this.Children.Add(child);
        }
    }
}
public record class QuickMarkupQMNode(string TypeName, ListAST<QuickMarkupQMPropertiesKeyValue> Properties, string? Name = null) : AST, ISFCTag, IQMNodeChild
{
    public ListAST<IQMNodeChild> Children { get; private set; } = [];
    public void Add(ListAST<IQMNodeChild> children)
    {
        Children = children;
    }
}
public abstract record class QuickMarkupForNodeListExpression;
public record class QuickMarkupForNodeListRangeExpression(int Start, int End) : QuickMarkupForNodeListExpression;
public record class QuickMarkupForNodeListForeignExpression(string ForeignAsString) : QuickMarkupForNodeListExpression;
public record class QuickMarkupForNode(string VarType, string TargetVariable, QuickMarkupForNodeListExpression ListExpression) : AST, IQMNodeChild
{
    public ListAST<IQMNodeChild> Children { get; private set; } = [];
    public bool NeedsReactivity { get; private set; } = false;
    public void Add(ListAST<IQMNodeChild> children)
    {
        Children = children;
    }
    public void Add(IQMNodeChild child)
    {
        Children.Add(child);
    }
}
public interface ISFCTag;
public abstract record class QuickMarkupQMPropertiesKeyValue(string? Key) : AST, IQMNodeChild;
public record class QuickMarkupQMPropertiesKeyString(string Key, string Value) : QuickMarkupQMPropertiesKeyValue(Key);
public record class QuickMarkupQMPropertiesKeyForeign(string? Key, string ForeignAsString, bool IsEventMode = false, bool IsBindBack = false) : QuickMarkupQMPropertiesKeyValue(Key);
public record class QuickMarkupQMPropertiesKeyEnum(string Key, string EnumMember) : QuickMarkupQMPropertiesKeyValue(Key);
public record class QuickMarkupQMPropertiesBoolOrExtension(string ExtensionMethod) : QuickMarkupQMPropertiesKeyValue(default(string));
public record class QuickMarkupQMPropertiesKeyBoolean(string Key, bool Value) : QuickMarkupQMPropertiesKeyValue(Key);
public record class QuickMarkupQMPropertiesKeyQM(string Key, QuickMarkupQMNode Value) : QuickMarkupQMPropertiesKeyValue(Key);
public record class QuickMarkupQMPropertiesKeyQMs(string Key, ListAST<IQMNodeChild> Value) : QuickMarkupQMPropertiesKeyValue(Key);
public record class QuickMarkupQMPropertiesKeyInt32(string Key, int Value) : QuickMarkupQMPropertiesKeyValue(Key);
public record class QuickMarkupQMPropertiesKeyDouble(string Key, double Value) : QuickMarkupQMPropertiesKeyValue(Key);