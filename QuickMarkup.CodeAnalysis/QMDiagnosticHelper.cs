using QuickMarkup.CodeAnalysis.Binders;

namespace QuickMarkup.CodeAnalysis;

public static class QMDiagnosticHelper
{
    public const string ParseErrorUnexpectedInputCode = "QM1001";
    public const string ParseErrorUnexpectedEndingCode = "QM1002";

    public static string GetDiagnosticCode(this QMDiagnostic diagnostic) => diagnostic switch
    {
        QMBinderPropertyUnknownError => "QM1006",
        QMBinderEnumMemberUnknownError => "QM1007",
        QMBinderChildrenTooMany => "QM1004",
        QMBinderTypeUnknownError => "QM1008",
        QMBinderTagMismatchedError => "QM1009",
        QMBinderTagUnexpectedError => "QM1010",
        QMBinderTypeMismatchError => "QM1011",
        _ => "QM1003"
    };
}
