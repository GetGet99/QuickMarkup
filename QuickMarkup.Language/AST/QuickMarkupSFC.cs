namespace QuickMarkup.AST;

public record class AST;

public record class QuickMarkupSFC(string Usings, ListAST<RefDeclaration> Refs) : AST
{
    public QuickMarkupScript? Scirpt { get; set; } = null;
    public QuickMarkupTemplate? Template { get; set; } = null;
    public void Add(ISFCTag tag)
    {
        switch (tag)
        {
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
public record class QuickMarkupTemplate(ListAST<QuickMarkupQMProperty> Properties) : QuickMarkupQMNode(new("template"), Properties), ISFCTag
{
    public QuickMarkupTemplate(ListAST<QuickMarkupQMProperty> Properties, ListAST<IQMNodeChild> Children) : this(Properties) {
        foreach (var child in Children)
        {
            this.Children.Add(child);
        }
    }
}
public record class QuickMarkupConstructor(string TypeName, ListAST<QuickMarkupValue> Parameters)
{
    public QuickMarkupConstructor(string TypeName) : this(TypeName, []) { }
}
public record class QuickMarkupQMNode(QuickMarkupConstructor Constructor, ListAST<QuickMarkupQMProperty> Properties, string? AssignToVarName = null) : AST, ISFCTag, IQMNodeChild
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
public record class TypeDeclaration(string Type, bool IsTypeNullable = false);
public record class RefDeclaration(TypeDeclaration Type, string Name, QuickMarkupValue? DefaultValue, bool IsPrivate, bool IsComputedDeclaration);
public interface ISFCTag;
public abstract record class QuickMarkupValue() : AST, IQMNodeChild;
public record class QuickMarkupDefault(bool IsExplicitlyNull) : QuickMarkupValue();
public record class QuickMarkupString(string Value) : QuickMarkupValue();
public record class QuickMarkupForeign(string Code) : QuickMarkupValue(), IQMNodeChild;
public record class QuickMarkupEnum(string EnumMember) : QuickMarkupValue();
public record class QuickMarkupBoolean(bool Value) : QuickMarkupValue();
public record class QuickMarkupQM(QuickMarkupQMNode Value) : QuickMarkupValue();
public record class QuickMarkupQMs(ListAST<IQMNodeChild> Value) : QuickMarkupValue();
public record class QuickMarkupInt32(int Value) : QuickMarkupValue();
public record class QuickMarkupDouble(double Value) : QuickMarkupValue();
public record class QuickMarkupQMProperty() : AST, IQMNodeChild;
public record class QuickMarkupQMPropertyKeyValue(string Key, QuickMarkupValue Value) : QuickMarkupQMProperty, IQMNodeChild;
public record class QuickMarkupQMPropertyKeyForeign(string Key, QuickMarkupForeign Foreign, bool IsEventMode = false, bool IsBindBack = false) : QuickMarkupQMPropertyKeyValue(Key, Foreign);
public record class QuickMarkupQMPropertyBoolOrExtension(string ExtensionMethod) : QuickMarkupQMProperty();
public record class QuickMarkupQMPropertyExtension(QuickMarkupForeign Extension) : QuickMarkupQMProperty();