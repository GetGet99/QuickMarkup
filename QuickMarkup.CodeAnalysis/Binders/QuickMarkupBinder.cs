using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using QuickMarkup.Language.Symbols;
using System.Text.RegularExpressions;

namespace QuickMarkup.CodeAnalysis.Binders;

record class QMBinderTagInfo(ITypeSymbol? TagType, string TagName, string? ChildrenProperty, ITypeSymbol? ChildrenType, ChildrenModes ChildrenMode);
partial class QuickMarkupBinder(CodeTypeResolver resolver, bool failFast = true) : Binder(failFast)
{
    readonly QuickMarkupBinderUtilities utils = new(resolver);
    readonly Stack<string> scopedLocalNames = [];
    public QMNodeSymbol<ITypeSymbol?> Bind(QuickMarkupParsedTag tag, ITypeSymbol rootType) => BindPrivate(tag, rootType);
    QMNodeSymbol<ITypeSymbol?> Bind(QuickMarkupParsedTag tag) => BindPrivate(tag, null);
    QMNodeSymbol<ITypeSymbol?> BindPrivate(QuickMarkupParsedTag tag, ITypeSymbol? rootType)
    {
        if (tag.HasMismatchedEndTag)
            ErrorTagMismatched(tag.TagStart.TagName, tag.EndTagName!);
        if (rootType is not null && tag.TagStart.TagName is not "root")
            ErrorTagUnexpected(tag.TagStart, "root");
        var type = rootType ?? resolver.GetTypeSymbol(tag.TagStart.TagName);
        if (type is null)
            ErrorUnknownType(tag.TagStart);
        resolver.TryGetContentProperty(type, out var propSymbol, out var childrenMode);
        var childrenType = childrenMode is ChildrenModes.Add
            ? resolver.GetCollectionElementType(propSymbol?.Type)
            : propSymbol?.Type;
        var tagInfo = new QMBinderTagInfo(type, tag.TagStart.TagName, propSymbol?.Name, childrenType, childrenMode);


        var members = new List<IQMMemberSymbol>();
        Bind(tag.InlineMembers, tagInfo, members);

        Bind(tag.Children, tagInfo, members);
        return new(
            type,
            Bind((QuickMarkupConstructor)tag.TagStart, tagInfo),
            members,
            tag.Name
        );
    }
    QMConstructor Bind(QuickMarkupConstructor constructor, QMBinderTagInfo tagInfo)
    {
        var parameters = new List<IQMValueSymbol>(capacity: constructor.Parameters.Count);
        var objectConstructor = (tagInfo.TagType as INamedTypeSymbol)?.Constructors.FirstOrDefault(
            x => x.Parameters.Length == constructor.Parameters.Count
        );
        for (int i = 0; i < constructor.Parameters.Count; i++)
        {
            parameters.Add(Bind(
                constructor.Parameters[i],
                objectConstructor?.Parameters[i].Type,
                tagInfo
            ));
        }
        return new(constructor.TagIdentifier.Name, parameters, tagInfo.TagType is not null);
    }
    List<IQMMemberSymbol> Bind(ListAST<IQMNodeChild>? children, QMBinderTagInfo tagInfo)
    {
        List<IQMMemberSymbol> members = [];
        Bind(children, tagInfo, members);
        return members;
    }
    void Bind(ListAST<IQMNodeChild>? children, QMBinderTagInfo tagInfo, List<IQMMemberSymbol> targetCollection)
    {
        if (children is null) return;
        if (tagInfo.ChildrenMode is ChildrenModes.Assignment)
        {
            BindAssignmentChildren(children, tagInfo, targetCollection);
            return;
        }

        var pendingMembers = new List<IQMMemberSymbol>();
        foreach (var child in children)
        {
            if (TryBindPropertyTagChild(child, tagInfo, pendingMembers))
                continue;

            if (tagInfo.ChildrenMode is ChildrenModes.None)
            {
                ErrorChildrenTooMany((AST.AST)child, tagInfo);
                BindCollectionChildForDiagnostics(child, tagInfo);
                continue;
            }

            pendingMembers.Add(new QMAddChildMember<ITypeSymbol?>(
                tagInfo.ChildrenProperty!,
                BindCollectionChild(child, tagInfo),
                ChildElementType: tagInfo.ChildrenType
            ));
        }

        var lowering = pendingMembers.Any(RequiresStructuralLowering)
            ? ChildCollectionLowering.Blocks
            : ChildCollectionLowering.DirectAdd;

        foreach (var member in pendingMembers)
        {
            targetCollection.Add(member switch
            {
                QMAddChildMember<ITypeSymbol?> addChild => addChild with { CollectionLowering = lowering },
                _ => member
            });
        }
    }

    void BindAssignmentChildren(ListAST<IQMNodeChild> children, QMBinderTagInfo tagInfo, List<IQMMemberSymbol> targetCollection)
    {
        var contentChildren = new List<IQMNodeChild>();
        var hasPropertyChildren = false;
        foreach (var child in children)
        {
            if (TryBindPropertyTagChild(child, tagInfo, targetCollection))
            {
                hasPropertyChildren = true;
                continue;
            }

            contentChildren.Add(child);
        }

        if (contentChildren.Count == 0)
        {
            if (!hasPropertyChildren)
                ErrorChildrenTooMany(children, tagInfo);
            return;
        }

        if (contentChildren.Count != 1)
            ErrorChildrenTooMany(children, tagInfo);

        foreach (var extra in contentChildren.Skip(1))
            BindSingleChildNodeForDiagnostics(extra, tagInfo);

        targetCollection.Add(new QMAssignChildMember(
            tagInfo.ChildrenProperty!,
            BindSingleChildNode(contentChildren[0], tagInfo)
        ));
    }

    bool TryBindPropertyTagChild(IQMNodeChild child, QMBinderTagInfo tagInfo, List<IQMMemberSymbol> targetCollection)
    {
        if (child is not QuickMarkupParsedTag { TagStart: QuickMarkupPropertyTagStart tagStart } tag)
            return false;

        if (tag.HasMismatchedEndTag)
            ErrorTagMismatched(tag.TagStart.TagName, tag.EndTagName!);
        if (tag.InlineMembers.Count > 0)
            throw new NotImplementedException("Not supported now");
        if (tag.Children is { } tagChildren)
            Bind(new QuickMarkupParsedProperty(
                tagStart.TagName,
                ParsedPropertyOperator.Assign,
                new QuickMarkupValueList(tagChildren)
            ), tagInfo, targetCollection);
        return true;
    }

    IQMNodeChildSymbol BindCollectionChild(IQMNodeChild child, QMBinderTagInfo tagInfo)
    {
        return child switch
        {
            QuickMarkupParsedIfNode ifNode => BindCollectionIf(ifNode, tagInfo),
            QuickMarkupParsedForNode forNode => Bind(forNode, tagInfo),
            QuickMarkupParsedFragmentNode fragment => BindFragment(fragment, tagInfo),
            QuickMarkupParsedTag tag => Bind(tag),
            QuickMarkupValue val => Bind(val, tagInfo.ChildrenType, tagInfo),
            _ => throw new NotImplementedException($"Unsupported child node: {child.GetType().Name}")
        };
    }

    void BindCollectionChildForDiagnostics(IQMNodeChild child, QMBinderTagInfo tagInfo)
    {
        _ = BindCollectionChild(child, tagInfo);
    }

    IQMNodeChildSymbol BindSingleChildNode(IQMNodeChild child, QMBinderTagInfo tagInfo)
    {
        return child switch
        {
            QuickMarkupParsedIfNode ifNode => BindSingleChildIf(ifNode, tagInfo),
            QuickMarkupParsedForNode forNode => ErrorForNotAllowedInSingleChild(forNode, tagInfo),
            QuickMarkupParsedFragmentNode fragment => BindSingleChildFragment(fragment, tagInfo),
            QuickMarkupParsedTag tag => Bind(tag),
            QuickMarkupValue val => Bind(val, tagInfo.ChildrenType, tagInfo),
            _ => throw new NotImplementedException($"Unsupported child node: {child.GetType().Name}")
        };
    }

    void BindSingleChildNodeForDiagnostics(IQMNodeChild child, QMBinderTagInfo tagInfo)
    {
        _ = BindSingleChildNode(child, tagInfo);
    }

    QMForNodeSymbol<ITypeSymbol> Bind(QuickMarkupParsedForNode forNode, QMBinderTagInfo tagInfo)
    {
        var type = forNode.VarType is null ? null : resolver.GetTypeSymbol(forNode.VarType.Type);
        var iterable = Bind(forNode.Iterable, type, tagInfo);
        var kind = iterable is QMRangeSymbol
            ? QMForKind.StaticRange
            : QMForKind.ReactiveCollection;

        scopedLocalNames.Push(forNode.VarName);
        if (forNode.IndexVarName is not null)
            scopedLocalNames.Push(forNode.IndexVarName);
        if (forNode.Key is not null and not QuickMarkupForeign)
            ErrorForKeyMustBeForeign(forNode.Key);
        var key = forNode.Key is null ? null : Bind(forNode.Key, null, tagInfo);
        var body = BindStructuralBody(forNode.Body, tagInfo);
        if (forNode.IndexVarName is not null)
            scopedLocalNames.Pop();
        scopedLocalNames.Pop();

        return new(kind, type?.WithNullableAnnotation(
            forNode.VarType?.IsTypeNullable ?? false ?
                NullableAnnotation.Annotated :
                NullableAnnotation.NotAnnotated
            ), forNode.VarName, iterable, body, forNode.IndexVarName, key);
    }

    QMIfNodeSymbol<ITypeSymbol?> BindCollectionIf(QuickMarkupParsedIfNode ifNode, QMBinderTagInfo tagInfo)
        => new(
            BindCondition(ifNode, tagInfo),
            BindStructuralBody(ifNode.BodyWhenTrue, tagInfo),
            ifNode.BodyWhenFalse is null ? null : BindStructuralBody(ifNode.BodyWhenFalse, tagInfo)
        );

    QMConditionalValueSymbol<ITypeSymbol?> BindSingleChildIf(QuickMarkupParsedIfNode ifNode, QMBinderTagInfo tagInfo)
    {
        if (ifNode.BodyWhenFalse is null)
            ErrorSingleChildConditionalRequiresElse(ifNode);

        return new(
            BindCondition(ifNode, tagInfo),
            BindSingleChildBranch(ifNode.BodyWhenTrue, tagInfo),
            ifNode.BodyWhenFalse is null
                ? ErrorRecoveryChild(tagInfo)
                : BindSingleChildBranch(ifNode.BodyWhenFalse, tagInfo)
        );
    }

    List<IQMMemberSymbol> BindStructuralBody(IQMNodeChild body, QMBinderTagInfo tagInfo)
        => body is QuickMarkupParsedFragmentNode fragment
            ? Bind(fragment.Children, tagInfo)
            : Bind(new ListAST<IQMNodeChild>([body]), tagInfo);

    IQMNodeChildSymbol BindSingleChildBranch(IQMNodeChild child, QMBinderTagInfo tagInfo)
        => BindSingleChildNode(child, tagInfo);

    IQMNodeChildSymbol BindSingleChildFragment(QuickMarkupParsedFragmentNode fragment, QMBinderTagInfo tagInfo)
    {
        if (fragment.Children.Count != 1)
            ErrorSingleChildFragmentMustHaveExactlyOneChild(fragment, fragment.Children.Count);

        foreach (var extra in fragment.Children.Skip(1))
            BindSingleChildNodeForDiagnostics(extra, tagInfo);

        return fragment.Children.Count == 0
            ? ErrorRecoveryChild(tagInfo)
            : BindSingleChildNode(fragment.Children[0], tagInfo);
    }

    QMFragmentNodeSymbol BindFragment(QuickMarkupParsedFragmentNode fragment, QMBinderTagInfo tagInfo)
        => new(Bind(fragment.Children, tagInfo));

    IQMValueSymbol BindCondition(QuickMarkupParsedIfNode ifNode, QMBinderTagInfo tagInfo)
    {
        if (ifNode.Condition is QuickMarkupIdentifier)
        {
            ErrorConditionTypeInvalid(ifNode.Condition, null);
            return new QMValueSymbol<ITypeSymbol>(resolver.Boolean, "false");
        }

        var condition = ifNode.Condition is QuickMarkupForeign
            ? Bind(ifNode.Condition, resolver.Boolean, tagInfo)
            : Bind(ifNode.Condition, null, tagInfo);

        if (condition is QMValueSymbol<ITypeSymbol> { Type: { } type })
        {
            if (!SymbolEqualityComparer.Default.Equals(type, resolver.Boolean))
                ErrorConditionTypeInvalid(ifNode.Condition, type);
        }
        else
        {
            ErrorConditionTypeInvalid(ifNode.Condition, null);
        }

        return condition;
    }

    void Bind(ListAST<QuickMarkupInlineMember> properties, QMBinderTagInfo tagInfo, List<IQMMemberSymbol> targetCollection)
    {
        if (properties is null) return;
        foreach (var property in properties)
        {
            Bind(property, tagInfo, targetCollection);
        }
    }
    void Bind(QuickMarkupInlineMember inlineMember, QMBinderTagInfo tagInfo, List<IQMMemberSymbol> targetCollection)
    {
        if (inlineMember is QuickMarkupCallback cb)
        {
            targetCollection.Add(new QMCallbackMember<ITypeSymbol>(tagInfo.TagType, cb.Code));
            return;
        }
        if (inlineMember is not QuickMarkupParsedProperty property)
            throw new NotImplementedException();
        var targetPropSymbol = CodeTypeResolver.FindProperty(tagInfo.TagType, property.Key);
        var targetType = targetPropSymbol?.Type;
        switch (property.Operator)
        {
            case ParsedPropertyOperator.AddAssign:
                // event
                // <QM Click+=`(_, _) => ShowDialog("Clicked")` />
                var isShorthand = property.Key.StartsWith("@");
                var eventName = isShorthand ? property.Key[1..] : property.Key;
                var eventSymbol = CodeTypeResolver.FindEvent(tagInfo.TagType, eventName);
                targetCollection.Add(new QMAddEventMember<ITypeSymbol>(
                    eventSymbol?.Type, // type hint to null
                    eventName,
                    Bind(property.Value ?? throw new NotImplementedException(), null, tagInfo),
                    isShorthand
                ));
                break;
            case ParsedPropertyOperator.Assign:
                // Property
                if (property.Value is QuickMarkupValueList listAssign)
                {
                    // <Grid RowDefinitions=<>
                    //          <RowDefinition/>
                    //          <RowDefinition/>
                    //     </>
                    // </Grid>
                    if (targetPropSymbol?.Name is not { } name)
                        throw new InvalidOperationException("Name is null");
                    var elementType = resolver.GetCollectionElementType(targetType);
                    var childrenMode = elementType is null
                        ? ChildrenModes.Assignment
                        : ChildrenModes.Add;
                    Bind(listAssign.Value, new(
                        targetType,
                        name,
                        name,
                        childrenMode is ChildrenModes.Add ? elementType : targetType,
                        childrenMode
                    ), targetCollection);
                }
                else
                {
                    // <QM Value=`Target` />
                    targetCollection.Add(new QMAddPropertyMember<ITypeSymbol>(
                        targetType,
                        property.Key,
                        Bind(property.Value, targetType, tagInfo),
                        // treated as one way binding if it is foreign
                        // treated as assignment otherwise
                        property.Value is QuickMarkupForeign ?
                            BindingModes.SourceToTarget : BindingModes.OneTime
                    ));
                }
                break;
            case ParsedPropertyOperator.BindBack:
            case ParsedPropertyOperator.BindTwoWay:
                // <QM Value=>`Target` />
                // <QM Value<=>`Target` />
                string target;
                if (property.Value is QuickMarkupForeign foreign)
                    target = foreign.Code;
                else if (property.Value is QuickMarkupIdentifier identifier)
                    target = identifier.Identifier;
                else
                    throw new InvalidOperationException($"Bind back to {property.Value?.GetType().Name ?? "<null>"} is not supported");
                bool isDependencyProp;
                string depName = "";
                {
                    isDependencyProp = CodeTypeResolver.TryGetDependencyProperty(
                        tagInfo.TagType,
                        property.Key,
                        out var dependencyPropertyName);
                    depName = dependencyPropertyName ?? "";
                }
                targetCollection.Add(new QMAddPropertyMember<ITypeSymbol>(
                    targetType,
                    property.Key,
                    new QMValueSymbol<ITypeSymbol>(CodeTypeResolver.FindProperty(tagInfo.TagType, property.Key)?.Type, target),
                    property.Operator is ParsedPropertyOperator.BindBack ?
                        BindingModes.TargetToSource :
                        BindingModes.TwoWay,
                    isDependencyProp,
                    depName,
                    property.Key
                ));
                break;
            case ParsedPropertyOperator.None:
                // extension or boolean value
                if (CodeTypeResolver.FindProperty(tagInfo.TagType, property.Key) is { } propSymbol)
                {
                    // <QM IsEnabled />
                    targetCollection.Add(new QMAddPropertyMember<ITypeSymbol>(
                        targetType,
                        property.Key,
                        new QMValueSymbol<ITypeSymbol>(propSymbol.Type, "true"),
                        BindingModes.OneTime
                    ));
                }
                else
                {
                    // <QM Extension />
                    targetCollection.Add(new QMExtensionMember(property.Key));
                }
                break;
            default:
                throw new NotImplementedException();
        }
    }
    IQMValueSymbol Bind(QuickMarkupValue? value, ITypeSymbol? type, QMBinderTagInfo? tagInfo)
    {
        switch (value)
        {
            case QuickMarkupValueList x:
                if (tagInfo is null)
                    throw new NotImplementedException($"Value lists require a parent tag type");
                return new QMNestedValuesSymbol<ITypeSymbol>(type, Bind(x.Value, tagInfo));
            case QuickMarkupParsedTag x:
                return Bind(x);
            default:
                return AddCapturedLocalNames(value, utils.Bind(value, type));
        }
        ;
    }

    IQMValueSymbol AddCapturedLocalNames(QuickMarkupValue? value, IQMValueSymbol symbol)
    {
        if (value is not QuickMarkupForeign foreign ||
            symbol is not QMValueSymbol<ITypeSymbol> valueSymbol ||
            scopedLocalNames.Count == 0)
            return symbol;

        List<string>? captures = null;
        foreach (var name in scopedLocalNames)
        {
            if (!Regex.IsMatch(foreign.Code, $@"\b{Regex.Escape(name)}\b"))
                continue;

            captures ??= [];
            captures.Add(name);
        }

        return captures is null
            ? symbol
            : valueSymbol with { CapturedLocalNames = captures };
    }
    void ErrorUnknownType(PositionedIdentifier identifier)
        => Error(new QMBinderTypeUnknownError(identifier, identifier.Name));
    void ErrorTagMismatched(string tagStartName, PositionedIdentifier endTag)
        => Error(new QMBinderTagMismatchedError(endTag, tagStartName, endTag.Name));
    void ErrorTagUnexpected(ITagStart tagStart, string expectedTag)
        => Error(new QMBinderTagMismatchedError((AST.AST)tagStart, tagStart.TagName, expectedTag));
    void ErrorUnknownType(ITagStart tagStart)
        => Error(new QMBinderTypeUnknownError((AST.AST)tagStart, tagStart.TagName));
    void ErrorChildrenTooMany(AST.AST node, QMBinderTagInfo parentTagInfo)
        => Error(new QMBinderChildrenTooMany(node, parentTagInfo));
    void ErrorSingleChildConditionalRequiresElse(QuickMarkupParsedIfNode node)
        => Error(node, "Single-child conditional content requires an else branch.");
    void ErrorConditionTypeInvalid(QuickMarkupValue node, ITypeSymbol? actualType)
        => Error(node, $"if condition must be bool, but got {actualType?.FullNameWithoutAnnotation() ?? "unknown"}.");
    void ErrorForKeyMustBeForeign(QuickMarkupValue node)
        => Error(node, "foreach key must be a C# literal expression.");
    void ErrorSingleChildFragmentMustHaveExactlyOneChild(QuickMarkupParsedFragmentNode node, int actualCount)
        => Error(node, $"Single-child fragment must contain exactly one child, but got {actualCount}.");
    IQMNodeChildSymbol ErrorForNotAllowedInSingleChild(QuickMarkupParsedForNode node, QMBinderTagInfo tagInfo)
    {
        Error(node, "foreach is not allowed in a single-child content position.");
        _ = Bind(node, tagInfo);
        return ErrorRecoveryChild(tagInfo);
    }
    IQMNodeChildSymbol ErrorRecoveryChild(QMBinderTagInfo tagInfo)
        => new QMValueSymbol<ITypeSymbol?>(tagInfo.ChildrenType, "default");

    bool RequiresStructuralLowering(IQMMemberSymbol member)
        => member switch
        {
            QMAddChildMember<ITypeSymbol?> addChild => RequiresStructuralLowering(addChild.Child),
            QMAssignChildMember assignChild => RequiresStructuralLowering(assignChild.Child),
            _ => false
        };

    bool RequiresStructuralLowering(IQMNodeChildSymbol child)
        => child switch
        {
            QMIfNodeSymbol<ITypeSymbol?> => true,
            QMConditionalValueSymbol<ITypeSymbol?> => true,
            QMFragmentNodeSymbol => true,
            QMForNodeSymbol<ITypeSymbol> { Kind: QMForKind.ReactiveCollection } => true,
            QMForNodeSymbol<ITypeSymbol> { Kind: QMForKind.StaticRange } forNode => ContainsStructuralChildren(forNode.Body),
            _ => false
        };

    bool ContainsStructuralChildren(IReadOnlyList<IQMMemberSymbol> members)
        => members.Any(RequiresStructuralLowering);
}
