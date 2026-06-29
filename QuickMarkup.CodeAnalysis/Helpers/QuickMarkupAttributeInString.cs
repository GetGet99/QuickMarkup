using Microsoft.CodeAnalysis;
using QuickMarkup.AST;

namespace QuickMarkup.CodeAnalysis.Helpers;

readonly record struct QuickMarkupAttributeInString(
    QuickMarkupTargetContext Target,
    string MarkupString
);

readonly record struct QuickMarkupParsedAttributeResult(
    QuickMarkupTargetContext Target,
    QuickMarkupSFC? Result,
    string? Error
);

readonly record struct QuickMarkupParsedAttribute(
    QuickMarkupTargetContext Target,
    QuickMarkupSFC AST
);
readonly record struct QuickMarkupParseError(
    QuickMarkupTargetContext Target,
    string Error
);

record struct QuickMarkupAllParsedResult(IncrementalValuesProvider<QuickMarkupParsedAttribute> Successful, IncrementalValuesProvider<QuickMarkupParseError> Errors);