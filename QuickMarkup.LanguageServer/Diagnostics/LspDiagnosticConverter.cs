using Get.Parser;
using Get.PLShared;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using QuickMarkup.AST;
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
                        Code = "QM1001",
                        Source = SourceName,
                        Message = $"Unexpected {unexpectedInput.UnexpectedElement}"
                    });
                    break;
                case LRParserRuntimeUnexpectedEndingException unexpectedEnding:
                    results.Add(new()
                    {
                        Range = PositionConverter.ToLspRange(error.Start, error.End),
                        Severity = DiagnosticSeverity.Error,
                        Code = "QM1002",
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

    static DiagnosticSeverity Severity(QMDiagnostic diag) => diag switch
    {
        QMBinderWarning => DiagnosticSeverity.Warning,
        QMBinderError => DiagnosticSeverity.Error,
        _ => DiagnosticSeverity.Error
    };

    static string DiagnosticCode(QMDiagnostic diag) => diag switch
    {
        QMBinderPropertyUnknownError => "QM1006",
        QMBinderEnumMemberUnknownError => "QM1007",
        QMBinderTypeUnknownError => "QM1008",
        QMBinderChildrenTooMany => "QM1004",
        QMBinderTagMismatchedError => "QM1009",
        QMBinderTagUnexpectedError => "QM1010",
        QMBinderTypeMismatchError => "QM1011",
        _ => "QM1003"
    };

    static Diagnostic ConvertSingle(QMDiagnostic diag)
    {
        var range = RangeFromNode(diag.Node);
        return new()
        {
            Range = range,
            Severity = Severity(diag),
            Code = DiagnosticCode(diag),
            Source = SourceName,
            Message = diag.Message
        };
    }

    internal static List<Diagnostic> ConvertAll(
        List<QMDiagnostic> binderDiagnostics,
        List<ErrorTerminalValue> parseErrors,
        string sourceText)
    {
        var results = new List<Diagnostic>(binderDiagnostics.Count + parseErrors.Count + 1);
        results.AddRange(ConvertParseErrors(parseErrors, sourceText));
        foreach (var diag in binderDiagnostics)
            results.Add(ConvertSingle(diag));
        return results;
    }
}
