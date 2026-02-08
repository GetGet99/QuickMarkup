using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace QuickMarkup.SourceGen.CodeGen;

class RefsGenContext(CodeGenTypeResolver resolver, StringBuilder membersBuilder, string nameHint)
{
    public void CGenWrite(IList<RefDeclaration> refs)
    {
        foreach (var @ref in refs)
            CGenWrite(@ref);
    }

    public void CGenWrite(RefDeclaration refDeclaration)
    {
        var typeDecl = refDeclaration.Type;
        var type = resolver.GetTypeSymbol(typeDecl.Type);
        var typeName =
            (type is null ? refDeclaration.Name : new FullType(type).TypeWithNamespace)
            + (typeDecl.IsTypeNullable ? "?" : "");
        var (defaultValue, _) = refDeclaration.DefaultValue is null ? ("default", null) : CGen(refDeclaration.DefaultValue,
            new(type, refDeclaration.Name));
        var accessibility = refDeclaration.IsPrivate ? "private" : "public";
        if (refDeclaration.IsComputedDeclaration)
        {
            var computedType = $"global::QuickMarkup.Infra.Computed<{typeName}>";
            membersBuilder.AppendLine($$"""
                {{accessibility}} {{computedType}} {{refDeclaration.Name}}Comp => field ??= new {{computedType}}(() => {{defaultValue}}, "{{nameHint}}.{{refDeclaration.Name}}");
                {{accessibility}} {{typeName}} {{refDeclaration.Name}} {
                    get {
                        return this.{{refDeclaration.Name}}Comp.Value;
                    }
                }
                """);
        } else
        {
            var refType = $"global::QuickMarkup.Infra.Reference<{typeName}>";
            membersBuilder.AppendLine($$"""
                {{accessibility}} {{refType}} {{refDeclaration.Name}}Prop => field ??= new {{refType}}({{defaultValue}}, "{{nameHint}}.{{refDeclaration.Name}}");
                {{accessibility}} {{typeName}} {{refDeclaration.Name}} {
                    get {
                        return this.{{refDeclaration.Name}}Prop.Value;
                    }
                    set {
                        this.{{refDeclaration.Name}}Prop.Value = value;
                    }
                }
                """);
        }
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
            case QuickMarkupIdentifier @enum:
                if (refPropertyForEnum is null) goto default;
                if (refPropertyForEnum.Type is null)
                    // use property name as fallback
                    return ($"{refPropertyForEnum}.{@enum.Identifier}", null);
                return ($"{new FullType(refPropertyForEnum.Type)}.{@enum.Identifier}", refPropertyForEnum.Type);
            case QuickMarkupDefault @default:
                if (@default.IsExplicitlyNull)
                {
                    if (refPropertyForEnum?.Type is { } type)
                    {
                        // cast null to type first
                        return ($"(({new FullType(type)}?)null)", type);
                    }
                    return ("null", null);
                } else
                {
                    if (refPropertyForEnum?.Type is { } type)
                    {
                        return ($"(default({new FullType(type)}))", type);
                    }
                    return ("default", null);
                }
                    default:
                throw new NotImplementedException();
        }
    }

}
