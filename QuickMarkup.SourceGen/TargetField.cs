using Microsoft.CodeAnalysis;

namespace QuickMarkup.SourceGen;

public record TargetField(ITypeSymbol? Type, string FieldName)
{
    public override string ToString() => FieldName;
}
