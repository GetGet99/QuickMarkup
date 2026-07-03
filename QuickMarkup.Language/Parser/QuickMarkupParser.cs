using Get.Parser;
using Get.PLShared;
using QuickMarkup.AST;
using QuickMarkup.Language.Symbols;
using System.Diagnostics;
using static QuickMarkup.Parser.QuickMarkupParser.NonTerminal;
using NonTerminal = QuickMarkup.Parser.QuickMarkupParser.NonTerminal;
using Terminal = QuickMarkup.Parser.QuickMarkupLexer.Tokens;

namespace QuickMarkup.Parser;

[Parser(SFC, UseGetLexerTypeInformation = true)]
[Precedence(Terminal.Else, Associativity.Right)]
public partial class QuickMarkupParser : ParserBase<Terminal, NonTerminal, QuickMarkupSFC>
{
    public enum NonTerminal
    {
        // SFC LEVEL SETUP
        [Type<QuickMarkupSFC>]
        [Rule(
            UsingStatementsOrEmpty, AS, nameof(QuickMarkupSFC.Usings),
            NamespaceDecl, AS, nameof(QuickMarkupSFC.Namespace),
            ClassDecl, AS, nameof(QuickMarkupSFC.ClassDeclaration),
            RefsDecl, AS, nameof(QuickMarkupSFC.Refs),
            typeof(QuickMarkupSFC))]
        [Rule(SFC, AS, LIST, SFCTag, AS, VALUE, APPENDLIST)]
        SFC,
        // HEADER: Namespace declaration
        [Type<PositionedIdentifier>]
        [Rule(NamespaceDeclInner, AS, VALUE, IDENTITY)]
        [Rule(WITHPARAM, VALUE, null, IDENTITY)]
        NamespaceDecl,
        [Type<PositionedIdentifier>]
        [Rule(Terminal.NamespaceKw, NsIdentifier, AS, VALUE, Terminal.NamespaceSemicolon, IDENTITY)]
        NamespaceDeclInner,
        [Type<PositionedIdentifier>]
        [Rule(Terminal.HeaderIdentifier, AS, nameof(PositionedIdentifier.Name), typeof(PositionedIdentifier))]
        [Rule(NsIdentifier, AS, "prev", Terminal.HeaderDot, Terminal.HeaderIdentifier, AS, "id", nameof(AppendNs))]
        NsIdentifier,
        // HEADER: Class declaration
        [Type<ClassDeclaration>]
        [Rule(ClassDeclInner, AS, VALUE, IDENTITY)]
        [Rule(WITHPARAM, VALUE, null, IDENTITY)]
        ClassDecl,
        [Type<ClassDeclaration>]
        [Rule(ClassKindPrefix, AS, "kind",
            Terminal.HeaderIdentifier, AS, "name",
            Terminal.ClassSemicolon,
            nameof(MakeClassDeclNoBase))]
        [Rule(ClassKindPrefix, AS, "kind",
            Terminal.HeaderIdentifier, AS, "name",
            Terminal.ClassColon,
            Terminal.RawBaseTypes, AS, "baseTypes",
            Terminal.BaseTypesSemicolon,
            nameof(MakeClassDeclWithBase))]
        ClassDeclInner,
        [Type<ClassKind>]
        [Rule(Terminal.ClassKw, WITHPARAM, VALUE, ClassKind.Subclass, IDENTITY)]
        [Rule(Terminal.ComponentKw, WITHPARAM, VALUE, ClassKind.Component, IDENTITY)]
        [Rule(Terminal.FragmentKw, Terminal.ComponentKw, WITHPARAM, VALUE, ClassKind.FragmentComponent, IDENTITY)]
        ClassKindPrefix,
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
        [Rule(RefsDecl, AS, VALUE, ERROR, IDENTITY)]
        RefsDecl,
        // Ref compile-time attributes (stacked [...][...] blocks before each declaration)
        [Type<ListAST<QMAttribute>>]
        [Rule(EMPTYLIST)]
        [Rule(NonEmptyRefAttributes, AS, VALUE, IDENTITY)]
        RefAttributes,
        [Type<ListAST<QMAttribute>>]
        [Rule(Terminal.OpenSquareBracket, RefAttrAppsInner, AS, VALUE, Terminal.CloseSquareBracket, IDENTITY)]
        [Rule(NonEmptyRefAttributes, AS, "prev", Terminal.OpenSquareBracket, RefAttrAppsInner, AS, "more", Terminal.CloseSquareBracket, nameof(AppendRefAttributeSections))]
        NonEmptyRefAttributes,
        [Type<ListAST<QMAttribute>>]
        [Rule(RefAttrApp, AS, VALUE, SINGLELIST)]
        [Rule(RefAttrAppsInner, AS, LIST, Terminal.Comma, RefAttrApp, AS, VALUE, APPENDLIST)]
        [Rule(RefAttrAppsInner, AS, VALUE, ERROR, IDENTITY)]
        RefAttrAppsInner,
        [Type<QMAttribute>]
        [Rule(QMPositionedIdentifier, AS, nameof(QMAttribute.TargetSpecifier),
            Terminal.Colon,
            QMPositionedIdentifier, AS, nameof(QMAttribute.AttributeName),
            RefAttrOptArgs, AS, nameof(QMAttribute.Arguments),
            typeof(QMAttribute))]
        [Rule(QMPositionedIdentifier, AS, nameof(QMAttribute.AttributeName),
            RefAttrOptArgs, AS, nameof(QMAttribute.Arguments),
            typeof(QMAttribute))]
        RefAttrApp,
        [Type<QMCompileTimeAttributeArguments>]
        [Rule(Terminal.OpenBracket, RefAttrArgsInner, AS, VALUE, Terminal.CloseBracket, IDENTITY)]
        [Rule(Terminal.OpenBracket, Terminal.CloseBracket, typeof(QMCompileTimeAttributeArguments))]
        [Rule(typeof(QMCompileTimeAttributeArguments))]
        RefAttrOptArgs,
        [Type<QMCompileTimeAttributeArguments>]
        [Rule(RefAttrNamedListInner, AS, nameof(QMCompileTimeAttributeArguments.Named), typeof(QMCompileTimeAttributeArguments))]
        [Rule(RefAttrPositionalListInner, AS, nameof(QMCompileTimeAttributeArguments.Positionals),
            RefAttrNamedTailOpt, AS, nameof(QMCompileTimeAttributeArguments.Named),
            typeof(QMCompileTimeAttributeArguments))]
        RefAttrArgsInner,
        [Type<ListAST<QuickMarkupValue>>]
        [Rule(QMValueWithoutNamedTag, AS, VALUE, SINGLELIST)]
        [Rule(RefAttrPositionalListInner, AS, LIST, Terminal.Comma, QMValueWithoutNamedTag, AS, VALUE, APPENDLIST)]
        [Rule(RefAttrPositionalListInner, AS, VALUE, ERROR, IDENTITY)]
        RefAttrPositionalListInner,
        [Type<ListAST<QMAttributeNamedArgument>>]
        [Rule(EMPTYLIST)]
        [Rule(Terminal.Comma, RefAttrNamedListInner, AS, VALUE, IDENTITY)]
        RefAttrNamedTailOpt,
        [Type<ListAST<QMAttributeNamedArgument>>]
        [Rule(RefAttrNamedArg, AS, VALUE, SINGLELIST)]
        [Rule(RefAttrNamedListInner, AS, LIST, Terminal.Comma, RefAttrNamedArg, AS, VALUE, APPENDLIST)]
        RefAttrNamedListInner,
        [Type<QMAttributeNamedArgument>]
        [Rule(QMPositionedIdentifier, AS, nameof(QMAttributeNamedArgument.Name),
            Terminal.Equal,
            QMValueWithoutNamedTag, AS, nameof(QMAttributeNamedArgument.Value),
            typeof(QMAttributeNamedArgument))]
        RefAttrNamedArg,
        [Type<RefDeclaration>]
        [Rule(
            RefAttributes, AS, nameof(RefDeclaration.Attributes),
            RefKind, AS, nameof(RefDeclaration.Kind),
            RefAccessibility, AS, nameof(RefDeclaration.Accessibility),
            RefStaticVisibility, AS, nameof(RefDeclaration.IsStatic),
            RefRequiredVisibility, AS, nameof(RefDeclaration.IsRequired),
            TypeDecl, AS, nameof(RefDeclaration.Type),
            QMPositionedIdentifier, AS, nameof(RefDeclaration.Name),
            RefDeclInitialValue, AS, nameof(RefDeclaration.DefaultValue),
            Terminal.Semicolon,
            WITHPARAM, nameof(RefDeclaration.IsComputedDeclaration), false,
            typeof(RefDeclaration)
        )]
        [Rule(
            RefAttributes, AS, nameof(RefDeclaration.Attributes),
            RefKind, AS, nameof(RefDeclaration.Kind),
            RefAccessibility, AS, nameof(RefDeclaration.Accessibility),
            RefStaticVisibility, AS, nameof(RefDeclaration.IsStatic),
            RefRequiredVisibility, AS, nameof(RefDeclaration.IsRequired),
            TypeDecl, AS, nameof(RefDeclaration.Type),
            QMPositionedIdentifier, AS, nameof(RefDeclaration.Name),
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
        [Type<Accessibility>]
        [Rule(WITHPARAM, VALUE, Accessibility.Default, IDENTITY)]
        [Rule(Terminal.Private, WITHPARAM, VALUE, Accessibility.Private, IDENTITY)]
        [Rule(Terminal.Public, WITHPARAM, VALUE, Accessibility.Public, IDENTITY)]
        RefAccessibility,
        [Type<bool>]
        [Rule(WITHPARAM, VALUE, false, IDENTITY)]
        [Rule(Terminal.Static, WITHPARAM, VALUE, true, IDENTITY)]
        RefStaticVisibility,
        [Type<bool>]
        [Rule(WITHPARAM, VALUE, false, IDENTITY)]
        [Rule(Terminal.Required, WITHPARAM, VALUE, true, IDENTITY)]
        RefRequiredVisibility,
        [Type<RefDeclarationKind>]
        [Rule(Terminal.Inject, WITHPARAM, VALUE, RefDeclarationKind.Inject, IDENTITY)]
        [Rule(Terminal.Inject, Terminal.QuestionMark,WITHPARAM, VALUE, RefDeclarationKind.InjectOptional, IDENTITY)]
        [Rule(Terminal.Provide, WITHPARAM, VALUE, RefDeclarationKind.Provide, IDENTITY)]
        [Rule(WITHPARAM, VALUE, RefDeclarationKind.Ref, IDENTITY)]
        RefKind,
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
        ParsedTagBeforeValidate,
        [Type<QuickMarkupParsedTag>]
        [Rule(ParsedTagBeforeValidate, AS, "tag", nameof(ValidateTag))]
        ParsedTag,
        [Type<QuickMarkupParsedTag>]
        [Rule(
            Terminal.Identifier, AS, "name",
            Terminal.Equal,
            ParsedTag, AS, "tag",
            nameof(AttachName)
        )]
        [Rule(
            Terminal.Ref,
            Terminal.Identifier, AS, "name",
            Terminal.Equal,
            ParsedTag, AS, "tag",
            nameof(AttachRefName)
        )]
        NamedTag,
        // CONSTRUCTOR
        [Type<QuickMarkupConstructor>]
        [Rule(QMPositionedIdentifier, AS, nameof(QuickMarkupConstructor.TagIdentifier), typeof(QuickMarkupConstructor))]
        [Rule(QMPositionedIdentifier, AS, nameof(QuickMarkupConstructor.TagIdentifier), Terminal.OpenBracket, QMConstructorParameters, AS, nameof(QuickMarkupConstructor.Parameters), Terminal.CloseBracket, typeof(QuickMarkupConstructor))]
        QMConstructor,
        [Type<ListAST<QuickMarkupValue>>]
        [Rule(EMPTYLIST)]
        [Rule(QMConstructorParametersInside, AS, VALUE, IDENTITY)]
        QMConstructorParameters,
        [Type<ListAST<QuickMarkupValue>>]
        [Rule(QMValue, AS, VALUE, SINGLELIST)]
        [Rule(QMConstructorParametersInside, AS, LIST, Terminal.Comma, QMValue, AS, VALUE, APPENDLIST)]
        [Rule(QMConstructorParametersInside, AS, VALUE, ERROR, IDENTITY)]
        QMConstructorParametersInside,
        // TAGSTART/TAGEND HELPER
        [Type<ITagStart>]
        [Rule(Terminal.Dot, Terminal.Identifier, AS, nameof(QuickMarkupPropertyTagStart.TagName), typeof(QuickMarkupPropertyTagStart))]
        [Rule(Terminal.Identifier, AS, nameof(QuickMarkupAttachedPropertyTagStart.TypeName),
              Terminal.Dot,
              Terminal.Identifier, AS, nameof(QuickMarkupAttachedPropertyTagStart.PropertyName),
              typeof(QuickMarkupAttachedPropertyTagStart))]
        [Rule(QMConstructor, AS, VALUE, IDENTITY)]
        ParsedTagStart,
        [Type<PositionedIdentifier>]
        [Rule(QMPositionedIdentifier, AS, VALUE, IDENTITY)]
        [Rule(Terminal.Dot, Terminal.Identifier, AS, "name", nameof(AddDot))]
        [Rule(Terminal.Identifier, AS, "typeName", Terminal.Dot, Terminal.Identifier, AS, "tagName", nameof(AddDotted))]
        ParsedTagEnd,
        // PROPERTIES
        [Type<ParsedPropertyOperator>]
        [Rule(Terminal.Equal, WITHPARAM, VALUE, ParsedPropertyOperator.Assign, IDENTITY)]
        [Rule(Terminal.EqualArrowRight, WITHPARAM, VALUE, ParsedPropertyOperator.BindBack, IDENTITY)]
        [Rule(Terminal.EqualArrowLeftRight, WITHPARAM, VALUE, ParsedPropertyOperator.BindTwoWay, IDENTITY)]
        [Rule(Terminal.AddEqual, WITHPARAM, VALUE, ParsedPropertyOperator.AddAssign, IDENTITY)]
        PropertyOperator,
        [Type<QuickMarkupInlineMember>]
        [Rule(
            Terminal.Identifier, AS, "typeName",
            Terminal.Dot,
            Terminal.Identifier, AS, "propName",
            PropertyOperator, AS, "op",
            QMValue, AS, "value",
            nameof(MakeAttachedProperty)
        )]
        [Rule(
            Terminal.Identifier, AS, nameof(QuickMarkupParsedProperty.Key),
            PropertyOperator, AS, nameof(QuickMarkupParsedProperty.Operator),
            QMValue, AS, nameof(QuickMarkupParsedProperty.Value),
            typeof(QuickMarkupParsedProperty)
        )]
        [Rule(
            Terminal.Foreign, AS, nameof(QuickMarkupParsedProperty.Key),
            PropertyOperator, AS, nameof(QuickMarkupParsedProperty.Operator),
            QMValue, AS, nameof(QuickMarkupParsedProperty.Value),
            WITHPARAM, nameof(QuickMarkupParsedProperty.IsKeyForeign), true,
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
            Terminal.Identifier, AS, "typeName",
            Terminal.Dot,
            Terminal.Identifier, AS, "propName",
            nameof(MakeNegatedAttachedProperty)
        )]
        [Rule(
            Terminal.Not,
            Terminal.Identifier, AS, nameof(QuickMarkupParsedProperty.Key),
            WITHPARAM, nameof(QuickMarkupParsedProperty.Operator), ParsedPropertyOperator.Assign,
            WITHPARAM, nameof(QuickMarkupParsedProperty.Value), false,
            typeof(QuickMarkupParsedProperty)
        )]
        [Rule(
            Terminal.Not,
            Terminal.Foreign, AS, nameof(QuickMarkupParsedProperty.Key),
            WITHPARAM, nameof(QuickMarkupParsedProperty.Operator), ParsedPropertyOperator.Assign,
            WITHPARAM, nameof(QuickMarkupParsedProperty.Value), false,
            WITHPARAM, nameof(QuickMarkupParsedProperty.IsKeyForeign), true,
            typeof(QuickMarkupParsedProperty)
        )]
        InlineMember,
        [Type<ListAST<QuickMarkupInlineMember>>]
        [Rule(InlineMember, AS, VALUE, SINGLELIST)]
        [Rule(InlineMembersInner, AS, LIST, InlineMember, AS, VALUE, APPENDLIST)]
        [Rule(InlineMembersInner, AS, VALUE, ERROR, IDENTITY)]
        InlineMembersInner,
        [Type<ListAST<QuickMarkupInlineMember>>]
        [Rule(EMPTYLIST)]
        [Rule(InlineMembersInner, AS, VALUE, IDENTITY)]
        InlineMembers,
        [Type<ListAST<IQMNodeChild>>]
        [Rule(EMPTYLIST)]
        [Rule(QMChildren, AS, LIST, QMChild, AS, VALUE, APPENDLIST)]
        [Rule(QMChildren, AS, VALUE, ERROR, IDENTITY)]
        QMChildren,
        [Type<IQMNodeChild>]
        [Rule(ParsedIfNode, AS, VALUE, IDENTITY, WITHPRECDENCE, Terminal.Else)]
        [Rule(ParsedForNode, AS, VALUE, IDENTITY)]
        [Rule(ParsedFragmentNode, AS, VALUE, IDENTITY)]
        [Rule(QMValue, AS, VALUE, IDENTITY)]
        QMChild,
        [Type<IQMNodeChild>]
        [Rule(MatchedStructuralBody, AS, VALUE, IDENTITY, WITHPRECDENCE, Terminal.Else)]
        [Rule(UnmatchedIf, AS, VALUE, IDENTITY, WITHPRECDENCE, Terminal.Else)]
        StructuralBody,
        [Type<IQMNodeChild>]
        [Rule(ParsedFragmentNode, AS, VALUE, IDENTITY)]
        [Rule(ParsedForNode, AS, VALUE, IDENTITY)]
        [Rule(QMValue, AS, VALUE, IDENTITY)]
        [Rule(MatchedIf, AS, VALUE, IDENTITY)]
        MatchedStructuralBody,
        [Type<QuickMarkupParsedForNode>]
        [Rule(
            Terminal.Foreach,
            Terminal.OpenBracket,
            ParsedForHeader, AS, "header",
            Terminal.CloseBracket,
            StructuralBody, AS, "body",
            nameof(CreateForNode)
        )]
        ParsedForNode,
        [Type<ParsedForHeader>]
        [Rule(
            TypeDeclOrVarKeyword, AS, "VarType",
            Terminal.Identifier, AS, "VarName",
            Terminal.In,
            QMIterable, AS, "Iterable",
            WITHPARAM, "IndexVarName", null,
            WITHPARAM, "Key", null,
            typeof(ParsedForHeader)
        )]
        [Rule(
            Terminal.Identifier, AS, "IndexVarName",
            Terminal.Semicolon,
            TypeDeclOrVarKeyword, AS, "VarType",
            Terminal.Identifier, AS, "VarName",
            Terminal.In,
            QMIterable, AS, "Iterable",
            WITHPARAM, "Key", null,
            typeof(ParsedForHeader)
        )]
        [Rule(
            TypeDeclOrVarKeyword, AS, "VarType",
            Terminal.Identifier, AS, "VarName",
            Terminal.In,
            QMIterable, AS, "Iterable",
            Terminal.Semicolon,
            QMValue, AS, "Key",
            WITHPARAM, "IndexVarName", null,
            typeof(ParsedForHeader)
        )]
        [Rule(
            Terminal.Identifier, AS, "IndexVarName",
            Terminal.Semicolon,
            TypeDeclOrVarKeyword, AS, "VarType",
            Terminal.Identifier, AS, "VarName",
            Terminal.In,
            QMIterable, AS, "Iterable",
            Terminal.Semicolon,
            QMValue, AS, "Key",
            typeof(ParsedForHeader)
        )]
        ParsedForHeader,
        [Type<QuickMarkupParsedIfNode>]
        [Rule(MatchedIf, AS, VALUE, IDENTITY, WITHPRECDENCE, Terminal.Else)]
        [Rule(UnmatchedIf, AS, VALUE, IDENTITY, WITHPRECDENCE, Terminal.Else)]
        ParsedIfNode,
        [Type<QuickMarkupParsedIfNode>]
        [Rule(
            Terminal.If,
            Terminal.OpenBracket,
            QMValue, AS, nameof(QuickMarkupParsedIfNode.Condition),
            Terminal.CloseBracket,
            MatchedStructuralBody, AS, nameof(QuickMarkupParsedIfNode.BodyWhenTrue),
            Terminal.Else,
            MatchedStructuralBody, AS, nameof(QuickMarkupParsedIfNode.BodyWhenFalse),
            typeof(QuickMarkupParsedIfNode),
            WITHPRECDENCE,
            Terminal.Else
        )]
        MatchedIf,
        [Type<QuickMarkupParsedIfNode>]
        [Rule(
            Terminal.If,
            Terminal.OpenBracket,
            QMValue, AS, nameof(QuickMarkupParsedIfNode.Condition),
            Terminal.CloseBracket,
            StructuralBody, AS, nameof(QuickMarkupParsedIfNode.BodyWhenTrue),
            WITHPARAM, nameof(QuickMarkupParsedIfNode.BodyWhenFalse), null,
            typeof(QuickMarkupParsedIfNode),
            WITHPRECDENCE,
            Terminal.Else
        )]
        [Rule(
            Terminal.If,
            Terminal.OpenBracket,
            QMValue, AS, nameof(QuickMarkupParsedIfNode.Condition),
            Terminal.CloseBracket,
            MatchedStructuralBody, AS, nameof(QuickMarkupParsedIfNode.BodyWhenTrue),
            Terminal.Else,
            UnmatchedIf, AS, nameof(QuickMarkupParsedIfNode.BodyWhenFalse),
            typeof(QuickMarkupParsedIfNode),
            WITHPRECDENCE,
            Terminal.Else
        )]
        UnmatchedIf,
        [Type<QuickMarkupParsedFragmentNode>]
        [Rule(
            Terminal.OpenCuryBracket,
            QMChildren, AS, nameof(QuickMarkupParsedFragmentNode.Children),
            Terminal.CloseCuryBracket,
            typeof(QuickMarkupParsedFragmentNode)
        )]
        ParsedFragmentNode,
        [Type<PositionedIdentifier>]
        [Rule(Terminal.Identifier, AS, nameof(PositionedIdentifier.Name), typeof(PositionedIdentifier))]
        QMPositionedIdentifier,
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
        [Rule(Terminal.QMOpenTagOpen, Terminal.QMOpenTagClose,
            QMChildren, AS, nameof(QuickMarkupValueList.Value),
            Terminal.QMCloseTagOpen, Terminal.QMCloseTagClose,
            typeof(QuickMarkupValueList))]
        QMValueWithoutNamedTag,
        [Type<QuickMarkupValue>]
        [Rule(QMValueWithoutNamedTag, AS, VALUE, IDENTITY)]
        [Rule(NamedTag, AS, VALUE, IDENTITY)]
        QMValue,
        // only for foreach loop due to ambiguity
        [Type<QuickMarkupValue>]
        [Rule(QMValue, AS, VALUE, IDENTITY)]
        [Rule(QMRange, AS, VALUE, IDENTITY)]
        QMIterable,
        [Type<QuickMarkupRange>]
        [Rule(Terminal.Integer, AS, nameof(QuickMarkupRange.RangeStart),
              Terminal.Range,
              Terminal.Integer, AS, nameof(QuickMarkupRange.RangeEnd),
              typeof(QuickMarkupRange))]
        [Rule(Terminal.Range,
              Terminal.Integer, AS, nameof(QuickMarkupRange.RangeEnd),
              WITHPARAM, nameof(QuickMarkupRange.RangeStart), 0,
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
        TypeDeclOrVarKeyword,
    }
    record class ParsedForHeader(
        TypeDeclaration? VarType,
        string VarName,
        QuickMarkupValue Iterable,
        string? IndexVarName,
        QuickMarkupValue? Key);
    static QuickMarkupParsedForNode CreateForNode(ParsedForHeader header, IQMNodeChild body)
        => new(header.VarType, header.VarName, header.Iterable, body, header.IndexVarName, header.Key);
    static QuickMarkupParsedTag AttachName(string name, QuickMarkupParsedTag tag)
        => tag with { Name = name };
    static QuickMarkupParsedTag AttachRefName(string name, QuickMarkupParsedTag tag)
        => tag with { Name = name, IsRef = true };
    static PositionedIdentifier AddDot(string name)
        => new($".{name}");
    static PositionedIdentifier AddDotted(string typeName, string tagName)
        => new($"{typeName}.{tagName}");
    static string CombineDottedPropertyKey(string typeName, string propName)
        => $"{typeName}.{propName}";
    static QuickMarkupParsedProperty MakeAttachedProperty(string typeName, string propName, ParsedPropertyOperator op, QuickMarkupValue? value)
        => new($"{typeName}.{propName}", op, value, IsAttachedPropertyKey: true);
    static QuickMarkupParsedProperty MakeNegatedAttachedProperty(string typeName, string propName)
        => new(
            $"{typeName}.{propName}",
            ParsedPropertyOperator.Assign,
            new QuickMarkupBoolean(false),
            IsAttachedPropertyKey: true
        );
    static QuickMarkupParsedTag ValidateTag(QuickMarkupParsedTag tag)
    {
        if (tag.HasMismatchedEndTag)
        {
            throw new QuickMarkupTagMismatchException(tag);
        }
        return tag;
    }
    static PositionedIdentifier AppendNs(PositionedIdentifier prev, string id)
        => new($"{prev.Name}.{id}");
    static ClassDeclaration MakeClassDeclNoBase(ClassKind kind, string name)
        => new(name, kind, null);
    static ClassDeclaration MakeClassDeclWithBase(ClassKind kind, string name, string baseTypes)
        => new(name, kind, baseTypes.TrimStart());
    static string CombineUsings(string A, string B)
    {
        return $"""
            {A}
            {B}
            """;
    }
    static ListAST<QMAttribute> AppendRefAttributeSections(ListAST<QMAttribute> prev, ListAST<QMAttribute> more)
    {
        foreach (var x in more)
            prev.Add(x);
        return prev;
    }
    public QuickMarkupSFC Parse(IEnumerable<IToken<Terminal>> inputTerminals, out List<ErrorTerminalValue> handledErrors)
    {
        handledErrors = [];
        IEnumerable<ITerminalValue> TerminalValues()
        {
            foreach (var inputTerminal in inputTerminals)
            {
                // Console.WriteLine($"Reading Terminal: {inputTerminal.TokenType} ({inputTerminal.Start} - {inputTerminal.End})");
                if (inputTerminal is IToken<Terminal, int> intTok)
                    yield return CreateValue(inputTerminal.TokenType, intTok.Data, inputTerminal.Start, inputTerminal.End);
                else if (inputTerminal is IToken<Terminal, double> doubleTok)
                    yield return CreateValue(inputTerminal.TokenType, doubleTok.Data, inputTerminal.Start, inputTerminal.End);
                else if (inputTerminal is IToken<Terminal, bool> boolTok)
                    yield return CreateValue(inputTerminal.TokenType, boolTok.Data, inputTerminal.Start, inputTerminal.End);
                else if (inputTerminal is IToken<Terminal, string> strTok)
                    yield return CreateValue(inputTerminal.TokenType, strTok.Data, inputTerminal.Start, inputTerminal.End);
                else
                    yield return CreateValue(inputTerminal.TokenType, inputTerminal.Start, inputTerminal.End);
            }
        }
        return Parse(TerminalValues(), debug: false, handledErrors: handledErrors, skipErrorHandling: false);
    }
}
class QuickMarkupTagMismatchException(QuickMarkupParsedTag tag) : Exception
{
    public QuickMarkupParsedTag FaultedTag { get; } = tag;
}
