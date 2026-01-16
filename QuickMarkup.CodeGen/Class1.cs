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
        {ImportNamespaceSafetyGenFromNode((sfc.Template.Children[0] as QuickMarkupQMNode)!, ref counterRef, out var nodeName)}
        return {nodeName};
        """;
    }
    private static string ImportNamespaceSafetyGenFromNode(QuickMarkupQMNode node, ref int counterRef, out string varNameOut)
    {
        string varName = varNameOut = $"QUICKMARKUP_NODE_{counterRef++}";
        var code = new StringBuilder();
        code.AppendLine($"var {varName} = new {node.Name}();");
        void AddProperty(QuickMarkupQMPropertiesKeyValue prop, ref int counterRef)
        {
            if (prop is QuickMarkupQMPropertiesKeyForeign foreign)
            {
                code.AppendLine($$"""
                QUICKMARKUP_EFFECTS.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange(() => {
                    return {{foreign.ForeignAsString}};
                }, x => {
                    {{varName}}.{{prop.Key}} = x;
                }));
                """);
            }
            else if (prop is QuickMarkupQMPropertiesKeyQM markup)
            {
                code.AppendLine($"""
                    {GenFromNode(markup.Value, ref counterRef, out var childVarName)}
                    {varName}.{prop.Key} = {childVarName};
                    """);
            }
            else
            {
                code.AppendLine($"""
                {varName}.{prop.Key} = {prop switch
                {
                    QuickMarkupQMPropertiesKeyString str => $"\"{SymbolDisplay.FormatLiteral(str.Value, false)}\"",
                    QuickMarkupQMPropertiesKeyBoolean boolean => boolean.Value ? "true" : "false",
                    QuickMarkupQMPropertiesKeyInt32 int32 => int32.Value.ToString(),
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
            if (child is QuickMarkupQMPropertiesKeyValue prop)
                AddProperty(prop, ref counterRef);
        }
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupQMNode childNode)
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
        {GenFromNode((sfc.Template.Children[0] as QuickMarkupQMNode)!, ref counterRef, out var nodeName)}
        return {nodeName};
        """;
    }
    private static string GenFromNode(QuickMarkupQMNode node, ref int counterRef, out string varNameOut)
    {
        string varName = varNameOut = $"QUICKMARKUP_NODE_{counterRef++}";
        var code = new StringBuilder();
        code.AppendLine($"var {varName} = new {node.Name}();");
        void AddProperty(QuickMarkupQMPropertiesKeyValue prop, ref int counterRef)
        {
            if (prop is QuickMarkupQMPropertiesKeyForeign foreign)
            {
                code.AppendLine($$"""
                QUICKMARKUP_EFFECTS.Add(ReferenceTracker.RunAndRerunOnReferenceChange(() => {
                    return {{foreign.ForeignAsString}};
                }, x => {
                    {{varName}}.{{prop.Key}} = x;
                }));
                """);
            }
            else if (prop is QuickMarkupQMPropertiesKeyQM markup)
            {
                code.AppendLine($"""
                    {GenFromNode(markup.Value, ref counterRef, out var childVarName)}
                    {varName}.{prop.Key} = {childVarName};
                    """);
            }
            else
            {
                code.AppendLine($"""
                {varName}.{prop.Key} = {prop switch
                {
                    QuickMarkupQMPropertiesKeyString str => $"\"{SymbolDisplay.FormatLiteral(str.Value, false)}\"",
                    QuickMarkupQMPropertiesKeyBoolean boolean => boolean.Value ? "true" : "false",
                    QuickMarkupQMPropertiesKeyInt32 int32 => int32.Value.ToString(),
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
            if (child is QuickMarkupQMPropertiesKeyValue prop)
                AddProperty(prop, ref counterRef);
        }
        foreach (var child in node.Children)
        {
            if (child is QuickMarkupQMNode childNode)
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
