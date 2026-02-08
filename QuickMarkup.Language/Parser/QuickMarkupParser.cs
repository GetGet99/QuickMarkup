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
        // SFC LEVEL SETUP
        [Type<QuickMarkupSFC>]
        [Rule(
            UsingStatementsOrEmpty, AS, nameof(QuickMarkupSFC.Usings),
            RefsDecl, AS, nameof(QuickMarkupSFC.Refs),
            typeof(QuickMarkupSFC))]
        [Rule(SFC, AS, LIST, SFCTag, AS, VALUE, APPENDLIST)]
        SFC,
        [Type<ISFCTag>]
        [Rule(Terminal.Setup, AS, nameof(QuickMarkupScript.RawScript), typeof(QuickMarkupScript))]
        [Rule(ParsedTag, AS, VALUE, IDENTITY)]
        SFCTag,
        // USINGS
        [Type<string>]
        [Rule(UsingStatements, AS, VALUE, IDENTITY)]
        [Rule(WITHPARAM, VALUE, "", IDENTITY)]
        UsingStatementsOrEmpty,
        [Type<string>]
        [Rule(Terminal.UsingStatement, AS, VALUE, IDENTITY)]
        [Rule(UsingStatements, AS, "A", Terminal.UsingStatement, AS, "B", nameof(CombineUsings))]
        UsingStatements,
        // REFS
        [Type<ListAST<RefDeclaration>>]
        [Rule(EMPTYLIST)]
        [Rule(RefsDecl, AS, LIST, RefDecl, AS, VALUE, APPENDLIST)]
        RefsDecl,
        [Type<RefDeclaration>]
        [Rule(
            RefPrivateVisibility, AS, nameof(RefDeclaration.IsPrivate),
            TypeDecl, AS, nameof(RefDeclaration.Type),
            Terminal.Identifier, AS, nameof(RefDeclaration.Name),
            RefDeclInitialValue, AS, nameof(RefDeclaration.DefaultValue),
            Terminal.Semicolon,
            WITHPARAM, nameof(RefDeclaration.IsComputedDeclaration), false,
            typeof(RefDeclaration)
        )]
        [Rule(
            RefPrivateVisibility, AS, nameof(RefDeclaration.IsPrivate),
            TypeDecl, AS, nameof(RefDeclaration.Type),
            Terminal.Identifier, AS, nameof(RefDeclaration.Name),
            Terminal.EqualArrowRight,
            QMValue, AS, nameof(RefDeclaration.DefaultValue),
            Terminal.Semicolon,
            WITHPARAM, nameof(RefDeclaration.IsComputedDeclaration), true,
            typeof(RefDeclaration)
        )]
        RefDecl,
        [Type<QuickMarkupValue>]
        [Rule(Terminal.Equal, QMValue, AS, VALUE, IDENTITY)]
        [Rule(WITHPARAM, nameof(QuickMarkupDefault.IsExplicitlyNull), false, typeof(QuickMarkupDefault))]
        RefDeclInitialValue,
        [Type<bool>]
        [Rule(WITHPARAM, VALUE, false, IDENTITY)]
        [Rule(Terminal.Private, WITHPARAM, VALUE, true, IDENTITY)]
        RefPrivateVisibility,
        // TAGS
        [Type<QuickMarkupParsedTag>]
        [Rule(
            Terminal.QMOpenTagOpen,
            QMConstructor, AS, nameof(QuickMarkupParsedTag.Constructor),
            ParsedProperties, AS, nameof(QuickMarkupParsedTag.Properties),
            Terminal.QMOpenTagCloseAuto,
            WITHPARAM, nameof(QuickMarkupParsedTag.Children), null,
            WITHPARAM, nameof(QuickMarkupParsedTag.EndTagName), null,
            WITHPARAM, nameof(QuickMarkupParsedTag.IsSelfClosing), true,
            typeof(QuickMarkupParsedTag)
        )]
        [Rule(
            Terminal.QMOpenTagOpen,
            QMConstructor, AS, nameof(QuickMarkupParsedTag.Constructor),
            ParsedProperties, AS, nameof(QuickMarkupParsedTag.Properties),
            Terminal.QMOpenTagClose,
            QMChildren, AS, nameof(QuickMarkupParsedTag.Children),
            Terminal.QMCloseTagOpen,
            Terminal.Identifier, AS, nameof(QuickMarkupParsedTag.EndTagName),
            Terminal.QMCloseTagClose,
            WITHPARAM, nameof(QuickMarkupParsedTag.IsSelfClosing), false,
            typeof(QuickMarkupParsedTag)
        )]
        ParsedTag,
        [Type<QuickMarkupParsedTag>]
        [Rule(
            Terminal.Identifier, AS, "name",
            Terminal.Equal,
            ParsedTag, AS, "tag",
            nameof(AttachName)
        )]
        NamedTag,
        // CONSTRUCTOR
        [Type<QuickMarkupConstructor>]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupConstructor.TagName), typeof(QuickMarkupConstructor))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupConstructor.TagName), Terminal.OpenBracket, QMConstructorParameters, AS, nameof(QuickMarkupConstructor.Parameters), Terminal.CloseBracket, typeof(QuickMarkupConstructor))]
        QMConstructor,
        [Type<ListAST<QuickMarkupValue>>]
        [Rule(EMPTYLIST)]
        [Rule(QMConstructorParametersInside, AS, VALUE, IDENTITY)]
        QMConstructorParameters,
        [Type<ListAST<QuickMarkupValue>>]
        [Rule(QMValue, AS, VALUE, SINGLELIST)]
        [Rule(QMConstructorParametersInside, AS, LIST, Terminal.Comma, QMValue, AS, VALUE, APPENDLIST)]
        QMConstructorParametersInside,
        // PROPERTIES
        [Type<ParsedPropertyOperator>]
        [Rule(Terminal.Equal, WITHPARAM, VALUE, ParsedPropertyOperator.Assign, IDENTITY)]
        [Rule(Terminal.EqualArrowRight, WITHPARAM, VALUE, ParsedPropertyOperator.BindBack, IDENTITY)]
        [Rule(Terminal.AddEqual, WITHPARAM, VALUE, ParsedPropertyOperator.AddAssign, IDENTITY)]
        PropertyOperator,
        [Type<QuickMarkupParsedProperty>]
        [Rule(
            Terminal.Identifier, AS, nameof(QuickMarkupParsedProperty.Key),
            PropertyOperator, AS, nameof(QuickMarkupParsedProperty.Operator),
            QMValue, AS, nameof(QuickMarkupParsedProperty.Value),
            typeof(QuickMarkupParsedProperty)
        )]
        [Rule(
            Terminal.Identifier, AS, nameof(QuickMarkupParsedProperty.Key),
            WITHPARAM, nameof(QuickMarkupParsedProperty.Operator), ParsedPropertyOperator.None,
            WITHPARAM, nameof(QuickMarkupParsedProperty.Value), null,
            typeof(QuickMarkupParsedProperty)
        )]
        [Rule(
            Terminal.Not,
            Terminal.Identifier, AS, nameof(QuickMarkupParsedProperty.Key),
            WITHPARAM, nameof(QuickMarkupParsedProperty.Operator), ParsedPropertyOperator.Assign,
            WITHPARAM, nameof(QuickMarkupParsedProperty.Value), false,
            typeof(QuickMarkupParsedProperty)
        )]
        ParsedProperty,
        [Type<ListAST<QuickMarkupParsedProperty>>]
        [Rule(EMPTYLIST)]
        [Rule(ParsedProperties, AS, LIST, ParsedProperty, AS, VALUE, APPENDLIST)]
        ParsedProperties,
        [Type<ListAST<IQMNodeChild>>]
        [Rule(EMPTYLIST)]
        [Rule(QMChildren, AS, LIST, QMChild, AS, VALUE, APPENDLIST)]
        QMChildren,
        [Type<IQMNodeChild>]
        [Rule(ParsedForNode, AS, VALUE, IDENTITY)]
        [Rule(QMValue, AS, VALUE, IDENTITY)]
        QMChild,
        [Type<QuickMarkupParsedForNode>]
        [Rule(
            Terminal.For,
            Terminal.OpenBracket,
            OptionalTypeDecl, AS, nameof(QuickMarkupParsedForNode.VarType),
            Terminal.Identifier, AS, nameof(QuickMarkupParsedForNode.VarName),
            Terminal.In,
            QMValue, AS, nameof(QuickMarkupParsedForNode.Iterable),
            Terminal.CloseBracket,
            Terminal.OpenCuryBracket,
            QMChildren, AS, nameof(QuickMarkupParsedForNode.Body),
            Terminal.CloseCuryBracket,
            typeof(QuickMarkupParsedForNode)
        )]
        ParsedForNode,
        [Type<QuickMarkupValue>]
        [Rule(Terminal.Integer, AS, nameof(QuickMarkupInt32.Value), typeof(QuickMarkupInt32))]
        [Rule(Terminal.Double, AS, nameof(QuickMarkupDouble.Value), typeof(QuickMarkupDouble))]
        [Rule(Terminal.String, AS, nameof(QuickMarkupString.Value), typeof(QuickMarkupString))]
        [Rule(Terminal.Boolean, AS, nameof(QuickMarkupBoolean.Value), typeof(QuickMarkupBoolean))]
        [Rule(Terminal.Foreign, AS, nameof(QuickMarkupForeign.Code), typeof(QuickMarkupForeign))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupIdentifier.Identifier), typeof(QuickMarkupIdentifier))]
        [Rule(ParsedTag, AS, VALUE, IDENTITY)]
        [Rule(NamedTag, AS, VALUE, IDENTITY)]
        [Rule(Terminal.QMOpenTagOpen, Terminal.QMOpenTagClose,
            QMChildren, AS, nameof(QuickMarkupQMs.Value),
            Terminal.QMCloseTagOpen, Terminal.QMCloseTagClose,
            typeof(QuickMarkupQMs)
        )]
        QMValue,
        // TYPES
        [Type<TypeDeclaration>]
        [Rule(Terminal.Foreign, AS, nameof(TypeDeclaration.Type), typeof(TypeDeclaration))]
        [Rule(Terminal.Identifier, AS, nameof(TypeDeclaration.Type), typeof(TypeDeclaration))]
        [Rule(Terminal.Identifier, AS, nameof(TypeDeclaration.Type),
            Terminal.QuestionMark, WITHPARAM, nameof(TypeDeclaration.IsTypeNullable), true,
            typeof(TypeDeclaration))]
        TypeDecl,
        [Type<TypeDeclaration>]
        [Rule(TypeDecl, AS, VALUE, IDENTITY)]
        [Rule(Terminal.Var, WITHPARAM, VALUE, null, IDENTITY)]
        OptionalTypeDecl,
    }
    static QuickMarkupParsedTag AttachName(string name, QuickMarkupParsedTag tag)
        => tag with { Name = name };
    static string CombineUsings(string A, string B)
    {
        return $"""
            {A}
            {B}
            """;
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