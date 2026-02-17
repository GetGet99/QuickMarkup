using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.Language.Symbols;
using System.Text;

namespace QuickMarkup.SourceGen.CodeGen;

static class CodeSnippetsExtension
{
    extension(StringBuilder codeBuilder)
    {
        public void AddClosure(ITypeSymbol? type, string target, string labmdaExpression)
        {
            codeBuilder.AppendLine($"""
            global::QuickMarkup.Infra.CompilerHelpers.Closure{(type is null ? "" : $"<{type.FullName()}>")}(
                {target},
                {labmdaExpression}
            );
            """);
        }

        public void AddPropertyAssign(string target, string valueExpression)
        {
            codeBuilder.AppendLine($"""
            {target} = {valueExpression};
            """);
        }

        public void AddEventAssign(string target, string valueExpression)
        {
            codeBuilder.AppendLine($"""
            {target} += {valueExpression};
            """);
        }

        public void AddForEachStart(ITypeSymbol? targetType, string targetName, string iterable)
        {
            codeBuilder.AppendLine($$"""
            foreach ({{(targetType is null ? "var" : targetType.FullName())}} {{targetName}} in {{iterable}}) {
                global::QuickMarkup.Infra.CompilerHelpers.Closure{{(targetType is null ? "" : $"<{targetType.FullName()}>")}}(
                    {{targetName}},
                    ({{targetName}}) => {
            """);
        }

        public void AddForEachStart(ITypeSymbol? targetType, string targetName, QMRangeSymbol iterable)
        {
            codeBuilder.AppendLine($$"""
            for ({{(targetType is null ? "var" : targetType.FullName())}} {{targetName}} = {{iterable.Start}}; {{targetName}} < {{iterable.End}}; {{targetName}}++) {
                global::QuickMarkup.Infra.CompilerHelpers.Closure{{(targetType is null ? "" : $"<{targetType.FullName()}>")}}(
                    {{targetName}},
                    ({{targetName}}) => {
            """);
        }

        public void AddForEachEnd()
        {
            codeBuilder.AppendLine($$"""
                });
            }
            """);
        }

        public void AddMethodCall(string target, params string[] parameters)
        {
            codeBuilder.Append($"{target}(");
            if (parameters.Length > 0)
            {
                codeBuilder.Append(parameters[0]);
                for (int i = 1; i < parameters.Length; i++)
                {
                    codeBuilder.Append(", ");
                    codeBuilder.Append(parameters[i]);
                }
            }
            codeBuilder.Append($");");
        }

        public void AddPropertyBindOneWay(ITypeSymbol? type, string target, string valueExpression, string tempVarOutputName = "QUICKMARUP_TEMPVALUE")
        {
            codeBuilder.AppendLine($$"""
            QUICKMARKUP_EFFECTS.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange{{(
                            type is null ? "" : $"<{new FullType(type)}>"
                        )}} (() => {
                return {{valueExpression}};
            }, {{tempVarOutputName}} => {
                {{target}} = {{tempVarOutputName}};
            }));
            """);
        }

        public void AddDependencyPropertyBindBack(string target, string targetDependencyObject, string dependencyPropertyName, string valueExpression)
        {
            codeBuilder.AppendLine($$"""
                {{valueExpression}} = {{target}};
                {{targetDependencyObject}}.RegisterPropertyChangedCallback(
                    {{dependencyPropertyName}},
                    (_, _) => {
                        {{valueExpression}} = {{target}};
                    }
                );
                """);
        }
    }
}
