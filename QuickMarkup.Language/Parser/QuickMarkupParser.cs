using Get.Parser;
using Get.PLShared;
using QuickMarkup.AST;
using System.Data.Common;
using System.Diagnostics;
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
            ParsedTagStart, AS, nameof(QuickMarkupParsedTag.TagStart),
            InlineMembers, AS, nameof(QuickMarkupParsedTag.InlineMembers),
            Terminal.QMOpenTagCloseAuto,
            WITHPARAM, nameof(QuickMarkupParsedTag.Children), null,
            WITHPARAM, nameof(QuickMarkupParsedTag.EndTagName), null,
            WITHPARAM, nameof(QuickMarkupParsedTag.IsSelfClosing), true,
            typeof(QuickMarkupParsedTag)
        )]
        [Rule(
            Terminal.QMOpenTagOpen,
            ParsedTagStart, AS, nameof(QuickMarkupParsedTag.TagStart),
            InlineMembers, AS, nameof(QuickMarkupParsedTag.InlineMembers),
            Terminal.QMOpenTagClose,
            QMChildren, AS, nameof(QuickMarkupParsedTag.Children),
            Terminal.QMCloseTagOpen,
            ParsedTagEnd, AS, nameof(QuickMarkupParsedTag.EndTagName),
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
        // TAGSTART/TAGEND HELPER
        [Type<ITagStart>]
        [Rule(Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupPropertyTagStart.TagName), typeof(QuickMarkupPropertyTagStart))]
        [Rule(QMConstructor, AS, VALUE, IDENTITY)]
        ParsedTagStart,
        [Type<string>]
        [Rule(Terminal.Identifier, AS, VALUE, IDENTITY)]
        [Rule(Terminal.Dot, Terminal.Identifier, AS, "name", nameof(AddDot))]
        ParsedTagEnd,
        // PROPERTIES
        [Type<ParsedPropertyOperator>]
        [Rule(Terminal.Equal, WITHPARAM, VALUE, ParsedPropertyOperator.Assign, IDENTITY)]
        [Rule(Terminal.EqualArrowRight, WITHPARAM, VALUE, ParsedPropertyOperator.BindBack, IDENTITY)]
        [Rule(Terminal.AddEqual, WITHPARAM, VALUE, ParsedPropertyOperator.AddAssign, IDENTITY)]
        PropertyOperator,
        [Type<QuickMarkupInlineMember>]
        [Rule(
            ParsedPropertyKey, AS, nameof(QuickMarkupParsedProperty.Key),
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
            Terminal.Foreign, AS, nameof(QuickMarkupCallback.Code),
            typeof(QuickMarkupCallback)
        )]
        [Rule(
            Terminal.Not,
            ParsedPropertyKey, AS, nameof(QuickMarkupParsedProperty.Key),
            WITHPARAM, nameof(QuickMarkupParsedProperty.Operator), ParsedPropertyOperator.Assign,
            WITHPARAM, nameof(QuickMarkupParsedProperty.Value), false,
            typeof(QuickMarkupParsedProperty)
        )]
        InlineMember,
        [Type<string>]
        [Rule(Terminal.Identifier, AS, VALUE, IDENTITY)]
        [Rule(Terminal.Foreign, AS, VALUE, IDENTITY)]
        ParsedPropertyKey,
        [Type<ListAST<QuickMarkupInlineMember>>]
        [Rule(InlineMember, AS, VALUE, SINGLELIST)]
        [Rule(InlineMembersInner, AS, LIST, InlineMember, AS, VALUE, APPENDLIST)]
        InlineMembersInner,
        [Type<ListAST<QuickMarkupInlineMember>>]
        [Rule(EMPTYLIST)]
        [Rule(InlineMembersInner, AS, VALUE, IDENTITY)]
        InlineMembers,
        [Type<ListAST<IQMNodeChild>>]
        [Rule(EMPTYLIST)]
        [Rule(QMChildren, AS, LIST, QMChild, AS, VALUE, APPENDLIST)]
        QMChildren,
        [Type<ListAST<IQMNodeChild>>]
        [Rule(QMChild, AS, VALUE, SINGLELIST)]
        QMSingleChildList,
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
            QMIterable, AS, nameof(QuickMarkupParsedForNode.Iterable),
            Terminal.CloseBracket,
            Terminal.OpenCuryBracket,
            QMChildren, AS, nameof(QuickMarkupParsedForNode.Body),
            Terminal.CloseCuryBracket,
            typeof(QuickMarkupParsedForNode)
        )]
        [Rule(
            Terminal.For,
            Terminal.OpenBracket,
            OptionalTypeDecl, AS, nameof(QuickMarkupParsedForNode.VarType),
            Terminal.Identifier, AS, nameof(QuickMarkupParsedForNode.VarName),
            Terminal.In,
            QMIterable, AS, nameof(QuickMarkupParsedForNode.Iterable),
            Terminal.CloseBracket,
            QMSingleChildList, AS, nameof(QuickMarkupParsedForNode.Body),
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
        [Rule(Terminal.Null, WITHPARAM, nameof(QuickMarkupDefault.IsExplicitlyNull), true, typeof(QuickMarkupDefault))]
        [Rule(Terminal.Default, WITHPARAM, nameof(QuickMarkupDefault.IsExplicitlyNull), false, typeof(QuickMarkupDefault))]
        [Rule(ParsedTag, AS, VALUE, IDENTITY)]
        [Rule(NamedTag, AS, VALUE, IDENTITY)]
        [Rule(Terminal.QMOpenTagOpen, Terminal.QMOpenTagClose,
            QMChildren, AS, nameof(QuickMarkupQMs.Value),
            Terminal.QMCloseTagOpen, Terminal.QMCloseTagClose,
            typeof(QuickMarkupQMs)
        )]
        QMValue,
        // only for foreach loop due to ambiguity
        [Type<QuickMarkupValue>]
        [Rule(QMValue, AS, VALUE, IDENTITY)]
        [Rule(QMRange, AS, VALUE, IDENTITY)]
        QMIterable,
        [Type<QuickMarkupRange>]
        [Rule(Terminal.Integer, AS, nameof(QuickMarkupRange.Start),
              Terminal.Range,
              Terminal.Integer, AS, nameof(QuickMarkupRange.End),
              typeof(QuickMarkupRange))]
        [Rule(Terminal.Range,
              Terminal.Integer, AS, nameof(QuickMarkupRange.End),
              WITHPARAM, nameof(QuickMarkupRange.Start), 0,
              typeof(QuickMarkupRange))]
        QMRange,
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
    static string AddDot(string name)
        => $".{name}";
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
        return Parse(TerminalValues(), debug: Debugger.IsAttached);
    }
}