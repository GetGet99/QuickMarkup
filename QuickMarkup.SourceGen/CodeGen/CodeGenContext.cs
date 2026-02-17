using Get.EasyCSharp.GeneratorTools;
using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.Language.Symbols;
using System.Text;

namespace QuickMarkup.SourceGen.CodeGen;

class CodeGenContext(CodeGenTypeResolver resolver, StringBuilder membersBuilder, StringBuilder codeBuilder, bool isConstuctorMode)
{
    // counter
    int counterRef = 0;
    string NewVariable() => $"QUICKMARKUP_NODE_{counterRef++}";

    // CGen - no target or target is read-only
    // CGenWrite - side effect will be made on the target

    /// <summary>
    /// Generate codes to write members stored in the name "target."
    /// Constructor is NOT called and a new instance is not created.
    /// </summary>
    public void CGenWrite(QMNodeSymbol<ITypeSymbol> node, string target)
    {
        CGenWrite(node.Members, target);
    }

    string CGen(QMNodeSymbol<ITypeSymbol> node)
    {
        List<string> parameters = [];
        var constructor = CGen(node.Constructor);

        string varName;
        if (string.IsNullOrWhiteSpace(node.Name))
        {
            varName = NewVariable();
            codeBuilder.AppendLine($"{node.Type.FullName()} {varName} = {constructor};");
        }
        else
        {
            varName = node.Name!;
            if (isConstuctorMode)
                membersBuilder.AppendLine($"private readonly {node.Type.FullName()} {varName};");
            else
                membersBuilder.AppendLine($"private {node.Type.FullName()} {varName} = null!;");
            codeBuilder.AppendLine($"{varName} = {constructor};");
        }

        CGenWrite(node, varName);
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
        foreach (var member in members)
            switch (member)
            {
                case QMAddChildMember addChild:
                    switch (addChild.Child)
                    {
                        case QMNodeSymbol<ITypeSymbol> nodeChild:
                            codeBuilder.AddMethodCall($"{target}.{addChild.ChildPropertyPath}", CGen(nodeChild));
                            break;
                        case QMValueSymbol<ITypeSymbol> nodeChild:
                            codeBuilder.AddMethodCall($"{target}.{addChild.ChildPropertyPath}", CGen(nodeChild));
                            break;
                        case QMForNodeSymbol<ITypeSymbol> forChild:
                            if (forChild.Iterable is QMRangeSymbol range)
                                codeBuilder.AddForEachStart(forChild.VarType, forChild.VarName, range);
                            else
                                codeBuilder.AddForEachStart(forChild.VarType, forChild.VarName, CGen(forChild.Iterable));
                            CGenWrite(forChild.Body, target);
                            codeBuilder.AddForEachEnd();
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    break;
                case QMAssignChildMember assignChild:
                    switch (assignChild.Child)
                    {
                        case QMNodeSymbol<ITypeSymbol> nodeChild:
                            codeBuilder.AddPropertyAssign($"{target}.{assignChild.ChildPropertyPath}", CGen(nodeChild));
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    break;
                case QMAddPropertyMember<ITypeSymbol> addProp:
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
                            // two way is basically:
                            // 1. source to target first
                            // 2. then target to source
                            AddSourceToTarget();
                            AddTargetToSource();
                            break;
                    }
                    void AddSourceToTarget()
                    {
                        codeBuilder.AddPropertyBindOneWay(
                            addProp.PropertyType,
                            property,
                            CGen(addProp.Value)
                        );
                    }
                    void AddTargetToSource()
                    {
                        if (addProp.IsDependencyProperty)
                            codeBuilder.AddDependencyPropertyBindBack(
                                property,
                                target,
                                addProp.DependencyPropertyName,
                                CGen(addProp.Value)
                            );
                        else
                            codeBuilder.AddPropertyBindOneWay(
                                addProp.PropertyType,
                                property,
                                CGen(addProp.Value)
                            );
                    }
                    break;
                case QMAddEventMember<ITypeSymbol> addEvent:
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
                    break;
                case QMExtensionMember extension:
                    codeBuilder.AddMethodCall($"{target}.{extension.Method}");
                    break;
                case QMCallbackMember<ITypeSymbol> callback:
                    codeBuilder.AddClosure(callback.Type, target, callback.RawDelegateCode);
                    break;
                default:
                    throw new NotImplementedException();
            }
    }


    string CGen(IQMValueSymbol valueSymbol)
    {
        return valueSymbol switch
        {
            QMNodeSymbol<ITypeSymbol> node => CGen(node),
            QMValueSymbol<ITypeSymbol> value => value.ValueInFinalCode,
            // TODO, fail for now
            QMNestedValuesSymbol<ITypeSymbol> => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
    }
}
