using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.LanguageServer.Contracts;
using LspDiagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;

namespace QuickMarkup.LanguageServer.Diagnostics;

public class QmuiDiagnosticService : IQmuiDiagnosticService
{
    readonly IQmuiWorkspaceService _workspace;

    public QmuiDiagnosticService(IQmuiWorkspaceService workspace)
    {
        _workspace = workspace;
    }

    public async Task<IReadOnlyList<LspDiagnostic>> GetDiagnosticsAsync(
        string filePath, string content, CancellationToken ct)
    {
        var (sfc, parseErrors) = _workspace.Catalog.GetOrParse(filePath, content);
        if (sfc is null)
            return [];

        var compilation = await _workspace.GetEnrichedCompilationAsync(ct);
        if (compilation is null)
            return LspDiagnosticConverter.ConvertParseErrors(parseErrors, content);

        var generatedMembers = _workspace.GetGeneratedMemberTable();
        var ns = sfc.Namespace?.Name ?? "";

        var effectiveCompilation = EnsureTypeInCompilation(compilation, sfc, ns, filePath);
        if (effectiveCompilation is null)
            return LspDiagnosticConverter.ConvertParseErrors(parseErrors, content);

        var analysis = QuickMarkupFileAnalyzer.Analyze(
            sfc, filePath, ns, effectiveCompilation, generatedMembers, failFast: false);

        return LspDiagnosticConverter.ConvertAll(analysis.Diagnostics, parseErrors);
    }

    static Compilation? EnsureTypeInCompilation(Compilation compilation, QuickMarkupSFC sfc, string ns, string filePath)
    {
        var typeName = sfc.ClassDeclaration?.Name;
        if (string.IsNullOrEmpty(typeName))
            return compilation;

        var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
        if (compilation.GetTypeByMetadataName(fullName) is not null)
            return compilation;

        if (sfc.ClassDeclaration is null)
            return null;

        var target = new QuickMarkupTargetContext(ns, typeName, fullName, filePath, default, default);
        return QuickMarkupCompilationEnricher.EnsureTypeSymbolInCompilation(target, sfc, compilation);
    }
}
