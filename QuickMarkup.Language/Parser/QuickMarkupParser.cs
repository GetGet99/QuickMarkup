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
        [Rule(Terminal.Setup, AS, nameof(QuickMarkupScript.RawScript), typeof(QuickMarkupScript))]
        [Rule(Terminal.Props, AS, nameof(QuickMarkupProps.RawScript), typeof(QuickMarkupProps))]
        [Rule(TemplateTag, AS, VALUE, IDENTITY)]
        SFCTag,
        [Type<QuickMarkupTemplate>]
        [Rule(Terminal.QMOpenTagOpen, Terminal.RootKeyword, QMPropertiesInline, AS, nameof(QuickMarkupTemplate.Properties), Terminal.QMOpenTagCloseAuto, typeof(QuickMarkupTemplate))]
        [Rule(Terminal.QMOpenTagOpen, Terminal.RootKeyword, QMPropertiesInline, AS, nameof(QuickMarkupTemplate.Properties), Terminal.QMOpenTagClose,
            QMChildren, AS, nameof(QuickMarkupTemplate.Children), Terminal.QMCloseTagOpen, Terminal.RootKeyword, Terminal.QMCloseTagClose, typeof(QuickMarkupTemplate))]
        TemplateTag,
        [Type<ListAST<IQMNodeChild>>]
        [Rule(EMPTYLIST)]
        [Rule(QMChildren, AS, LIST, QMNode, AS, VALUE, APPENDLIST)]
        [Rule(QMChildren, AS, LIST, QMPropertyInChild, AS, VALUE, APPENDLIST)]
        [Rule(QMChildren, AS, LIST, QMForNode, AS, VALUE, APPENDLIST)]
        QMChildren,
        [Type<string>]
        //[Rule(Terminal.Identifier, AS, VALUE, IDENTITY)]
        [Rule(WITHPARAM, VALUE, "var", IDENTITY)]
        OptionalType,
        [Type<QuickMarkupForNode>]
        [Rule(Terminal.For, Terminal.OpenBracket,
            OptionalType, AS, nameof(QuickMarkupForNode.VarType),
            Terminal.Identifier, AS, nameof(QuickMarkupForNode.TargetVariable),
            Terminal.In,
            ForList, AS, nameof(QuickMarkupForNode.ListExpression),
            Terminal.CloseBracket,
            typeof(QuickMarkupForNode)
        )]
        QMForNodeStart,
        [Type<QuickMarkupForNode>]
        [Rule(QMForNodeStart, AS, LIST, Terminal.OpenCuryBracket, QMChildren, AS, VALUE, Terminal.CloseCuryBracket, APPENDLIST)]
        [Rule(QMForNodeStart, AS, LIST, QMNode, AS, VALUE, APPENDLIST)]
        [Rule(QMForNodeStart, AS, LIST, QMForNode, AS, VALUE, APPENDLIST)]
        QMForNode,
        [Type<QuickMarkupForNodeListExpression>]
        [Rule(Terminal.Integer, AS, nameof(QuickMarkupForNodeListRangeExpression.Start), Terminal.Range, Terminal.Integer, AS, nameof(QuickMarkupForNodeListRangeExpression.End), typeof(QuickMarkupForNodeListRangeExpression))]
        [Rule(Terminal.Range, Terminal.Integer, AS, nameof(QuickMarkupForNodeListRangeExpression.End), WITHPARAM, nameof(QuickMarkupForNodeListRangeExpression.Start), 0, typeof(QuickMarkupForNodeListRangeExpression))]
        [Rule(Terminal.Foreign, AS, nameof(QuickMarkupForNodeListForeignExpression.ForeignAsString), typeof(QuickMarkupForNodeListForeignExpression))]
        ForList,
        [Type<QuickMarkupQMNode>]
        [Rule(QMNodeStart, AS, VALUE, Terminal.QMOpenTagCloseAuto, IDENTITY)]
        [Rule(QMNodeStart, AS, "node", Terminal.QMOpenTagClose, QMChildren, AS, "children", Terminal.QMCloseTagOpen, Terminal.Identifier, AS, "endTagName", Terminal.QMCloseTagClose, nameof(BuildWithChildren))]
        [Rule(Terminal.Identifier, AS, "name", Terminal.Equal, QMNode, AS, "node", nameof(AddName))]
        QMNode,
        [Type<QuickMarkupQMNode>]
        [Rule(Terminal.QMOpenTagOpen, Terminal.Identifier, AS, nameof(QuickMarkupQMNode.TypeName),
            QMPropertiesInline, AS, nameof(QuickMarkupQMNode.Properties),
            typeof(QuickMarkupQMNode))]
        QMNodeStart,
        [Type<ListAST<QuickMarkupQMPropertiesKeyValue>>]
        [Rule(EMPTYLIST)]
        [Rule(QMPropertiesInline, AS, LIST, QMPropertyInline, AS, VALUE, APPENDLIST)]
        QMPropertiesInline,
        [Type<string>]
        [Rule(Terminal.Identifier, AS, VALUE, IDENTITY)]
        [Rule(Terminal.Foreign, AS, VALUE, IDENTITY)]
        QMKey,
        [Type<QuickMarkupQMPropertiesKeyValue>]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyInt32.Key), Terminal.Equal, Terminal.Integer, AS, nameof(QuickMarkupQMPropertiesKeyInt32.Value), typeof(QuickMarkupQMPropertiesKeyInt32))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyInt32.Key), Terminal.Equal, Terminal.Double, AS, nameof(QuickMarkupQMPropertiesKeyDouble.Value), typeof(QuickMarkupQMPropertiesKeyDouble))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyString.Key), Terminal.Equal, Terminal.String, AS, nameof(QuickMarkupQMPropertiesKeyString.Value), typeof(QuickMarkupQMPropertiesKeyString))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyForeign.Key), Terminal.Equal, Terminal.Foreign, AS, nameof(QuickMarkupQMPropertiesKeyForeign.ForeignAsString), typeof(QuickMarkupQMPropertiesKeyForeign))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyForeign.Key), Terminal.EqualBindBack, Terminal.Foreign, AS, nameof(QuickMarkupQMPropertiesKeyForeign.ForeignAsString), WITHPARAM, nameof(QuickMarkupQMPropertiesKeyForeign.IsBindBack), true, typeof(QuickMarkupQMPropertiesKeyForeign))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyForeign.Key), Terminal.AddEqual, Terminal.Foreign, AS, nameof(QuickMarkupQMPropertiesKeyForeign.ForeignAsString), WITHPARAM, nameof(QuickMarkupQMPropertiesKeyForeign.IsEventMode), true, typeof(QuickMarkupQMPropertiesKeyForeign))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyEnum.Key), Terminal.Equal, Terminal.Identifier, AS, nameof(QuickMarkupQMPropertiesKeyEnum.EnumMember), typeof(QuickMarkupQMPropertiesKeyEnum))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyBoolean.Key), Terminal.Equal, Terminal.Boolean, AS, nameof(QuickMarkupQMPropertiesKeyBoolean.Value), typeof(QuickMarkupQMPropertiesKeyBoolean))]
        [Rule(Terminal.Not, QMKey, AS, nameof(QuickMarkupQMPropertiesKeyBoolean.Key), WITHPARAM, nameof(QuickMarkupQMPropertiesKeyBoolean.Value), false, typeof(QuickMarkupQMPropertiesKeyBoolean))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyQM.Key), Terminal.Equal, QMNode, AS, nameof(QuickMarkupQMPropertiesKeyQM.Value), typeof(QuickMarkupQMPropertiesKeyQM))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertiesKeyQMs.Key), Terminal.Equal, Terminal.QMOpenTagOpen, Terminal.QMOpenTagClose, QMChildren, AS, nameof(QuickMarkupQMPropertiesKeyQMs.Value), Terminal.QMCloseTagOpen, Terminal.QMCloseTagClose, typeof(QuickMarkupQMPropertiesKeyQMs))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupQMPropertiesBoolOrExtension.ExtensionMethod), typeof(QuickMarkupQMPropertiesBoolOrExtension))]
        [Rule(Terminal.Foreign, AS, nameof(QuickMarkupQMPropertiesKeyForeign.ForeignAsString), WITHPARAM, nameof(QuickMarkupQMPropertiesKeyForeign.Key), null, typeof(QuickMarkupQMPropertiesKeyForeign))]
        QMPropertyInline,
        [Type<QuickMarkupQMPropertiesKeyValue>]
        [Rule(Terminal.QMOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupQMPropertiesKeyInt32.Key), Terminal.Equal, Terminal.Integer, AS, nameof(QuickMarkupQMPropertiesKeyInt32.Value), Terminal.QMOpenTagCloseAuto, typeof(QuickMarkupQMPropertiesKeyInt32))]
        [Rule(Terminal.QMOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupQMPropertiesKeyString.Key), Terminal.Equal, Terminal.String, AS, nameof(QuickMarkupQMPropertiesKeyString.Value), Terminal.QMOpenTagCloseAuto, typeof(QuickMarkupQMPropertiesKeyString))]
        [Rule(Terminal.QMOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupQMPropertiesKeyForeign.Key), Terminal.Equal, Terminal.Foreign, AS, nameof(QuickMarkupQMPropertiesKeyForeign.ForeignAsString), Terminal.QMOpenTagCloseAuto, typeof(QuickMarkupQMPropertiesKeyForeign))]
        [Rule(Terminal.QMOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupQMPropertiesKeyBoolean.Key), Terminal.Equal, Terminal.Boolean, AS, nameof(QuickMarkupQMPropertiesKeyBoolean.Value), Terminal.QMOpenTagCloseAuto, typeof(QuickMarkupQMPropertiesKeyBoolean))]
        [Rule(Terminal.QMOpenTagOpen, Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupQMPropertiesKeyBoolean.Key), Terminal.QMOpenTagCloseAuto, WITHPARAM, nameof(QuickMarkupQMPropertiesKeyBoolean.Value), true, typeof(QuickMarkupQMPropertiesKeyBoolean))]
        QMPropertyInChild
    }
    static QuickMarkupQMNode AddName(QuickMarkupQMNode node, string name)
    {
        return node with { Name = name };
    }
    static QuickMarkupQMNode BuildWithChildren(QuickMarkupQMNode node, ListAST<IQMNodeChild> children, string endTagName)
    {
        if (node.TypeName != endTagName)
        {
            throw new InvalidOperationException($"Name of opening tag <{node.TypeName}> and end tag </{endTagName}> does not match");
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
                Console.WriteLine($"Reading Terminal: {inputTerminal.TokenType} ({inputTerminal.Start} - {inputTerminal.End})");
                if (inputTerminal is IToken<Terminal, int> intTok)
                    yield return CreateValue(inputTerminal.TokenType, intTok.Data);
                else if (inputTerminal is IToken<Terminal, double> doubleTok)
                    yield return CreateValue(inputTerminal.TokenType, doubleTok.Data);
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
