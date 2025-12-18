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
public interface IXMLNodeChild;
public record class QuickMarkupTemplate(ListAST<QuickMarkupXMLPropertiesKeyValue> Properties) : QuickMarkupXMLNode("template", Properties), ISFCTag
{
    public QuickMarkupTemplate(ListAST<QuickMarkupXMLPropertiesKeyValue> Properties, ListAST<IXMLNodeChild> Children) : this(Properties) {
        foreach (var child in Children)
        {
            this.Children.Add(child);
        }
    }
}
public record class QuickMarkupXMLNode(string Name, ListAST<QuickMarkupXMLPropertiesKeyValue> Properties) : AST, ISFCTag, IXMLNodeChild
{
    public ListAST<IXMLNodeChild> Children { get; private set; } = [];
    public void Add(ListAST<IXMLNodeChild> children)
    {
        Children = children;
    }

}
public interface ISFCTag;
public abstract record class QuickMarkupXMLPropertiesKeyValue(string Key) : AST, IXMLNodeChild;
public record class QuickMarkupXMLPropertiesKeyString(string Key, string Value) : QuickMarkupXMLPropertiesKeyValue(Key);
public record class QuickMarkupXMLPropertiesKeyForeign(string Key, string ForeignAsString) : QuickMarkupXMLPropertiesKeyValue(Key);
public record class QuickMarkupXMLPropertiesKeyBoolean(string Key, bool Value) : QuickMarkupXMLPropertiesKeyValue(Key);
public record class QuickMarkupXMLPropertiesKeyXML(string Key, QuickMarkupXMLNode Value) : QuickMarkupXMLPropertiesKeyValue(Key);
public record class QuickMarkupXMLPropertiesKeyInt32(string Key, int Value) : QuickMarkupXMLPropertiesKeyValue(Key);