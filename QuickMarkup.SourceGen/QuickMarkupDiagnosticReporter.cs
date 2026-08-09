using Get.Parser;
using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Parser;

namespace QuickMarkup.SourceGen;

static class QuickMarkupDiagnosticReporter
{
    public static bool TryHandleParseException(Exception e, IQuickMarkupLocationProvider loc, Action<Diagnostic> report)
    {
        if (e is LRParserRuntimeUnexpectedInputException input)
        {
            report(Diagnostic.Create(
                QuickMarkupAnalyzer.ParseErrorUnexpectedInput,
                loc.GetLocation(input.UnexpectedElement.Start, input.UnexpectedElement.End),
                input.UnexpectedElement
            ));
            return true;
        }

        if (e is LRParserRuntimeUnexpectedEndingException ending)
        {
            report(Diagnostic.Create(
                QuickMarkupAnalyzer.ParseErrorUnexpectedEnding,
                loc.Fallback,
                $"{string.Join(", ", (object?[])ending.ExpectedInputs)} after the last parameter"
            ));
            return true;
        }

        if (e is QuickMarkupTagMismatchException tag)
        {
            var startTagName = tag.FaultedTag.TagStart.TagName;
            var endTagName = tag.FaultedTag.EndTagName;
            report(Diagnostic.Create(
                QuickMarkupAnalyzer.TagCloseMismatchedError,
                loc.GetLocation(tag.FaultedTag.TagStart.TagIdentifierAST),
                startTagName,
                endTagName
            ));
            report(Diagnostic.Create(
                QuickMarkupAnalyzer.TagCloseMismatchedError,
                loc.GetLocation(tag.FaultedTag.EndTagName),
                startTagName,
                endTagName
            ));
            return true;
        }

        report(Diagnostic.Create(
            QuickMarkupAnalyzer.BindErrorGeneral,
            loc.Fallback,
            $"Internal parser error: {e.Message}"
        ));
        return true;
    }

    public static void ReportErrorTerminals(List<ErrorTerminalValue> errors, IQuickMarkupLocationProvider loc, Action<Diagnostic> report)
    {
        foreach (var error in errors)
        {
            if (error.Value is LRParserRuntimeUnexpectedInputException unexpectedInput)
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.ParseErrorUnexpectedInput,
                    loc.GetLocation(unexpectedInput.UnexpectedElement.Start, unexpectedInput.UnexpectedElement.End),
                    unexpectedInput.UnexpectedElement
                ));
            else if (error.Value is LRParserRuntimeUnexpectedEndingException unexpectedEnding)
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.ParseErrorUnexpectedEnding,
                    loc.GetLocation(error.Start, error.End),
                    $"{string.Join(", ", (object?[])unexpectedEnding.ExpectedInputs)} after the last parameter"
                ));
        }
    }

    public static void ReportBinderDiagnostics(IEnumerable<QMDiagnostic> diagnostics, IQuickMarkupLocationProvider loc, CodeTypeResolver resolver, Action<Diagnostic> report)
    {
        foreach (var diagnostic in diagnostics)
        {
            var dLoc = loc.GetLocation(diagnostic.Node);
            if (diagnostic is QMBinderPropertyUnknownError propertyUnknown)
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorPropertyUnknown,
                    dLoc,
                    resolver.GetTypeSymbol(propertyUnknown.TypeName),
                    propertyUnknown.PropertyName,
                    QMDiagnosticSuggestion.FormatSuggestions(propertyUnknown.Suggestions)
                ));
            }
            else if (diagnostic is QMBinderEnumMemberUnknownError enumMemberUnknown)
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorEnumMemberUnknown,
                    dLoc,
                    resolver.GetTypeSymbol(enumMemberUnknown.TypeName),
                    enumMemberUnknown.MemberName,
                    QMDiagnosticSuggestion.FormatSuggestions(enumMemberUnknown.Suggestions)
                ));
            }
            else if (diagnostic is QMBinderChildrenTooMany childrenTooMany)
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorChildrenTooMany,
                    dLoc,
                    childrenTooMany.ParentTagInfo.TagType as object ?? childrenTooMany.ParentTagInfo.TagName,
                    childrenTooMany.Expecting
                ));
            }
            else if (diagnostic is QMBinderTypeUnknownError typeUnknown)
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorTypeUnknown,
                    dLoc,
                    typeUnknown.TypeName
                ));
            }
            else if (diagnostic is QMBinderTagMismatchedError tagMismatched)
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorTagMismatched,
                    dLoc,
                    tagMismatched.TagStart,
                    tagMismatched.TagEnd
                ));
            }
            else if (diagnostic is QMBinderTagUnexpectedError tagUnexpected)
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorTagUnexpected,
                    dLoc,
                    tagUnexpected.ExpectedTag,
                    tagUnexpected.TagName
                ));
            }
            else if (diagnostic is QMBinderRequiredPropertyMissingError requiredMissing)
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorRequiredPropertyMissing,
                    dLoc,
                    requiredMissing.PropertyName,
                    requiredMissing.TypeName
                ));
            }
            else if (diagnostic is QMBinderTypeMismatchError typeMismatch)
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorTypeMismatch,
                    dLoc,
                    resolver.GetTypeSymbol(typeMismatch.ValueTypeName),
                    resolver.GetTypeSymbol(typeMismatch.PropertyTypeName)
                ));
            }
            else if (diagnostic is QMBinderRefMissingDefaultValueWarning missingDefault)
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorRefMissingDefaultValue,
                    dLoc,
                    missingDefault.RefName,
                    missingDefault.TypeName
                ));
            }
            else
            {
                report(Diagnostic.Create(
                    QuickMarkupAnalyzer.BindErrorGeneral,
                    dLoc,
                    diagnostic.Message
                ));
            }
        }
    }
}
