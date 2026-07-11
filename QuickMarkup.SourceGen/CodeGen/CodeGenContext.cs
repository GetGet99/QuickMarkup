using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using QuickMarkup.Language.Symbols;
using System.Text;

namespace QuickMarkup.SourceGen.CodeGen;

class CodeGenContext(StringBuilder membersBuilder, StringBuilder codeBuilder, QuickMarkupInitializationMode initMode, bool hasExplicitConstructors)
{
    int counterRef = 0;
    readonly Stack<ForScope> forScopes = [];
    string disposableAddTarget = "QUICKMARKUP_DISPOSABLES";
    // Use null-forgiving assignment (private T X = null!) when fields are assigned in Init(),
    // not directly in a constructor body. readonly only works when everything is inline in
    // the constructor (old v0.1.15 behavior: BackwardCompatible + no explicit constructors).
    bool useNullForgivingFields => initMode is QuickMarkupInitializationMode.DeferredInit
        || (initMode is QuickMarkupInitializationMode.BackwardCompatible && hasExplicitConstructors);

    string NewVariable() => $"QUICKMARKUP_NODE_{counterRef++}";

    public void CGenWrite(QMNodeSymbol<ITypeSymbol?> node, string target)
    {
        CGenWrite(node.Members, target);
    }

    string CGen(QMNodeSymbol<ITypeSymbol?> node)
    {
        if (node.InitMode == QuickMarkupInitializationMode.DeferredInit)
            return CGenDeferredInit(node);

        var constructor = CGen(node.Constructor);
        var varName = EmitVariableAndField(node, constructor);
        CGenWrite(node, varName);
        return varName;
    }

    /// <summary>
    /// Shared variable and field generation for both BackwardCompatible and DeferredInit paths.
    /// BackwardCompatible calls the constructor inline at declaration; DeferredInit defers it.
    /// </summary>
    string EmitVariableAndField(QMNodeSymbol<ITypeSymbol?> node, string constructorExpr)
    {
        var typeName = node.Type?.FullName() ?? "QM_UnknownType";
        bool isDeferred = node.InitMode == QuickMarkupInitializationMode.DeferredInit;

        if (string.IsNullOrWhiteSpace(node.Name))
        {
            var varName = NewVariable();
            if (isDeferred)
                codeBuilder.AppendLine($"{typeName} {varName} = null!;");
            else
                codeBuilder.AppendLine($"{typeName} {varName} = {constructorExpr};");
            return varName;
        }
        else if (node.IsRef)
        {
            var name = node.Name!;
            var nullableType = typeName + "?";
            var fieldName = name + "Prop";
            membersBuilder.AppendLine($"private readonly global::QuickMarkup.Infra.Reference<{nullableType}> {fieldName} = new(null);");
            membersBuilder.AppendLine($"private {nullableType} {name} => {fieldName}.Value;");
            if (!isDeferred)
                codeBuilder.AppendLine($"{fieldName}.Value = {constructorExpr};");
            return $"{fieldName}.Value!";
        }
        else
        {
            var varName = node.Name!;
            if (useNullForgivingFields)
                membersBuilder.AppendLine($"private {typeName} {varName} = null!;");
            else
                membersBuilder.AppendLine($"private readonly {typeName} {varName};");
            if (!isDeferred)
                codeBuilder.AppendLine($"{varName} = {constructorExpr};");
            return varName;
        }
    }

    string CGenDeferredInit(QMNodeSymbol<ITypeSymbol?> node)
    {
        var typeName = node.Type?.FullName() ?? "QM_UnknownType";
        var constructorExpr = CGen(node.Constructor);
        var varName = EmitVariableAndField(node, constructorExpr);

        // Separate init properties from post-init members
        var initMembers = new List<IQMMemberSymbol>();
        var postInitMembers = new List<IQMMemberSymbol>();
        var outputPrefix = node.ComponentOutputPropertyName is not null
            ? $"{node.ComponentOutputPropertyName}."
            : null;
        foreach (var member in node.Members)
        {
            bool isOutputMember = member switch
            {
                QMAddPropertyMember<ITypeSymbol?> p
                    when outputPrefix is not null && p.PropertyName.StartsWith(outputPrefix, StringComparison.Ordinal)
                    => true,
                _ => false
            };

            if (isOutputMember)
                postInitMembers.Add(member);
            else if (member is QMAddPropertyMember<ITypeSymbol?>
                or QMAttachedPropertyMember<ITypeSymbol?>
                or QMCallbackMember<ITypeSymbol?>)
                initMembers.Add(member);
            else
                postInitMembers.Add(member);
        }

        var varTarget = varName.EndsWith("!") ? varName[..^1] : varName;
        if (initMembers.Count > 0)
        {
            // Build lambda body for property initializers
            var lambdaParam = NewVariable();
            var lambdaBuilder = new StringBuilder();
            var lambdaCtx = new CodeGenContext(membersBuilder, lambdaBuilder, QuickMarkupInitializationMode.BackwardCompatible, hasExplicitConstructors: true)
            {
                counterRef = counterRef,
                disposableAddTarget = disposableAddTarget
            };
            foreach (var scope in forScopes.Reverse())
                lambdaCtx.forScopes.Push(scope);
            // Assign outer variable before property initializers to match order-of-operations promise
            lambdaBuilder.AppendLine($"{varTarget} = {lambdaParam};");

            // Propagate context to child components supporting it
            if (node.SupportsContext)
            {
                lambdaBuilder.AppendLine($"{lambdaParam}.Context = new global::QuickMarkup.Infra.QuickMarkupContext(Context);");
            }

            lambdaCtx.CGenWrite(initMembers, lambdaParam);
            counterRef = lambdaCtx.counterRef;

            if (node.Constructor.Parameters.Count > 0)
            {
                var ctorArgs = string.Join(", ", node.Constructor.Parameters.Select(p => CGen(p)));
                codeBuilder.AppendLine($"{varTarget} = new {typeName}({ctorArgs}, {lambdaParam} => {{");
            }
            else
            {
                codeBuilder.AppendLine($"{varTarget} = new {typeName}({lambdaParam} => {{");
            }
            codeBuilder.Append(lambdaBuilder.ToString().IndentWOF(2));
            codeBuilder.AppendLine("});");
        }
        else if (node.SupportsContext)
        {
            // No properties to set, but still need to propagate context to child components
            var lambdaParam = NewVariable();
            if (node.Constructor.Parameters.Count > 0)
            {
                var ctorArgs = string.Join(", ", node.Constructor.Parameters.Select(p => CGen(p)));
                codeBuilder.AppendLine($"{varTarget} = new {typeName}({ctorArgs}, {lambdaParam} => {{");
            }
            else
            {
                codeBuilder.AppendLine($"{varTarget} = new {typeName}({lambdaParam} => {{");
            }
            codeBuilder.AppendLine($"    {lambdaParam}.Context = new global::QuickMarkup.Infra.QuickMarkupContext(Context);");
            codeBuilder.AppendLine("});");
        }
        else
        {
            codeBuilder.AppendLine($"{varTarget} = {constructorExpr};");
        }

        // Process post-init members (children, events, etc.)
        if (postInitMembers.Count > 0)
            CGenWrite(postInitMembers, varName);

        return varName;
    }

    string CGen(QMConstructor constructor)
    {
        StringBuilder sb = new();
        if (constructor.ShouldUseNewKeyword)
            sb.Append("new ");
        sb.Append(constructor.Method);
        sb.Append("(");
        foreach (var parameter in constructor.Parameters)
            sb.Append(CGen(parameter));
        sb.Append(")");
        return sb.ToString();
    }

    void CGenWrite(IReadOnlyList<IQMMemberSymbol> members, string target)
    {
        for (var i = 0; i < members.Count; i++)
        {
            switch (members[i])
            {
                case QMAddChildMember<ITypeSymbol?> { CollectionLowering: ChildCollectionLowering.Blocks }:
                    i = CGenBlockCollection(members, target, i);
                    break;
                case QMAddChildMember<ITypeSymbol?> addChild:
                    CGenAddChildDirect(addChild, target);
                    break;
                case QMAssignChildMember<ITypeSymbol?> assignChild:
                    CGenAssignChild(assignChild, target);
                    break;
                case QMAddPropertyMember<ITypeSymbol?> addProp:
                    CGenAddProperty(addProp, target);
                    break;
                case QMAttachedPropertyMember<ITypeSymbol?> addAttachedProp:
                    CGenAddAttachedProperty(addAttachedProp, target);
                    break;
                case QMAddEventMember<ITypeSymbol?> addEvent:
                    CGenAddEvent(addEvent, target);
                    break;
                case QMExtensionMember extension:
                    codeBuilder.AddMethodCall($"{target}{TargetPath(extension.TargetPath)}.{extension.Method}");
                    break;
                case QMCallbackMember<ITypeSymbol?> callback:
                    codeBuilder.AddClosure(callback.Type, target, callback.RawDelegateCode);
                    break;
                case QMComponentRootMember<ITypeSymbol?> componentRoot:
                    CGenComponentRoot(componentRoot, target);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }

    int CGenBlockCollection(IReadOnlyList<IQMMemberSymbol> members, string target, int startIndex)
    {
        var first = (QMAddChildMember<ITypeSymbol?>)members[startIndex];
        var host = NewVariable();
        var elementType = TypeName(first.ChildElementType);
        codeBuilder.AppendLine($"""
        global::QuickMarkup.Infra.UIBlockHost<{elementType}> {host} = new global::QuickMarkup.Infra.UIBlockHost<{elementType}>(
            new global::QuickMarkup.Infra.TargetUICollection<{elementType}>({target}.{first.ChildPropertyPath}));
        """);

        var i = startIndex;
        for (; i < members.Count; i++)
        {
            if (members[i] is not QMAddChildMember<ITypeSymbol?> addChild ||
                addChild.CollectionLowering is not ChildCollectionLowering.Blocks ||
                addChild.ChildPropertyPath != first.ChildPropertyPath)
                break;

            var block = CGenBlock(addChild.Child, addChild.ChildElementType);
            codeBuilder.AppendLine($"{host}.AddBlock({block});");
        }

        codeBuilder.AppendLine($"""
        QUICKMARKUP_DISPOSABLES.Add(new global::QuickMarkup.Infra.DisposableAction(() => {host}.Clear()));
        """);
        return i - 1;
    }

    void CGenAddChildDirect(QMAddChildMember<ITypeSymbol?> addChild, string target)
    {
        switch (addChild.Child)
        {
            case QMNodeSymbol<ITypeSymbol?> nodeChild:
                codeBuilder.AddMethodCall($"{target}.{addChild.ChildPropertyPath}.Add", CGenNodeValue(nodeChild));
                break;
            case QMValueSymbol<ITypeSymbol?> nodeChild:
                codeBuilder.AddMethodCall($"{target}.{addChild.ChildPropertyPath}.Add", CGen(nodeChild));
                break;
            case QMForNodeSymbol<ITypeSymbol?> { Kind: QMForKind.StaticRange } forChild:
                if (forChild.Iterable is not QMRangeSymbol range)
                    throw new NotSupportedException("Static range foreach requires a range iterable.");
                codeBuilder.AddForEachStart(forChild.VarType, forChild.VarName, range);
                CGenWrite(forChild.Body, target);
                codeBuilder.AddForEachEnd();
                break;
            default:
                throw new NotImplementedException($"Direct child codegen does not support {addChild.Child.GetType().Name}.");
        }
    }

    void CGenAssignChild(QMAssignChildMember<ITypeSymbol?> assignChild, string target)
    {
        switch (assignChild.Child)
        {
            case QMNodeSymbol<ITypeSymbol?> nodeChild:
                codeBuilder.AddPropertyAssign($"{target}.{assignChild.ChildPropertyPath}", CGenNodeValue(nodeChild));
                break;
            case QMConditionalValueSymbol<ITypeSymbol?> conditional:
                CGenConditionalSlot(conditional, $"{target}.{assignChild.ChildPropertyPath}", assignChild.ChildType);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    void CGenAddProperty(QMAddPropertyMember<ITypeSymbol?> addProp, string target)
    {
        var property = $"{target}.{addProp.PropertyName}";
        switch (addProp.BindingMode)
        {
            case BindingModes.OneTime:
                codeBuilder.AddPropertyAssign(
                    property,
                    CGen(addProp.Value)
                );
                break;
            case BindingModes.SourceToTarget:
                AddSourceToTarget();
                break;
            case BindingModes.TargetToSource:
                AddTargetToSource();
                break;
            case BindingModes.TwoWay:
                AddSourceToTarget();
                AddTargetToSource();
                break;
        }

        void AddSourceToTarget()
        {
            codeBuilder.AddPropertyBindOneWay(
                addProp.PropertyType,
                property,
                CGen(addProp.Value),
                disposableAddTarget: disposableAddTarget
            );
        }

        void AddTargetToSource()
        {
            if (addProp.IsDependencyProperty)
                codeBuilder.AddDependencyPropertyBindBack(
                    property,
                    TargetObjectForPropertyPath(target, addProp.PropertyName),
                    addProp.DependencyPropertyName,
                    CGen(addProp.Value)
                );
            else
                codeBuilder.AddPropertyBindOneWay(
                    addProp.PropertyType,
                    CGen(addProp.Value),
                    property,
                    disposableAddTarget: disposableAddTarget
                );
        }
    }

    void CGenAddAttachedProperty(QMAttachedPropertyMember<ITypeSymbol?> addProp, string target)
    {
        switch (addProp.BindingMode)
        {
            case BindingModes.OneTime:
                codeBuilder.AddAttachedPropertyAssign(
                    addProp.AttachedTypeFullName,
                    addProp.PropertyName,
                    target,
                    CGen(addProp.Value)
                );
                break;
            case BindingModes.SourceToTarget:
                codeBuilder.AddAttachedPropertyBindOneWay(
                    addProp.AttachedTypeFullName,
                    addProp.PropertyName,
                    target,
                    CGen(addProp.Value),
                    disposableAddTarget: disposableAddTarget
                );
                break;
            case BindingModes.TargetToSource:
                if (addProp.IsDependencyProperty)
                    codeBuilder.AddAttachedDependencyPropertyBindBack(
                        addProp.AttachedTypeFullName,
                        addProp.PropertyName,
                        target,
                        addProp.DependencyPropertyName,
                        CGen(addProp.Value)
                    );
                else
                    codeBuilder.AddPropertyBindOneWay(
                        addProp.PropertyType,
                        CGen(addProp.Value),
                        $"{target}",
                        disposableAddTarget: disposableAddTarget
                    );
                break;
            case BindingModes.TwoWay:
                codeBuilder.AddAttachedPropertyBindOneWay(
                    addProp.AttachedTypeFullName,
                    addProp.PropertyName,
                    target,
                    CGen(addProp.Value),
                    disposableAddTarget: disposableAddTarget
                );
                if (addProp.IsDependencyProperty)
                    codeBuilder.AddAttachedDependencyPropertyBindBack(
                        addProp.AttachedTypeFullName,
                        addProp.PropertyName,
                        target,
                        addProp.DependencyPropertyName,
                        CGen(addProp.Value)
                    );
                else
                    codeBuilder.AddPropertyBindOneWay(
                        addProp.PropertyType,
                        CGen(addProp.Value),
                        $"{target}",
                        disposableAddTarget: disposableAddTarget
                    );
                break;
        }
    }

    void CGenComponentRoot(QMComponentRootMember<ITypeSymbol?> componentRoot, string target)
    {
        var property = $"{target}.{componentRoot.OutputPropertyName}";
        switch (componentRoot.Kind)
        {
            case QMComponentKind.Single:
                switch (componentRoot.Output)
                {
                    case QMNodeSymbol<ITypeSymbol?> node:
                        codeBuilder.AddPropertyAssign(property, CGenNodeValue(node));
                        break;
                    case QMConditionalValueSymbol<ITypeSymbol?> conditional:
                        CGenConditionalSlot(conditional, property, componentRoot.OutputType);
                        break;
                    case QMValueSymbol<ITypeSymbol?> value:
                        codeBuilder.AddPropertyAssign(property, CGen(value));
                        break;
                    default:
                        throw new NotSupportedException($"Single component root codegen does not support {componentRoot.Output.GetType().Name}.");
                }
                break;
            case QMComponentKind.Fragment:
                if (componentRoot.Output is not QMFragmentNodeSymbol fragment)
                    throw new NotSupportedException($"Fragment component root codegen does not support {componentRoot.Output.GetType().Name}.");
                codeBuilder.AddPropertyAssign(property, CGenFragmentBlock(fragment, componentRoot.OutputType));
                break;
            default:
                throw new NotSupportedException("Component root codegen requires a component kind.");
        }
    }

    void CGenAddEvent(QMAddEventMember<ITypeSymbol?> addEvent, string target)
    {
        if (addEvent.Value is QMValueSymbol<ITypeSymbol?> { CapturedLocalNames.Count: > 0 } capturedValue &&
            addEvent.MemberType is INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod } eventType)
        {
            codeBuilder.AddEventAssign(
                $"{target}.{addEvent.EventName}",
                CGenCapturedEventHandler(capturedValue, eventType, invokeMethod, addEvent.IsShorthand));
            return;
        }

        var rhs = CGen(addEvent.Value);
        if (addEvent.IsShorthand)
        {
            if ((addEvent.MemberType as INamedTypeSymbol)?.DelegateInvokeMethod?.ReturnsVoid ?? true)
                rhs = $$"""
                    delegate { {{rhs}}; }
                    """;
            else
                rhs = $$"""
                    delegate { return {{rhs}}; }
                    """;
        }
        codeBuilder.AddEventAssign($"{target}.{addEvent.EventName}", rhs);
    }

    string CGenCapturedEventHandler(
        QMValueSymbol<ITypeSymbol?> value,
        INamedTypeSymbol eventType,
        IMethodSymbol invokeMethod,
        bool isShorthand)
    {
        var parameters = invokeMethod.Parameters
            .Select((_, index) => $"QUICKMARKUP_EVENT_ARG_{index}")
            .ToArray();
        var parameterList = parameters.Length is 0
            ? "()"
            : $"({string.Join(", ", parameters)})";
        var arguments = string.Join(", ", parameters);
        var locals = CGenCapturedLocalDeclarations(value);
        var body = new StringBuilder();
        body.Append(locals);

        if (isShorthand)
        {
            if (invokeMethod.ReturnsVoid)
                body.AppendLine($"{value.ValueInFinalCode};");
            else
                body.AppendLine($"return {value.ValueInFinalCode};");
        }
        else
        {
            var eventTypeName = TypeName(eventType.WithNullableAnnotation(NullableAnnotation.NotAnnotated));
            if (invokeMethod.ReturnsVoid)
                body.AppendLine($"(({eventTypeName})({value.ValueInFinalCode}))!({arguments});");
            else
                body.AppendLine($"return (({eventTypeName})({value.ValueInFinalCode}))!({arguments});");
        }

        return $$"""
        {{parameterList}} => {
            {{body.ToString().IndentWOF(1)}}
        }
        """;
    }

    string CGenBlock(IQMNodeChildSymbol child, ITypeSymbol? elementType)
    {
        return child switch
        {
            QMNodeSymbol<ITypeSymbol?> { ComponentKind: QMComponentKind.Fragment } node => CGenFragmentComponentBlock(node),
            QMNodeSymbol<ITypeSymbol?> node => CGenStaticBlock(node, elementType),
            QMValueSymbol<ITypeSymbol?> value => CGenStaticBlock(value, elementType),
            QMIfNodeSymbol<ITypeSymbol?> ifNode => CGenConditionalBlock(ifNode, elementType),
            QMForNodeSymbol<ITypeSymbol?> forNode => CGenForBlock(forNode, elementType),
            QMFragmentNodeSymbol fragment => CGenFragmentBlock(fragment, elementType),
            _ => throw new NotImplementedException($"Block codegen does not support {child.GetType().Name}.")
        };
    }

    string CGenFragmentComponentBlock(QMNodeSymbol<ITypeSymbol?> node)
    {
        var component = CGen(node);
        return $"{component}.{node.ComponentOutputPropertyName}";
    }

    string CGenStaticBlock(IQMNodeChildSymbol child, ITypeSymbol? elementType)
    {
        var typeName = TypeName(elementType);
        var items = NewVariable();
        var scopeParameter = NewVariable();
        var nested = new StringBuilder();
        var previousDisposableTarget = disposableAddTarget;
        disposableAddTarget = scopeParameter;

        var valueExpression = child switch
        {
            QMNodeSymbol<ITypeSymbol?> node => CGenIntoValue(node, nested),
            QMValueSymbol<ITypeSymbol?> value => CGen(value),
            _ => throw new NotImplementedException()
        };

        nested.AppendLine($"{items}.Add({valueExpression});");
        disposableAddTarget = previousDisposableTarget;

        return $$"""
        new global::QuickMarkup.Infra.StaticBlock<{{typeName}}>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            ({{items}}, {{scopeParameter}}) => {
                {{nested.ToString().IndentWOF(2)}}
            })
        """;
    }

    string CGenConditionalBlock(QMIfNodeSymbol<ITypeSymbol?> ifNode, ITypeSymbol? elementType)
    {
        var typeName = TypeName(elementType);
        var trueBlock = CGenFragmentBlock(ifNode.BodyWhenTrue, elementType);
        var falseBlock = ifNode.BodyWhenFalse is null
            ? null
            : CGenFragmentBlock(ifNode.BodyWhenFalse, elementType);

        if (falseBlock is null)
            return $$"""
            new global::QuickMarkup.Infra.ConditionalBlock<{{typeName}}>(
                new global::QuickMarkup.Infra.ReactiveScope(),
                () => {{CGen(ifNode.Condition)}},
                () => {{trueBlock}})
            """;

        return $$"""
        new global::QuickMarkup.Infra.ConditionalBlock<{{typeName}}>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            () => {{CGen(ifNode.Condition)}},
            () => {{trueBlock}},
            () => {{falseBlock}})
        """;
    }

    string CGenForBlock(QMForNodeSymbol<ITypeSymbol?> forNode, ITypeSymbol? elementType)
    {
        if (forNode.Kind is QMForKind.StaticRange)
            return CGenStaticRangeFragmentBlock(forNode, elementType);

        var typeName = TypeName(elementType);
        var itemRef = NewVariable();
        var indexRef = NewVariable();
        var body = CGenForBodyBlock(forNode, elementType, itemRef, forNode.IndexVarName is null ? null : indexRef);
        var source = CGen(forNode.Iterable);

        if (forNode.Key is not null)
        {
            var keyFactory = CGenForKeyFactory(forNode);
            if (forNode.IndexVarName is null)
                return $$"""
                global::QuickMarkup.Infra.ForBlock.Create(
                    new global::QuickMarkup.Infra.ReactiveScope(),
                    {{source}},
                    {{keyFactory}},
                    ({{itemRef}}) => {{body}})
                """;

            return $$"""
            global::QuickMarkup.Infra.ForBlock.Create(
                new global::QuickMarkup.Infra.ReactiveScope(),
                {{source}},
                {{keyFactory}},
                ({{indexRef}}, {{itemRef}}) => {{body}})
            """;
        }

        if (forNode.IndexVarName is null)
            return $$"""
            global::QuickMarkup.Infra.ForBlock.Create(
                new global::QuickMarkup.Infra.ReactiveScope(),
                {{source}},
                ({{itemRef}}) => {{body}})
            """;

        return $$"""
        global::QuickMarkup.Infra.ForBlock.Create(
            new global::QuickMarkup.Infra.ReactiveScope(),
            {{source}},
            ({{indexRef}}, {{itemRef}}) => {{body}})
        """;
    }

    string CGenForBodyBlock(QMForNodeSymbol<ITypeSymbol?> forNode, ITypeSymbol? elementType, string itemRef, string? indexRef)
    {
        forScopes.Push(new(forNode.VarName, itemRef, forNode.IndexVarName, indexRef));
        var block = CGenFragmentBlock(forNode.Body, elementType);
        forScopes.Pop();
        return block;
    }

    string CGenForKeyFactory(QMForNodeSymbol<ITypeSymbol?> forNode)
    {
        if (forNode.Key is null)
            throw new InvalidOperationException("For key factory requires a key expression.");

        if (forNode.VarType is { } varType)
        {
            if (forNode.IndexVarName is null)
                return $$"""
                ({{varType.FullName()}} {{forNode.VarName}}) => {
                    return {{CGen(forNode.Key)}};
                }
                """;

            return $$"""
            ({{varType.FullName()}} {{forNode.VarName}}, int {{forNode.IndexVarName}}) => {
                return {{CGen(forNode.Key)}};
            }
            """;
        }

        var item = NewVariable();
        var index = NewVariable();
        if (forNode.IndexVarName is null)
            return $$"""
            ({{item}}) => {
                var {{forNode.VarName}} = {{item}};
                return {{CGen(forNode.Key)}};
            }
            """;

        return $$"""
        ({{item}}, {{index}}) => {
            var {{forNode.VarName}} = {{item}};
            var {{forNode.IndexVarName}} = {{index}};
            return {{CGen(forNode.Key)}};
        }
        """;
    }

    string CGenStaticRangeFragmentBlock(QMForNodeSymbol<ITypeSymbol?> forNode, ITypeSymbol? elementType)
    {
        if (forNode.Iterable is not QMRangeSymbol range)
            throw new NotSupportedException("Static range foreach requires a range iterable.");

        var typeName = TypeName(elementType);
        var host = NewVariable();
        var scopeParameter = NewVariable();
        var nested = new StringBuilder();
        var nestedContext = Clone(nested);
        nestedContext.disposableAddTarget = scopeParameter;
        nested.AppendLine($"for ({(forNode.VarType is null ? "var" : forNode.VarType.FullName())} {forNode.VarName} = {range.Start}; {forNode.VarName} < {range.End}; {forNode.VarName}++) {{");
        nestedContext.CGenAddBlocksToHost(forNode.Body, host, elementType);
        counterRef = nestedContext.counterRef;
        nested.AppendLine("}");

        return $$"""
        new global::QuickMarkup.Infra.FragmentBlock<{{typeName}}>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            ({{host}}, {{scopeParameter}}) => {
                {{nested.ToString().IndentWOF(2)}}
            })
        """;
    }

    string CGenFragmentBlock(QMFragmentNodeSymbol fragment, ITypeSymbol? elementType)
        => CGenFragmentBlock(fragment.Body, elementType);

    string CGenFragmentBlock(IReadOnlyList<IQMMemberSymbol> body, ITypeSymbol? elementType)
    {
        var typeName = TypeName(elementType);
        var host = NewVariable();
        var scopeParameter = NewVariable();
        var nested = new StringBuilder();
        var nestedContext = Clone(nested);
        nestedContext.disposableAddTarget = scopeParameter;
        nestedContext.CGenAddBlocksToHost(body, host, elementType);
        counterRef = nestedContext.counterRef;

        return $$"""
        new global::QuickMarkup.Infra.FragmentBlock<{{typeName}}>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            ({{host}}, {{scopeParameter}}) => {
                {{nested.ToString().IndentWOF(2)}}
            })
        """;
    }

    void CGenAddBlocksToHost(IReadOnlyList<IQMMemberSymbol> body, string host, ITypeSymbol? elementType)
    {
        foreach (var member in body)
        {
            if (member is not QMAddChildMember<ITypeSymbol?> addChild)
                throw new NotSupportedException("Only child members are supported inside generated block bodies.");

            var block = CGenBlock(addChild.Child, addChild.ChildElementType ?? elementType);
            codeBuilder.AppendLine($"{host}.AddBlock({block});");
        }
    }

    void CGenConditionalSlot(QMConditionalValueSymbol<ITypeSymbol?> conditional, string target, ITypeSymbol? expectedType = null)
    {
        var type = expectedType ?? GetChildValueType(conditional);
        var typeName = TypeName(type);
        var slot = NewVariable();
        codeBuilder.AppendLine($$"""
        var {{slot}} = new global::QuickMarkup.Infra.ConditionalSlot<{{typeName}}>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            () => {{CGen(conditional.Condition)}},
            QUICKMARKUP_VALUE => {{target}} = QUICKMARKUP_VALUE,
            () => {
                {{CGenScopedValueFactoryBody(conditional.ValueWhenTrue, type, target).IndentWOF(2)}}
            },
            () => {
                {{CGenScopedValueFactoryBody(conditional.ValueWhenFalse, type, target).IndentWOF(2)}}
            });
        QUICKMARKUP_DISPOSABLES.Add({{slot}});
        """);
    }

    string CGenScopedValueFactoryBody(IQMNodeChildSymbol child, ITypeSymbol? type, string target)
    {
        var scope = NewVariable();
        var nested = new StringBuilder();
        var previousDisposableTarget = disposableAddTarget;
        disposableAddTarget = scope;
        var expression = child switch
        {
            QMNodeSymbol<ITypeSymbol?> node => CGenIntoValue(node, nested),
            QMValueSymbol<ITypeSymbol?> value => CGen(value),
            QMConditionalValueSymbol<ITypeSymbol?> conditional => CGenNestedConditionalSlot(
                conditional,
                type,
                target,
                scope,
                nested),
            _ => throw new NotSupportedException($"Scoped value codegen does not support {child.GetType().Name}.")
        };
        disposableAddTarget = previousDisposableTarget;

        return $$"""
        global::QuickMarkup.Infra.ReactiveScope {{scope}} = new global::QuickMarkup.Infra.ReactiveScope();
        {{nested}}
        return new global::QuickMarkup.Infra.ScopedValue<{{TypeName(type)}}>(
            {{expression}},
            {{scope}});
        """;
    }

    string CGenNestedConditionalSlot(
        QMConditionalValueSymbol<ITypeSymbol?> conditional,
        ITypeSymbol? type,
        string target,
        string scope,
        StringBuilder nested)
    {
        var typeName = TypeName(type);
        var value = NewVariable();
        var slot = NewVariable();
        nested.AppendLine($$"""
        {{typeName}} {{value}} = default!;
        var {{slot}} = new global::QuickMarkup.Infra.ConditionalSlot<{{typeName}}>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            () => {{CGen(conditional.Condition)}},
            QUICKMARKUP_VALUE => {
                {{value}} = QUICKMARKUP_VALUE;
                {{target}} = QUICKMARKUP_VALUE;
            },
            () => {
                {{CGenScopedValueFactoryBody(conditional.ValueWhenTrue, type, target).IndentWOF(2)}}
            },
            () => {
                {{CGenScopedValueFactoryBody(conditional.ValueWhenFalse, type, target).IndentWOF(2)}}
            });
        {{scope}}.Add({{slot}});
        """);
        return value;
    }

    ITypeSymbol? GetChildValueType(IQMNodeChildSymbol child)
        => child switch
        {
            QMNodeSymbol<ITypeSymbol?> node => NodeValueType(node),
            QMValueSymbol<ITypeSymbol?> value => value.Type,
            QMConditionalValueSymbol<ITypeSymbol?> conditional =>
                GetChildValueType(conditional.ValueWhenTrue) ??
                GetChildValueType(conditional.ValueWhenFalse),
            _ => null
        };

    ITypeSymbol? NodeValueType(QMNodeSymbol<ITypeSymbol?> node)
        => node.ComponentKind is QMComponentKind.Single or QMComponentKind.Fragment
            ? node.ComponentOutputType
            : node.Type;

    string CGenInto(QMNodeSymbol<ITypeSymbol?> node, StringBuilder targetBuilder)
    {
        var nested = Clone(targetBuilder);
        var result = nested.CGen(node);
        counterRef = nested.counterRef;
        return result;
    }

    string CGenIntoValue(QMNodeSymbol<ITypeSymbol?> node, StringBuilder targetBuilder)
    {
        var nested = Clone(targetBuilder);
        var result = nested.CGenNodeValue(node);
        counterRef = nested.counterRef;
        return result;
    }

    string CGenNodeValue(QMNodeSymbol<ITypeSymbol?> node)
    {
        var value = CGen(node);
        return node.ComponentKind is QMComponentKind.Single
            ? $"{value}.{node.ComponentOutputPropertyName}"
            : value;
    }

    CodeGenContext Clone(StringBuilder builder)
    {
        var clone = new CodeGenContext(membersBuilder, builder, initMode, hasExplicitConstructors)
        {
            counterRef = counterRef,
            disposableAddTarget = disposableAddTarget
        };
        foreach (var scope in forScopes.Reverse())
            clone.forScopes.Push(scope);
        return clone;
    }

    string CGen(IQMValueSymbol valueSymbol)
    {
        return valueSymbol switch
        {
            QMNodeSymbol<ITypeSymbol?> node => CGenNodeValue(node),
            QMValueSymbol<ITypeSymbol?> value => CGenValue(value),
            QMNestedValuesSymbol<ITypeSymbol?> => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
    }

    string CGenValue(QMValueSymbol<ITypeSymbol?> value)
    {
        if (forScopes.Count == 0 || value.CapturedLocalNames is not { Count: > 0 })
            return value.ValueInFinalCode;

        var captures = GetCapturedLocals(value);

        if (captures.Count is 1)
            return $$"""
            global::QuickMarkup.Infra.CompilerHelpers.ClosureValue(
                {{captures[0].Ref}}.Value,
                {{captures[0].Name}} => {{value.ValueInFinalCode}})
            """;

        if (captures.Count is 2)
            return $$"""
            global::QuickMarkup.Infra.CompilerHelpers.ClosureValue(
                {{captures[0].Ref}}.Value,
                {{captures[1].Ref}}.Value,
                ({{captures[0].Name}}, {{captures[1].Name}}) => {{value.ValueInFinalCode}})
            """;

        var locals = CGenCapturedLocalDeclarations(value);

        return $$"""
        (new global::System.Func<{{TypeName(value.Type)}}>(() => {
            {{locals.IndentWOF(1)}}
            return {{value.ValueInFinalCode}};
        }))()
        """;
    }

    List<(string Name, string Ref)> GetCapturedLocals(QMValueSymbol<ITypeSymbol?> value)
    {
        var captures = new List<(string Name, string Ref)>();
        foreach (var scope in forScopes)
        {
            if (value.CapturedLocalNames.Contains(scope.ItemName))
                captures.Add((scope.ItemName, scope.ItemRef));
            if (scope.IndexName is not null &&
                scope.IndexRef is not null &&
                value.CapturedLocalNames.Contains(scope.IndexName))
                captures.Add((scope.IndexName, scope.IndexRef));
        }

        return captures;
    }

    string CGenCapturedLocalDeclarations(QMValueSymbol<ITypeSymbol?> value)
    {
        var locals = new StringBuilder();
        foreach (var capture in GetCapturedLocals(value))
            locals.AppendLine($"var {capture.Name} = {capture.Ref}.Value;");

        return locals.ToString();
    }

    static string TypeName(ITypeSymbol? type)
        => type?.FullName() ?? "object";

    static string TargetPath(string path)
        => string.IsNullOrWhiteSpace(path) ? "" : $".{path}";

    static string TargetObjectForPropertyPath(string target, string propertyPath)
    {
        var lastDot = propertyPath.LastIndexOf('.');
        return lastDot < 0
            ? target
            : $"{target}.{propertyPath[..lastDot]}";
    }

    sealed record ForScope(string ItemName, string ItemRef, string? IndexName, string? IndexRef);
}
