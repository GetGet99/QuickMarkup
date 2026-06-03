using Get.Lexer;
using Get.Parser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.LanguageServer.Contracts;
using QuickMarkup.Parser;
using LspDiagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;

namespace QuickMarkup.LanguageServer.Diagnostics;

public class QmuiDiagnosticService : IQmuiDiagnosticService
{
    readonly IRoslynWorkspaceManager _workspaceManager;

    public QmuiDiagnosticService(IRoslynWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    public Task<IReadOnlyList<LspDiagnostic>> GetDiagnosticsAsync(
        string filePath, string content, CancellationToken ct)
    {
        var (sfc, parseErrors) = ParseContent(content);
        var compilation = _workspaceManager.Compilation;

        if (sfc is null)
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>([]);

        if (compilation is null)
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
                LspDiagnosticConverter.ConvertParseErrors(parseErrors, content));

        var ns = sfc.Namespace?.Name ?? "";
        var typeName = sfc.ClassDeclaration?.Name ?? "";
        if (string.IsNullOrEmpty(typeName))
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
                LspDiagnosticConverter.ConvertParseErrors(parseErrors, content));

        var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
        var typeSym = compilation.GetTypeByMetadataName(fullName);
        if (typeSym is not null)
        {
            var binder = Bind(compilation, sfc, typeSym, ns);
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
                LspDiagnosticConverter.ConvertAll(binder.Diagnostics, parseErrors, content));
        }
        if (sfc.ClassDeclaration is { } classDecl)
        {
            var effectiveBaseTypes = classDecl.Kind switch
            {
                ClassKind.Component => $"global::QuickMarkup.Infra.IQuickMarkupComponent<{classDecl.BaseTypes}>",
                ClassKind.FragmentComponent => $"global::QuickMarkup.Infra.IQuickMarkupFragmentComponent<{classDecl.BaseTypes}>",
                _ => classDecl.BaseTypes ?? ""
            };
            var baseClause = string.IsNullOrEmpty(effectiveBaseTypes) ? "" : $" : {effectiveBaseTypes}";

            // Type not found: create a dummy class and add it to the compilation
            var dummySource = $$"""
            #nullable enable
            {{sfc.Usings}}
            namespace {{ns}} {
                partial class {{typeName}}{{baseClause}} { }
            }
            """;

            // Parse the dummy source
            var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
            var dummyTree = CSharpSyntaxTree.ParseText(dummySource, parseOptions);

            // Add the dummy syntax tree to the compilation
            var compilationWithDummy = compilation.AddSyntaxTrees(dummyTree);

            // Get the type symbol from the new compilation
            var dummyTypeSym = compilationWithDummy.GetTypeByMetadataName(fullName);
            if (dummyTypeSym is null)
            {
                // Fallback to just parse errors if we still can't find the type (should not happen)
                goto fallback;
            }

            var dummyBinder = Bind(compilationWithDummy, sfc, dummyTypeSym, ns);
            return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
                LspDiagnosticConverter.ConvertAll(dummyBinder.Diagnostics, parseErrors, content));
        }
    fallback:
        return Task.FromResult<IReadOnlyList<LspDiagnostic>>(
            LspDiagnosticConverter.ConvertParseErrors(parseErrors, content));
    }

    static (QuickMarkupSFC? sfc, List<ErrorTerminalValue> errors) ParseContent(string content)
    {
        QuickMarkupLexer? lexer = null;
        for (int i = 0; i < 10; i++)
        {
            try
            {
                lexer = new QuickMarkupLexer(new StringTextSeeker(content));
                break;
            }
            catch { }
        }
        lexer ??= new QuickMarkupLexer(new StringTextSeeker(content));

        QuickMarkupParser? parser = null;
        for (int i = 0; i < 10; i++)
        {
            try
            {
                parser = new QuickMarkupParser();
                break;
            }
            catch { }
        }
        parser ??= new QuickMarkupParser();

        try
        {
            var tokens = lexer.GetTokens();
            var sfc = parser.Parse(tokens, out var errors);
            return (sfc, errors);
        }
        catch
        {
            return (null, []);
        }
    }

    static QuickMarkupBinder Bind(
        Compilation compilation,
        QuickMarkupSFC sfc,
        INamedTypeSymbol typeSym,
        string ns)
    {
        var resolver = new CodeTypeResolver(compilation, sfc.Usings, ns);
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
