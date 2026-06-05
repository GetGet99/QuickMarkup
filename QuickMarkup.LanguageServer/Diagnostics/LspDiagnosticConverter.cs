using Get.Parser;
using Get.PLShared;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.Parser;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace QuickMarkup.LanguageServer.Diagnostics;

public static class LspDiagnosticConverter
{
    const string SourceName = "QuickMarkup";



    internal static List<Diagnostic> ConvertParseErrors(List<ErrorTerminalValue> errors, string sourceText)
    {
        var results = new List<Diagnostic>(errors.Count);
        foreach (var error in errors)
        {
            switch (error.Value)
            {
                case LRParserRuntimeUnexpectedInputException unexpectedInput:
                    results.Add(new()
                    {
                        Range = PositionConverter.ToLspRange(unexpectedInput.UnexpectedElement.Start, unexpectedInput.UnexpectedElement.End),
                        Severity = DiagnosticSeverity.Error,
                        Code = QMDiagnosticHelper.ParseErrorUnexpectedInputCode,
                        Source = SourceName,
                        Message = $"Unexpected {unexpectedInput.UnexpectedElement}"
                    });
                    break;
                case LRParserRuntimeUnexpectedEndingException unexpectedEnding:
                    results.Add(new()
                    {
                        Range = PositionConverter.ToLspRange(error.Start, error.End),
                        Severity = DiagnosticSeverity.Error,
                        Code = QMDiagnosticHelper.ParseErrorUnexpectedEndingCode,
                        Source = SourceName,
                        Message = $"Expect {string.Join(", ", (object?[])unexpectedEnding.ExpectedInputs)} after the last parameter"
                    });
                    break;
            }
        }
        return results;
    }

    static Range RangeFromNode(AST.AST? node)
    {
        if (node is null)
            return new();
        return PositionConverter.ToLspRange(node.Start, node.End);
    }

    static Diagnostic ConvertSingle(QMDiagnostic diag)
    {
        var range = RangeFromNode(diag.Node);
        return new()
        {
            Range = range,
            Severity = diag is QMBinderWarning ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
            Code = diag.GetDiagnosticCode(),
            Source = SourceName,
            Message = diag.Message
        };
    }

    internal static List<Diagnostic> ConvertAll(
        IReadOnlyList<QMDiagnostic> binderDiagnostics,
        List<ErrorTerminalValue> parseErrors)
    {
        var results = new List<Diagnostic>(binderDiagnostics.Count + parseErrors.Count);
        results.AddRange(ConvertParseErrors(parseErrors));
        foreach (var diag in binderDiagnostics)
            results.Add(ConvertSingle(diag));
        return results;
    }

    internal static List<Diagnostic> ConvertParseErrors(List<ErrorTerminalValue> errors)
        => ConvertParseErrors(errors, "");
}
