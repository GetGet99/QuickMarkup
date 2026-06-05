using LspLocation = OmniSharp.Extensions.LanguageServer.Protocol.Models.Location;

namespace QuickMarkup.LanguageServer.Contracts;

public interface ISymbolLocationResolver
{
    LspLocation? GetDefinitionLocation(TagResolutionResult? tagResult, string currentFilePath);
    LspLocation? GetDefinitionLocation(PropertyResolutionResult? propertyResult, string currentFilePath);
}
