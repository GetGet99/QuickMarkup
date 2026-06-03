using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace QuickMarkup.LanguageServer.Contracts;

public interface IQmuiDiagnosticService
{
    Task<IReadOnlyList<Diagnostic>> GetDiagnosticsAsync(
        string filePath, string content, CancellationToken ct
    );
}
