using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;

namespace QuickMarkup.Snapshot.SourceGen.Binders;

class SnapshotBinder(CodeTypeResolver resolver, bool failFast = false) : Binder(failFast)
{
    protected readonly QuickMarkupBinderUtilities utils = new(resolver);
    public List<SnapshotField> Bind(IEnumerable<RefDeclaration> declarations, SnasphostConfiguration configuration)
    {
        List<SnapshotField> included = [];
        foreach (var declaration in declarations)
        {
            bool? shouldInclude = null;
            string? jsonName = null;
            foreach (var attribute in declaration.Attributes)
            {
                if (attribute.AttributeName.Name is "SnapshotIgnore")
                {
                    if (shouldInclude is not null)
                    {
                        Error(attribute.AttributeName, $"only one [SnapshotIgnore] or [SnaspshotInclude] must be used");
                    }
                    shouldInclude = false;
                }
                else if (attribute.AttributeName.Name is "SnapshotInclude")
                {
                    if (shouldInclude is not null)
                    {
                        Error(attribute.AttributeName, $"only one [SnapshotIgnore] or [SnaspshotInclude] must be used");
                    }
                    shouldInclude = true;

                    if (attribute.Arguments.Positionals.Count > 0)
                    {
                        if (attribute.Arguments.Positionals[0] is QuickMarkupString str)
                        {
                            jsonName = str.Value;
                        }
                    }
                }
            }
            if (configuration.DiagnosticMode.HasFlag(SnapshotDiagnosticMode.Public))
            {
                if (shouldInclude is null)
                    Warn(
                        declaration.Name,
                        $"This public field may unintentionally {(configuration.SnapshotMode.HasFlag(SnapshotStateMode.IncludesPublic) ? "" : "not ")}be serialized. Please include [SnapshotIgnore] or [SnaspshotInclude] attribute explicitly."
                    );
            }
            if (configuration.DiagnosticMode.HasFlag(SnapshotDiagnosticMode.NoName))
            {
                if (shouldInclude is null)
                    Warn(
                        declaration.Name,
                        $"This key may unintentionally be changed after renaming field. Please include the key explicitly with [SnapshotInclude(\"{declaration.Name.Name}\")]."
                    );
            }
            if (shouldInclude ?? (configuration.SnapshotMode.HasFlag(SnapshotStateMode.IncludesPublic) && !declaration.IsPrivate))
                included.Add(new(declaration.Name.Name, jsonName ?? declaration.Name.Name, resolver.GetTypeSymbol(declaration.Type.Type)));
        }
        return included;
    }
}

internal record struct SnapshotField(string fieldName, string jsonName, INamedTypeSymbol? fieldType);