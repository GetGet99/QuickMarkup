using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;

namespace QuickMarkup.Snapshot.SourceGen.Binders;

class SnapshotBinder(CodeTypeResolver resolver, Action<QMBinderError> onError) : Binder(onError)
{
    protected readonly QuickMarkupBinderUtilities utils = new(resolver);
    public List<SnapshotField> Bind(IEnumerable<RefDeclaration> declarations, SnasphostConfiguration configuration)
    {
        List<SnapshotField> included = [];
        foreach (var declaration in declarations)
        {
            bool? shouldInclude = null;
            string? jsonName = null;
            bool hasExplicitName = false;
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
                            if (string.IsNullOrWhiteSpace(str.Value))
                            {
                                Error(attribute.AttributeName, "[SnapshotInclude(\"...\")] requires a non-empty, non-whitespace key.");
                            }
                            else
                            {
                                jsonName = str.Value;
                                hasExplicitName = true;
                            }
                        }
                    }
                }
            }
            var isIncluded = shouldInclude ?? (configuration.SnapshotMode.HasFlag(SnapshotStateMode.IncludesPublic) && declaration.Accessibility is ResolvedAccessibility.Public);
            if (isIncluded && declaration.IsComputedDeclaration)
            {
                Error(declaration.Name, "Derived/computed value should not be saved.");
                continue;
            }
            if (isIncluded && configuration.DiagnosticMode.HasFlag(SnapshotDiagnosticMode.NoName) && !hasExplicitName)
            {
                Warn(
                    declaration.Name,
                    $"This key may unintentionally be changed after renaming field. Please include the key explicitly with [SnapshotInclude(\"{declaration.Name.Name}\")]."
                );
            }
            if (isIncluded)
                included.Add(new(declaration.Name.Name, jsonName ?? declaration.Name.Name, resolver.GetTypeSymbol(declaration.Type.Type)));
        }
        return included;
    }
}

internal record struct SnapshotField(string FieldName, string JsonName, INamedTypeSymbol? FieldType);
