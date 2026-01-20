using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace QuickMarkup.SourceGen;

class CodeGenContext(CodeGenTypeResolver resolver, StringBuilder membersBuilder, StringBuilder codeBuilder, bool isConstuctorMode)
{
    // counter
    int counterRef = 0;
    string NewVariable() => $"QUICKMARKUP_NODE_{counterRef++}";

    // CGen - no target or target is read-only
    // CGenWrite - side effect will be made on the target

    public (string varName, ITypeSymbol type) CGen(QuickMarkupQMNode node)
    {
        var type = resolver.GetTypeSymbol(node.Constructor.TypeName);
        var typeName = type is null ? node.Constructor.TypeName : new FullType(type).TypeWithNamespace;
        List<string> parameters = [];
        var constructor = type?.Constructors.FirstOrDefault(x => x.Parameters.Length == node.Constructor.Parameters.Count);
        for (int i = 0; i < node.Constructor.Parameters.Count; i++)
        {
            var targetType = new TargetField(constructor?.Parameters[i].Type, "Unknown");
            parameters.Add(CGen(node.Constructor.Parameters[i], targetType).code);
        }
        var createdObj = $"new {typeName}({string.Join(" ,", parameters)})";
        string varName;
        if (string.IsNullOrWhiteSpace(node.AssignToVarName))
        {
            varName = NewVariable();
            codeBuilder.AppendLine($"{typeName} {varName} = {createdObj};");
        }
        else
        {
            varName = node.AssignToVarName!;
            if (isConstuctorMode)
                membersBuilder.AppendLine($"private readonly {typeName} {varName};");
            else
                membersBuilder.AppendLine($"private {typeName} {varName} = null!;");
            codeBuilder.AppendLine($"{varName} = {createdObj};");
        }

        CGenWrite(node, new(type, varName));
        return (varName, type);
    }

    public void CGenWrite(QuickMarkupQMNode node, TargetField target)
    {
        CGenWrite(node.Properties, target);
        CGenWrite(node.Children, target);
    }

    void CGenWrite(IList<QuickMarkupQMProperty> props, TargetField target)
    {
        foreach (var prop in props)
            CGenWrite(prop, target);
    }

    void CGenWrite(IList<IQMNodeChild> children, TargetField target)
    {
        bool isMultipleNode = true; // if unknown, use add rather than to set and override
        TargetField contentTarget = target; // if unknown, fallback to self
        if (target.Type is not null)
        {
            if (resolver.TryGetContentProperty(target.Type, out var content, out isMultipleNode))
                contentTarget = new TargetField(content.Type, $"{target}.{content.Name}");
            else
                contentTarget = target;
        }
        CGenWrite(children, target, contentTarget, isMultipleNode);
    }

    void CGenWrite(IList<IQMNodeChild> children, TargetField? target, TargetField childTarget, bool addMode)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case QuickMarkupQMProperty prop:
                    if (target is null)
                    {
                        throw new ArgumentException("You cannot put properties here.");
                    }
                    CGenWrite(prop, target);
                    break;
                case QuickMarkupForNode @for:
                    CGenWrite(@for, childTarget);
                    break;
                case QuickMarkupQMNode node:
                    var (childInstance, _) = CGen(node);
                    if (addMode)
                    {
                        codeBuilder.AppendLine($"{childTarget}.Add({childInstance});");
                    }
                    else
                    {
                        codeBuilder.AppendLine($"{childTarget} = {childInstance};");
                    }
                    break;
            }
        }
    }
    void CGenWrite(QuickMarkupForNode forNode, TargetField target)
    {
        if (forNode.ListExpression is QuickMarkupForNodeListRangeExpression range)
        {
            var i = forNode.TargetVariable;
            // vartype can be "var" as of now. not reliable to be used currently
            codeBuilder.AppendLine($$"""
                for ({{forNode.VarType}} {{i}} = {{range.Start}}; {{i}} < {{range.End}}; {{i}}++) {
                    global::QuickMarkup.Infra.CompilerHelpers.Closure({{i}}, {{i}} => {
                """);
            // using foreach, must do add mode
            CGenWrite(forNode.Children, null, target, addMode: true);
            codeBuilder.AppendLine("""
                    });
                }
                """);
        }
        else if (forNode.ListExpression is QuickMarkupForNodeListForeignExpression foreignExpression)
        {
            var x = forNode.TargetVariable;
            codeBuilder.AppendLine($$"""
                foreach ({{forNode.VarType}} {{forNode.TargetVariable}} in ({{foreignExpression.ForeignAsString}})) {
                """);
            // using foreach, must do add mode
            CGenWrite(forNode.Children, null, target, addMode: true);
            codeBuilder.AppendLine("""
                }
                """);
        }
    }
    void CGenWrite(QuickMarkupQMProperty prop, TargetField target)
    {
        switch (prop)
        {
            case QuickMarkupQMPropertyBoolOrExtension extension:
                CGenWrite(extension, target);
                break;
            case QuickMarkupQMPropertyKeyValue kv:
                CGenWrite(kv, target);
                break;
        }
    }

    void CGenWrite(QuickMarkupQMPropertyKeyForeign kf, TargetField target)
    {
        if (kf.Key is null)
        {
            var typedArg = target.Type is null ? "" : $"<{new FullType(target.Type)}>";
            codeBuilder.AppendLine($"""
                global::QuickMarkup.Infra.CompilerHelpers.Closure{typedArg}({target}, {kf.Foreign.Code});
                """);
            return;
        }
        if (kf.IsEventMode)
        {
            codeBuilder.AppendLine($$"""
                {{target}}.{{kf.Key}} += {{kf.Foreign.Code}};
                """);
        }
        else if (kf.IsBindBack)
        {
            var propSym = CodeGenTypeResolver.FindProperty(target.Type, $"{kf.Key}Property");
            if (propSym?.Type.Name is "DependencyProperty" && propSym.IsStatic)
            {
                codeBuilder.AppendLine($$"""
                    {{kf.Foreign.Code}} = {{target}}.{{kf.Key}};
                    {{target}}.RegisterPropertyChangedCallback(
                        {{new FullType(propSym.ContainingType)}}.{{propSym.Name}},
                        (_, _) => {
                            {{kf.Foreign.Code}} = {{target}}.{{kf.Key}};
                        }
                    );
                    """);
            }
            else
            {
                propSym = CodeGenTypeResolver.FindProperty(target.Type, kf.Key);
                codeBuilder.AppendLine($$"""
                    QUICKMARKUP_EFFECTS.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange{{(
                        propSym is null ? "" : $"<{new FullType(propSym.Type)}>"
                    )}} (() => {
                        return {{target}}.{{kf.Key}};
                    }, x => {
                        {{kf.Foreign.Code}} = x;
                    }));
                    """);
            }
        }
        else
        {
            var propSym = CodeGenTypeResolver.FindProperty(target.Type, kf.Key);
            codeBuilder.AppendLine($$"""
                QUICKMARKUP_EFFECTS.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange{{(
                    propSym is null ? "" : $"<{new FullType(propSym.Type)}>"
                )}} (() => {
                    return {{kf.Foreign.Code}};
                }, x => {
                    {{target}}.{{kf.Key}} = x;
                }));
                """);
        }
    }

    void CGenWrite(QuickMarkupQMPropertyBoolOrExtension extension, TargetField target)
    {
        var propSym = CodeGenTypeResolver.FindProperty(target.Type, extension.ExtensionMethod);
        if (propSym is not null)
        {
            codeBuilder.AppendLine($"""
                {target}.{extension.ExtensionMethod} = true;
                """);
        }
        else
        {
            codeBuilder.AppendLine($"""
                {target}.{extension.ExtensionMethod}();
                """);
        }
    }

    void CGenWrite(QuickMarkupQMPropertyKeyValue kv, TargetField target)
    {
        if (kv is QuickMarkupQMPropertyKeyForeign kf)
        {
            CGenWrite(kf, target);
            return;
        }
        if (kv.Value is QuickMarkupForeign foreign)
        {
            CGenWrite(new QuickMarkupQMPropertyKeyForeign(kv.Key, foreign), target);
            return;
        }
        var property = CodeGenTypeResolver.FindProperty(target.Type, kv.Key);
        var contentTarget = new TargetField(property?.Type, $"{target.FieldName}.{kv.Key}");
        if (kv.Value is QuickMarkupQMs propChildren)
        {
            // if on multiple node, we assume we want to do add mode
            // also, if user tries to set property, go into the same node
            CGenWrite(propChildren.Value, contentTarget, contentTarget, addMode: true);
            return;
        }
        var (value, generatedType) = CGen(kv.Value, contentTarget);
        if (property is not null)
        {
            if (resolver.ShouldAutoNew(generatedType, property.Type))
            {
                codeBuilder.AppendLine($"""
                    {target}.{kv.Key} = new({value});
                    """);
                return;
            }
        }
        codeBuilder.AppendLine($"""
            {target}.{kv.Key} = {value};
            """);
    }

    (string code, ITypeSymbol? type) CGen(QuickMarkupValue value, TargetField? refPropertyForEnum)
    {
        switch (value)
        {
            case QuickMarkupForeign foreign:
                // we don't know the real type of foreign
                // but we can approximate with the field type
                return (foreign.Code, refPropertyForEnum?.Type);
            case QuickMarkupString str:
                return ($"\"{SymbolDisplay.FormatLiteral(str.Value, false)}\"", resolver.String);
            case QuickMarkupBoolean boolean:
                return (boolean.Value ? "true" : "false", resolver.Boolean);
            case QuickMarkupInt32 int32:
                return (int32.Value.ToString(), resolver.Int32);
            case QuickMarkupDouble @double:
                return (@double.Value.ToString(), resolver.Double);
            case QuickMarkupEnum @enum:
                if (refPropertyForEnum is null) goto default;
                if (refPropertyForEnum.Type is null)
                    // use property name as fallback
                    return ($"{refPropertyForEnum}.{@enum.EnumMember}", null);
                return ($"{new FullType(refPropertyForEnum.Type)}.{@enum.EnumMember}", refPropertyForEnum.Type);
            case QuickMarkupQM qm:
                return CGen(qm.Value);
            default:
                throw new NotImplementedException();
        }
    }

}
