using Microsoft.CodeAnalysis;
using QuickMarkup.CodeAnalysis;

namespace QuickMarkup.LanguageServer.Contracts;

public interface IMemberTableService
{
    QuickMarkupGeneratedMemberTable GetTable();

    Task RebuildAllAsync(Compilation enrichedCompilation, CancellationToken ct = default);

    Task InvalidateTypeAsync(string filePath, Compilation enrichedCompilation, CancellationToken ct = default);

    event EventHandler<QuickMarkupGeneratedMemberTable> TableUpdated;
}
