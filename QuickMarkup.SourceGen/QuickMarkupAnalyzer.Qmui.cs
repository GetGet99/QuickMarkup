using System.Collections.Immutable;
using Get.Parser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using static QuickMarkup.SourceGen.QuickMarkupDiagnosticReporter;

namespace QuickMarkup.SourceGen;

partial class QuickMarkupAnalyzer
{
    void InitializeQmuiAnalysis(AnalysisContext context)
    {
        context.RegisterCompilationAction(compilationContext =>
        {
            var qmuiFiles = compilationContext.Options.AdditionalFiles
                .Where(f => f.Path.EndsWith(".qmui", StringComparison.OrdinalIgnoreCase))
                .ToImmutableArray();

            if (qmuiFiles.Length == 0) return;

            foreach (var file in qmuiFiles)
            {
                compilationContext.CancellationToken.ThrowIfCancellationRequested();
                AnalyzeQmuiFile(compilationContext, file);
            }
        });
    }

    void AnalyzeQmuiFile(CompilationAnalysisContext context, AdditionalText file)
    {
        var ct = context.CancellationToken;
        var content = file.GetText(ct)?.ToString();
        if (content is null) return;

        var locationProvider = new QuickMarkupQmuiLocationProvider(file.Path, content);

        QuickMarkupSFC sfc;
        List<ErrorTerminalValue> errors;
        try
        {
            (sfc, errors) = QuickMarkupProviderExtension.ParseWithErrorsCore(content);
        }
        catch (Exception e) when (TryHandleParseException(e, locationProvider, d => context.ReportDiagnostic(d)))
        {
            return;
        }
        ReportErrorTerminals(errors, locationProvider, d => context.ReportDiagnostic(d));

        var ns = sfc.Namespace?.Name ?? "";
        var typeName = sfc.ClassDeclaration?.Name ?? "";
        if (string.IsNullOrEmpty(typeName))
            return;

        var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
        var target = new QuickMarkupTargetContext(
            Namespace: ns,
            TypeName: typeName,
            FullTypeName: fullName,
            FileName: file.Path,
            AttributeLocation: default,
            AttributeLineSpan: default
        );

        if (!target.TryGetTypeSymbol(context.Compilation, out var typeSym, out _))
            return;

        var frameworkConfig = FrameworkConfigurationReader.ReadFromCompilation(context.Compilation) ?? FrameworkConfiguration.Default;
        var resolver = new CodeTypeResolver(context.Compilation, sfc.Usings, target.Namespace, frameworkConfiguration: frameworkConfig);
        var binder = new QuickMarkupBinder(resolver, Binder.Collect);

        if (sfc.Template is not null)
        {
            try
            {
                binder.Bind(sfc.Template, typeSym);
            }
            catch (Exception e)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    BindErrorGeneral,
                    locationProvider.Fallback,
                    e.Message
                ));
            }
        }

        try
        {
            _ = binder.BindRefDeclarations(sfc.Refs, typeSym);
        }
        catch (Exception e)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                BindErrorGeneral,
                locationProvider.Fallback,
                e.Message
            ));
        }

        ReportBinderDiagnostics(binder.Diagnostics, locationProvider, resolver, d => context.ReportDiagnostic(d));
    }
}
