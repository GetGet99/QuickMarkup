using Get.Parser;
using Get.PLShared;
using QuickMarkup.AST;
using System.Data.Common;
using static QuickMarkup.Parser.QuickMarkupParser.NonTerminal;
using NonTerminal = QuickMarkup.Parser.QuickMarkupParser.NonTerminal;
using Terminal = QuickMarkup.Parser.QuickMarkupLexer.Tokens;

namespace QuickMarkup.Parser;

[Parser(SFC, UseGetLexerTypeInformation = true)]
public partial class QuickMarkupParser : ParserBase<Terminal, NonTerminal, QuickMarkupSFC>
{
    public enum NonTerminal
    {
        [Type<QuickMarkupSFC>]
        [Rule(typeof(QuickMarkupSFC))]
        [Rule(SFC, AS, LIST, SFCTag, AS, VALUE, APPENDLIST)]
        SFC,
        [Type<ISFCTag>]
        [Rule(Terminal.UsingStatements, AS, nameof(QuickMarkupUsings.RawScript), typeof(QuickMarkupUsings))]
        [Rule(Terminal.Script, AS, nameof(QuickMarkupScript.RawScript), typeof(QuickMarkupScript))]
        [Rule(Terminal.Props, AS, nameof(QuickMarkupProps.RawScript), typeof(QuickMarkupProps))]
        [Rule(TemplateTag, AS, VALUE, IDENTITY)]
        SFCTag,
        [Type<QuickMarkupTemplate>]
        [Rule(Terminal.XMLOpenTagOpen, Terminal.TemplateKeyword, XMLPropertiesInline, AS, nameof(QuickMarkupTemplate.Properties), Terminal.XMLOpenTagCloseAuto, typeof(QuickMarkupTemplate))]
        [Rule(Terminal.XMLOpenTagOpen, Terminal.TemplateKeyword, XMLPropertiesInline, AS, nameof(QuickMarkupTemplate.Properties), Terminal.XMLOpenTagClose,
            XMLChildren, AS, nameof(QuickMarkupTemplate.Children), Terminal.XMLCloseTagOpen, Terminal.TemplateKeyword, Terminal.XMLCloseTagClose, typeof(QuickMarkupTemplate))]
        TemplateTag,
        [Type<ListAST<IXMLNodeChild>>]
        [Rule(XMLNode, AS, VALUE, SINGLELIST)]
        [Rule(XMLPropertyInChild, AS, VALUE, SINGLELIST)]
        [Rule(XMLChildren, AS, LIST, XMLNode, AS, VALUE, APPENDLIST)]
        [Rule(XMLChildren, AS, LIST, XMLPropertyInChild, AS, VALUE, APPENDLIST)]
        XMLChildren,
        [Type<QuickMarkupXMLNode>]
        [Rule(XMLNodeStart, AS, VALUE, Terminal.XMLOpenTagCloseAuto, IDENTITY)]
        [Rule(XMLNodeStart, AS, "node", Terminal.XMLOpenTagClose, XMLChildren, AS, "children", Terminal.XMLCloseTagOpen, Terminal.Identifier, AS, "endTagName", Terminal.XMLCloseTagClose, nameof(BuildWithChildren))]
        XMLNode,
        [Type<QuickMarkupXMLNode>]
        [Rule(Terminal.XMLOpenTagOpen, Terminal.Identifier, AS, nameof(QuickMarkupXMLNode.Name),
            XMLPropertiesInline, AS, nameof(QuickMarkupXMLNode.Properties),
            typeof(QuickMarkupXMLNode))]
        XMLNodeStart,
        [Type<ListAST<QuickMarkupXMLPropertiesKeyValue>>]
        [Rule(EMPTYLIST)]
        [Rule(XMLPropertiesInline, AS, LIST, XMLPropertyInline, AS, VALUE, APPENDLIST)]
        XMLPropertiesInline,
        [Type<QuickMarkupXMLPropertiesKeyValue>]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyInt32.Key), Terminal.Equal, Terminal.Integer, AS, nameof(QuickMarkupXMLPropertiesKeyInt32.Value), typeof(QuickMarkupXMLPropertiesKeyInt32))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyString.Key), Terminal.Equal, Terminal.String, AS, nameof(QuickMarkupXMLPropertiesKeyString.Value), typeof(QuickMarkupXMLPropertiesKeyString))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyForeign.Key), Terminal.Equal, Terminal.Foreign, AS, nameof(QuickMarkupXMLPropertiesKeyForeign.ForeignAsString), typeof(QuickMarkupXMLPropertiesKeyForeign))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyBoolean.Key), Terminal.Equal, Terminal.Boolean, AS, nameof(QuickMarkupXMLPropertiesKeyBoolean.Value), typeof(QuickMarkupXMLPropertiesKeyBoolean))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyBoolean.Key), WITHPARAM, nameof(QuickMarkupXMLPropertiesKeyBoolean.Value), true, typeof(QuickMarkupXMLPropertiesKeyBoolean))]
        [Rule(Terminal.Not, Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyBoolean.Key), WITHPARAM, nameof(QuickMarkupXMLPropertiesKeyBoolean.Value), false, typeof(QuickMarkupXMLPropertiesKeyBoolean))]
        XMLPropertyInline,
        [Type<QuickMarkupXMLPropertiesKeyValue>]
        [Rule(Terminal.XMLOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyInt32.Key), Terminal.Equal, Terminal.Integer, AS, nameof(QuickMarkupXMLPropertiesKeyInt32.Value), Terminal.XMLOpenTagCloseAuto, typeof(QuickMarkupXMLPropertiesKeyInt32))]
        [Rule(Terminal.XMLOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyString.Key), Terminal.Equal, Terminal.String, AS, nameof(QuickMarkupXMLPropertiesKeyString.Value), Terminal.XMLOpenTagCloseAuto, typeof(QuickMarkupXMLPropertiesKeyString))]
        [Rule(Terminal.XMLOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyForeign.Key), Terminal.Equal, Terminal.Foreign, AS, nameof(QuickMarkupXMLPropertiesKeyForeign.ForeignAsString), Terminal.XMLOpenTagCloseAuto, typeof(QuickMarkupXMLPropertiesKeyForeign))]
        [Rule(Terminal.XMLOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyBoolean.Key), Terminal.Equal, Terminal.Boolean, AS, nameof(QuickMarkupXMLPropertiesKeyBoolean.Value), Terminal.XMLOpenTagCloseAuto, typeof(QuickMarkupXMLPropertiesKeyBoolean))]
        [Rule(Terminal.XMLOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupXMLPropertiesKeyBoolean.Key), Terminal.XMLOpenTagCloseAuto, WITHPARAM, nameof(QuickMarkupXMLPropertiesKeyBoolean.Value), true, typeof(QuickMarkupXMLPropertiesKeyBoolean))]
        XMLPropertyInChild
    }
    static QuickMarkupXMLNode BuildWithChildren(QuickMarkupXMLNode node, ListAST<IXMLNodeChild> children, string endTagName)
    {
        if (node.Name != endTagName)
        {
            throw new InvalidOperationException($"Name of opening tag <{node.Name}> and end tag </${endTagName}> does not match");
        }
        node.Add(children);
        return node;
    }
    public QuickMarkupSFC Parse(IEnumerable<IToken<Terminal>> inputTerminals)
    {
        IEnumerable<ITerminalValue> TerminalValues()
        {
            foreach (var inputTerminal in inputTerminals)
            {
                if (inputTerminal is IToken<Terminal, int> intTok)
                    yield return CreateValue(inputTerminal.TokenType, intTok.Data);
                else if (inputTerminal is IToken<Terminal, bool> boolTok)
                    yield return CreateValue(inputTerminal.TokenType, boolTok.Data);
                else if (inputTerminal is IToken<Terminal, string> strTok)
                    yield return CreateValue(inputTerminal.TokenType, strTok.Data);
                else
                    yield return CreateValue(inputTerminal.TokenType);
            }
        }
        return Parse(TerminalValues(), debug: true);
    }
}
