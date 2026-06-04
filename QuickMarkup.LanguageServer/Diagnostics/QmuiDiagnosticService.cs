using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
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
        var (sfc, parseErrors) = QuickMarkupProviderExtension.ParseWithErrors(content);
        var compilation = await _workspace.GetEnrichedCompilationAsync(ct);

        if (sfc is null)
            return [];

        if (compilation is null)
            return LspDiagnosticConverter.ConvertParseErrors(parseErrors, content);

        var generatedMembers = _workspace.GetGeneratedMemberTable();

        var ns = sfc.Namespace?.Name ?? "";
        var typeName = sfc.ClassDeclaration?.Name ?? "";
        if (string.IsNullOrEmpty(typeName))
            return LspDiagnosticConverter.ConvertParseErrors(parseErrors, content);

        var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
        var typeSym = compilation.GetTypeByMetadataName(fullName);
        if (typeSym is not null)
        {
            var binder = Bind(compilation, sfc, typeSym, ns, generatedMembers);
            return LspDiagnosticConverter.ConvertAll(binder.Diagnostics, parseErrors, content);
        }
        if (sfc.ClassDeclaration is { } classDecl)
        {
            var target = new QuickMarkupTargetContext(
                Namespace: ns,
                TypeName: typeName,
                FullTypeName: fullName,
                FileName: filePath,
                AttributeLocation: default,
                AttributeLineSpan: default);

            var compilationWithDummy = QuickMarkupCompilationEnricher.EnsureTypeSymbolInCompilation(target, sfc, compilation);

            var dummyTypeSym = compilationWithDummy.GetTypeByMetadataName(fullName);
            if (dummyTypeSym is null)
                return LspDiagnosticConverter.ConvertParseErrors(parseErrors, content);

            var dummyBinder = Bind(compilationWithDummy, sfc, dummyTypeSym, ns, generatedMembers);
            return LspDiagnosticConverter.ConvertAll(dummyBinder.Diagnostics, parseErrors, content);
        }

        return LspDiagnosticConverter.ConvertParseErrors(parseErrors, content);
    }

    static QuickMarkupBinder Bind(
        Compilation compilation,
        QuickMarkupSFC sfc,
        INamedTypeSymbol typeSym,
        string ns,
        QuickMarkupGeneratedMemberTable generatedMembers)
    {
        var resolver = new CodeTypeResolver(compilation, sfc.Usings, ns, generatedMembers);
        var binder = new QuickMarkupBinder(resolver, failFast: false);

        if (sfc.Template is not null)
        {
            try
            {
                binder.Bind(sfc.Template, typeSym);
            }
            catch (Exception e)
            {
                binder.Diagnostics.Add(new QMBinderError(
                    sfc.Template, $"Internal error during binding: {e.Message}"));
            }
        }

        try
        {
            binder.BindRefDeclarations(sfc.Refs, typeSym);
        }
        catch (Exception e)
        {
            binder.Diagnostics.Add(new QMBinderError(
                sfc, $"Internal error during ref binding: {e.Message}"));
        }

        return binder;
    }
}
