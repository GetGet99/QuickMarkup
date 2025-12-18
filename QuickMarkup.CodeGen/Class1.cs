using Microsoft.CodeAnalysis.CSharp;
using QuickMarkup.AST;
using System.Text;
namespace QuickMarkup.CodeGen;

public static class QuickMarkupCodeGen
{
    public static string ImportNamespaceSafetyGenInnerFromSFC(QuickMarkupSFC sfc)
    {
        int counterRef = 0;
        return $"""
        var QUICKMARKUP_EFFECTS = new global::System.Collections.Generic.List<IReference>();
        {sfc.Scirpt.RawScript}
        {ImportNamespaceSafetyGenFromNode((sfc.Template.Children[0] as QuickMarkupXMLNode)!, ref counterRef, out var nodeName)}
        return {nodeName};
        """;
    }
    private static string ImportNamespaceSafetyGenFromNode(QuickMarkupXMLNode node, ref int counterRef, out string varNameOut)
    {
        string varName = varNameOut = $"QUICKMARKUP_NODE_{counterRef++}";
        var code = new StringBuilder();
        code.AppendLine($"var {varName} = new {node.Name}();");
        void AddProperty(QuickMarkupXMLPropertiesKeyValue prop, ref int counterRef)
        {
            if (prop is QuickMarkupXMLPropertiesKeyForeign foreign)
            {
                code.AppendLine($$"""
                QUICKMARKUP_EFFECTS.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange(() => {
                    return {{foreign.ForeignAsString}};
                }, x => {
                    {{varName}}.{{prop.Key}} = x;
                }));
                """);
            }
            else if (prop is QuickMarkupXMLPropertiesKeyXML xml)
            {
                code.AppendLine($"""
                    {GenFromNode(xml.Value, ref counterRef, out var childVarName)}
                    {varName}.{prop.Key} = {childVarName};
                    """);
            }
            else
            {
                code.AppendLine($"""
                {varName}.{prop.Key} = {prop switch
                {
                    QuickMarkupXMLPropertiesKeyString str => $"\"{SymbolDisplay.FormatLiteral(str.Value, false)}\"",
                    QuickMarkupXMLPropertiesKeyBoolean boolean => boolean.Value ? "true" : "false",
                    QuickMarkupXMLPropertiesKeyInt32 int32 => int32.Value.ToString(),
                    _ => throw new NotImplementedException()
                }};
                """);
            }
        }
        foreach (var prop in node.Properties)
        {
            AddProperty(prop, ref counterRef);
        }
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupXMLPropertiesKeyValue prop)
                AddProperty(prop, ref counterRef);
        }
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupXMLNode childNode)
            {
                code.AppendLine($"""
                    {GenFromNode(childNode, ref counterRef, out var childNodeName)}
                    {varName}.Children.Add({childNodeName});
                    """);
            }
        }
        return code.ToString();
    }
    public static string GenInnerFromSFC(QuickMarkupSFC sfc)
    {
        int counterRef = 0;
        return $"""
        var QUICKMARKUP_EFFECTS = new List<IReference>();
        {sfc.Scirpt.RawScript}
        {GenFromNode((sfc.Template.Children[0] as QuickMarkupXMLNode)!, ref counterRef, out var nodeName)}
        return {nodeName};
        """;
    }
    private static string GenFromNode(QuickMarkupXMLNode node, ref int counterRef, out string varNameOut)
    {
        string varName = varNameOut = $"QUICKMARKUP_NODE_{counterRef++}";
        var code = new StringBuilder();
        code.AppendLine($"var {varName} = new {node.Name}();");
        void AddProperty(QuickMarkupXMLPropertiesKeyValue prop, ref int counterRef)
        {
            if (prop is QuickMarkupXMLPropertiesKeyForeign foreign)
            {
                code.AppendLine($$"""
                QUICKMARKUP_EFFECTS.Add(ReferenceTracker.RunAndRerunOnReferenceChange(() => {
                    return {{foreign.ForeignAsString}};
                }, x => {
                    {{varName}}.{{prop.Key}} = x;
                }));
                """);
            }
            else if (prop is QuickMarkupXMLPropertiesKeyXML xml)
            {
                code.AppendLine($"""
                    {GenFromNode(xml.Value, ref counterRef, out var childVarName)}
                    {varName}.{prop.Key} = {childVarName};
                    """);
            }
            else
            {
                code.AppendLine($"""
                {varName}.{prop.Key} = {prop switch
                {
                    QuickMarkupXMLPropertiesKeyString str => $"\"{SymbolDisplay.FormatLiteral(str.Value, false)}\"",
                    QuickMarkupXMLPropertiesKeyBoolean boolean => boolean.Value ? "true" : "false",
                    QuickMarkupXMLPropertiesKeyInt32 int32 => int32.Value.ToString(),
                    _ => throw new NotImplementedException()
                }};
                """);
            }
        }
        foreach (var prop in node.Properties)
        {
            AddProperty(prop, ref counterRef);
        }
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupXMLPropertiesKeyValue prop)
                AddProperty(prop, ref counterRef);
        }
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupXMLNode childNode)
            {
                code.AppendLine($"""
                    {GenFromNode(childNode, ref counterRef, out var childNodeName)}
                    {varName}.Children.Add({childNodeName});
                    """);
            }
        }
        return code.ToString();
    }
}
