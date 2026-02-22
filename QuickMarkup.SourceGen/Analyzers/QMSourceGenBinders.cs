using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using QuickMarkup.Language.Symbols;
using System.Xml.Linq;

namespace QuickMarkup.SourceGen.Analyzers;

record class QMBinderTagInfo(ITypeSymbol? TagType, string TagName, string? ChildrenProperty, ITypeSymbol? ChildrenType, ChildrenModes ChildrenMode);
partial class QMSourceGenBinders(CodeGenTypeResolver resolver, bool failFast = true)
{
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
        var tagInfo = new QMBinderTagInfo(type, tag.TagStart.TagName, propSymbol?.Name, propSymbol?.Type, childrenMode);


        var members = new List<IQMMemberSymbol>();
        Bind(tag.InlineMembers, tagInfo, members);

        var propertiesCount = members.Count;
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
        return new(constructor.TagName, parameters, tagInfo.TagType is not null);
    }
    List<IQMMemberSymbol> Bind(ListAST<IQMNodeChild>? children, QMBinderTagInfo tagInfo)
    {
        List<IQMMemberSymbol> members = [];
        Bind(children, tagInfo, members);
        return members;
    }
    void Bind(ListAST<IQMNodeChild>? children, QMBinderTagInfo tagInfo, List<IQMMemberSymbol> targetCollection)
    {
        var childrenMode = tagInfo.ChildrenMode;
        if (children is null) return;
        foreach (var child in children)
        {
            switch (child)
            {
                case QuickMarkupParsedTag tag:
                    if (tag.TagStart is QuickMarkupPropertyTagStart tagStart)
                    {
                        if (tag.HasMismatchedEndTag)
                            ErrorTagMismatched(tag.TagStart.TagName, tag.EndTagName!);
                        if (tag.InlineMembers.Count > 0)
                            throw new NotImplementedException("Not supported now");
                        if (tag.Children is { } tagChildren)
                            Bind(new QuickMarkupParsedProperty(
                                tagStart.TagName,
                                ParsedPropertyOperator.Assign,
                                new QuickMarkupQMs(tagChildren)
                            ), tagInfo, targetCollection);
                        break;
                    }
                    if (childrenMode is ChildrenModes.None)
                        ErrorChildrenTooMany(tag, tagInfo);
                    if (childrenMode is ChildrenModes.Assignment)
                    {
                        targetCollection.Add(new QMAssignChildMember(tagInfo.ChildrenProperty!, Bind(tag)));
                        childrenMode = ChildrenModes.None;
                    }
                    else
                    {
                        targetCollection.Add(new QMAddChildMember($"{tagInfo.ChildrenProperty!}.Add", Bind(tag)));
                    }
                    break;
                case QuickMarkupValue val:
                    if (childrenMode is ChildrenModes.None)
                        ErrorChildrenTooMany(val, tagInfo);
                    if (childrenMode is ChildrenModes.Assignment)
                    {
                        targetCollection.Add(new QMAssignChildMember(tagInfo.ChildrenProperty!, Bind(val, tagInfo.ChildrenType, tagInfo)));
                        childrenMode = ChildrenModes.None;
                    }
                    else
                    {
                        targetCollection.Add(new QMAddChildMember($"{tagInfo.ChildrenProperty!}.Add", Bind(val, tagInfo.ChildrenType, tagInfo)));
                    }
                    break;
                case QuickMarkupParsedForNode forNode:
                    if (childrenMode is ChildrenModes.None or ChildrenModes.Assignment)
                        ErrorChildrenTooMany(forNode, tagInfo);
                    targetCollection.Add(new QMAddChildMember($"{tagInfo.ChildrenProperty!}.Add", Bind(forNode, tagInfo)));
                    break;
            }
        }
    }

    QMForNodeSymbol<ITypeSymbol> Bind(QuickMarkupParsedForNode forNode, QMBinderTagInfo tagInfo)
    {
        var type = forNode.VarType is null ? null : resolver.GetTypeSymbol(forNode.VarType.Type);
        return new(type?.WithNullableAnnotation(
            forNode.VarType?.IsTypeNullable ?? false ?
                NullableAnnotation.Annotated :
                NullableAnnotation.NotAnnotated
            ), forNode.VarName, Bind(forNode.Iterable, type, tagInfo), Bind(forNode.Body, tagInfo));
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
        var targetPropSymbol = CodeGenTypeResolver.FindProperty(tagInfo.TagType, property.Key);
        var targetType = targetPropSymbol?.Type;
        switch (property.Operator)
        {
            case ParsedPropertyOperator.AddAssign:
                // event
                // <QM Click+=`(_, _) => ShowDialog("Clicked")` />
                var isShorthand = property.Key.StartsWith("@");
                var eventName = isShorthand ? property.Key[1..] : property.Key;
                var eventSymbol = CodeGenTypeResolver.FindEvent(tagInfo.TagType, eventName);
                targetCollection.Add(new QMAddEventMember<ITypeSymbol>(
                    eventSymbol?.Type, // type hint to null
                    eventName,
                    Bind(property.Value ?? throw new NotImplementedException(), null, tagInfo),
                    isShorthand
                ));
                break;
            case ParsedPropertyOperator.Assign:
                // Property
                if (property.Value is QuickMarkupQMs listAssign)
                {
                    // <Grid RowDefinitions=<>
                    //          <RowDefinition/>
                    //          <RowDefinition/>
                    //     </>
                    // </Grid>
                    if (targetPropSymbol?.Name is not { } name)
                        throw new InvalidOperationException("Name is null");
                    Bind(listAssign.Value, new(
                        targetType,
                        name,
                        name,
                        null, // element type
                        ChildrenModes.Add
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
                    var deProp = CodeGenTypeResolver.FindProperty(tagInfo.TagType, $"{property.Key}Property");
                    if (deProp is null)
                        isDependencyProp = false;
                    else if (!deProp.IsStatic)
                        isDependencyProp = false;
                    else
                    {
                        isDependencyProp = deProp.Type.Name is "DependencyProperty";
                        depName = $"{deProp.ContainingType.FullNameWithoutAnnotation()}.{deProp.Name}";
                    }
                }
                targetCollection.Add(new QMAddPropertyMember<ITypeSymbol>(
                    targetType,
                    property.Key,
                    Value(CodeGenTypeResolver.FindProperty(tagInfo.TagType, property.Key)?.Type, target),
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
                if (CodeGenTypeResolver.FindProperty(tagInfo.TagType, property.Key) is { } propSymbol)
                {
                    // <QM IsEnabled />
                    targetCollection.Add(new QMAddPropertyMember<ITypeSymbol>(
                        targetType,
                        property.Key,
                        Value(propSymbol.Type, "true"),
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
    IQMValueSymbol Bind(QuickMarkupValue? value, ITypeSymbol? type, QMBinderTagInfo tagInfo)
    {
        switch (value)
        {
            case QuickMarkupInt32 x:
                return ValueOrAutoNew(resolver.Int32, x.Value.ToString(), type);
            case QuickMarkupDouble x:
                return ValueOrAutoNew(resolver.Double, x.Value.ToString(), type);
            case QuickMarkupBoolean x:
                return ValueOrAutoNew(resolver.Boolean, x.Value ? "true" : "false", type);
            case QuickMarkupString x:
                return ValueOrAutoNew(resolver.Boolean, $"\"{SymbolDisplay.FormatLiteral(x.Value, false)}\"", type);
            case QuickMarkupDefault x:
                if (x.IsExplicitlyNull)
                {
                    if (type is null)
                        return Value(type, "null");
                    else
                        return Value(type, $"(null as {type.FullName()})");
                }
                if (type is null)
                {
                    // cannot resolve type, use "default" without type
                    return Value(type, "default");
                }
                return Value(type, $"default({type.FullName()})");
            case QuickMarkupForeign x:
                return Value(type, x.Code);
            case QuickMarkupIdentifier x:
                if (type is null)
                    throw new NotImplementedException($"Cannot infer type for the enum member {x.Identifier}");
                return Value(type, $"{type.FullName()}.{x.Identifier}");
            case QuickMarkupQMs x:
                return new QMNestedValuesSymbol<ITypeSymbol>(type, Bind(x.Value, tagInfo));
            case QuickMarkupParsedTag x:
                return Bind(x);
            case QuickMarkupRange x:
                return new QMRangeSymbol(x.RangeStart, x.RangeEnd);
            default:
                throw new NotImplementedException();
        }
        ;
    }
    static QMValueSymbol<ITypeSymbol> Value(ITypeSymbol? type, string ValueInFinalCode) => new(type, ValueInFinalCode);
    QMValueSymbol<ITypeSymbol> ValueOrAutoNew(ITypeSymbol? type, string ValueInFinalCode, ITypeSymbol? targetType)
    {
        if (targetType is null)
            return Value(type, ValueInFinalCode);
        if (resolver.ShouldAutoNew(type, targetType))
            // wrap in new(...)
            return Value(type, $"new {targetType.FullName()}({ValueInFinalCode})");
        else
            return Value(type, ValueInFinalCode);
    }
}
