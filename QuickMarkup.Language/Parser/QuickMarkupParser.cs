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
        [Rule(Terminal.QMOpenTagOpen, QMConstructor, AS, nameof(QuickMarkupQMNode.Constructor),
            QMPropertiesInline, AS, nameof(QuickMarkupQMNode.Properties),
            typeof(QuickMarkupQMNode))]
        QMNodeStart,
        [Type<QuickMarkupConstructor>]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupConstructor.TypeName), typeof(QuickMarkupConstructor))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupConstructor.TypeName),
            Terminal.OpenBracket,
            QMConstructorParameters, AS, nameof(QuickMarkupConstructor.Parameters),
            Terminal.CloseBracket, typeof(QuickMarkupConstructor))]
        QMConstructor,
        [Type<ListAST<QuickMarkupValue>>]
        [Rule(EMPTYLIST)]
        [Rule(QMConstructorParametersInside, AS, VALUE, IDENTITY)]
        QMConstructorParameters,
        [Type<ListAST<QuickMarkupValue>>]
        [Rule(QMValueAndEnum, AS, VALUE, SINGLELIST)]
        [Rule(QMConstructorParametersInside, AS, LIST, Terminal.Comma, QMValueAndEnum, AS, VALUE, APPENDLIST)]
        QMConstructorParametersInside,
        [Type<ListAST<QuickMarkupQMProperty>>]
        [Rule(EMPTYLIST)]
        [Rule(QMPropertiesInline, AS, LIST, QMPropertyInline, AS, VALUE, APPENDLIST)]
        QMPropertiesInline,
        [Type<string>]
        [Rule(Terminal.Identifier, AS, VALUE, IDENTITY)]
        [Rule(Terminal.Foreign, AS, VALUE, IDENTITY)]
        QMKey,
        [Type<QuickMarkupQMProperty>]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertyKeyValue.Key), Terminal.Equal, QMMultiValue, AS, nameof(QuickMarkupQMPropertyKeyValue.Value), typeof(QuickMarkupQMPropertyKeyValue))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertyKeyForeign.Key), Terminal.EqualBindBack, QMForeign, AS, nameof(QuickMarkupQMPropertyKeyForeign.Foreign), WITHPARAM, nameof(QuickMarkupQMPropertyKeyForeign.IsBindBack), true, typeof(QuickMarkupQMPropertyKeyForeign))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertyKeyForeign.Key), Terminal.AddEqual, QMForeign, AS, nameof(QuickMarkupQMPropertyKeyForeign.Foreign), WITHPARAM, nameof(QuickMarkupQMPropertyKeyForeign.IsEventMode), true, typeof(QuickMarkupQMPropertyKeyForeign))]
        [Rule(QMKey, AS, nameof(QuickMarkupQMPropertyKeyValue.Key), Terminal.Equal, QMEnum, AS, nameof(QuickMarkupQMPropertyKeyValue.Value), typeof(QuickMarkupQMPropertyKeyValue))]
        [Rule(QMNotAsFalse, AS, nameof(QuickMarkupQMPropertyKeyValue.Value), QMKey, AS, nameof(QuickMarkupQMPropertyKeyValue.Key), typeof(QuickMarkupQMPropertyKeyValue))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupQMPropertyBoolOrExtension.ExtensionMethod), typeof(QuickMarkupQMPropertyBoolOrExtension))]
        [Rule(QMForeign, AS, nameof(QuickMarkupQMPropertiesExtension.Extension), typeof(QuickMarkupQMPropertiesExtension))]
        QMPropertyInline,
        [Type<QuickMarkupQMPropertyKeyValue>]
        [Rule(Terminal.QMOpenTagOpen,
            Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupQMPropertyKeyValue.Key),
            Terminal.Equal, QMValue, AS, nameof(QuickMarkupQMPropertyKeyValue.Value),
            Terminal.QMOpenTagCloseAuto,
        typeof(QuickMarkupQMPropertyKeyValue))]
        [Rule(Terminal.QMOpenTagOpen,
            Terminal.Dot, Terminal.Identifier, AS, "key",
            Terminal.QMOpenTagClose,
            QMValueNoQM, AS, "value",
            Terminal.QMCloseTagOpen,
            Terminal.Dot,
            Terminal.Identifier, AS, "endTagName",
            Terminal.QMCloseTagClose,
        nameof(BuildWithValue))]
        [Rule(Terminal.QMOpenTagOpen,
            Terminal.Dot, Terminal.Identifier, AS, "key",
            Terminal.QMOpenTagClose,
            QMsWithoutFragment, AS, "value",
            Terminal.QMCloseTagOpen,
            Terminal.Dot,
            Terminal.Identifier, AS, "endTagName",
            Terminal.QMCloseTagClose,
        nameof(BuildWithValue))]
        QMPropertyInChild,
        [Type<QuickMarkupValue>]
        [Rule(Terminal.Integer, AS, nameof(QuickMarkupInt32.Value), typeof(QuickMarkupInt32))]
        [Rule(Terminal.Double, AS, nameof(QuickMarkupDouble.Value), typeof(QuickMarkupDouble))]
        [Rule(Terminal.String, AS, nameof(QuickMarkupString.Value), typeof(QuickMarkupString))]
        [Rule(QMForeign, AS, VALUE, IDENTITY)]
        [Rule(Terminal.Boolean, AS, nameof(QuickMarkupBoolean.Value), typeof(QuickMarkupBoolean))]
        QMValueNoQM,
        [Type<QuickMarkupValue>]
        [Rule(QMValueNoQM, AS, VALUE, IDENTITY)]
        [Rule(QMNode, AS, nameof(QuickMarkupQM.Value), typeof(QuickMarkupQM))]
        QMValue,
        [Type<QuickMarkupValue>]
        [Rule(QMValue, AS, VALUE, IDENTITY)]
        [Rule(QMEnum, AS, VALUE, IDENTITY)]
        QMValueAndEnum,
        [Type<QuickMarkupValue>]
        [Rule(QMs, AS, VALUE, IDENTITY)]
        [Rule(QMValue, AS, VALUE, IDENTITY)]
        QMMultiValue,
        [Type<QuickMarkupQMs>]
        [Rule(Terminal.QMOpenTagOpen, Terminal.QMOpenTagClose,
            QMChildren, AS, nameof(QuickMarkupQMs.Value),
            Terminal.QMCloseTagOpen, Terminal.QMCloseTagClose, typeof(QuickMarkupQMs))]
        QMs,
        [Type<QuickMarkupQMs>]
        [Rule(QMChildren, AS, nameof(QuickMarkupQMs.Value), typeof(QuickMarkupQMs))]
        QMsWithoutFragment,
        [Type<QuickMarkupEnum>]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupEnum.EnumMember), typeof(QuickMarkupEnum))]
        QMEnum,
        [Type<QuickMarkupForeign>]
        [Rule(Terminal.Foreign, AS, nameof(QuickMarkupForeign.Code), typeof(QuickMarkupForeign))]
        QMForeign,
        [Type<QuickMarkupBoolean>]
        [Rule(Terminal.Not, WITHPARAM, nameof(QuickMarkupBoolean.Value), false, typeof(QuickMarkupBoolean))]
        QMNotAsFalse
    }
    static QuickMarkupQMNode AddName(QuickMarkupQMNode node, string name)
    {
        return node with { AssignToVarName = name };
    }
    static QuickMarkupQMNode BuildWithChildren(QuickMarkupQMNode node, ListAST<IQMNodeChild> children, string endTagName)
    {
        if (node.Constructor.TypeName != endTagName)
        {
            throw new InvalidOperationException($"Name of opening tag <{node.Constructor}> and end tag </{endTagName}> does not match");
        }
        node.Add(children);
        return node;
    }
    static QuickMarkupQMPropertyKeyValue BuildWithValue(string key, QuickMarkupValue value, string endTagName)
    {
        if (key != endTagName)
        {
            throw new InvalidOperationException($"Name of opening tag <{key}> and end tag </{endTagName}> does not match");
        }
        if (value is QuickMarkupQMs qms && qms.Value.Count is 1)
        {
            value = (QuickMarkupQM)qms.Value[0];
        }
        return new(key, value);
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