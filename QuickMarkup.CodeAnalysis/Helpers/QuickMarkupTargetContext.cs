using Get.EasyCSharp.GeneratorTools;
using Get.EasyCSharp.GeneratorTools.SyntaxCreator.Members;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics.CodeAnalysis;

namespace QuickMarkup.CodeAnalysis.Helpers;

/// <summary>
/// Represents the target class and position that <see cref="QuickMarkupAttribute"/> is appleid to.
/// </summary>
/// <param name="Namespace">The namespace of the target class</param>
/// <param name="TypeName">Type name, without namespace</param>
/// <param name="FullTypeName">Full type name, in strnig</param>
/// <param name="FileName"></param>
/// <param name="AttributeLocation"></param>
/// <param name="AttributeLineSpan"></param>
public readonly record struct QuickMarkupTargetContext(
    string Namespace,
    string TypeName,
    string FullTypeName,
    string? FileName,
    TextSpan AttributeLocation,
    LinePositionSpan AttributeLineSpan
)
{
    static readonly SymbolDisplayFormat withoutNamespace = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    public static QuickMarkupTargetContext FromSyntaxAndSymbol(ITypeSymbol type, SyntaxReference? syntaxReference, CancellationToken ct)
    {
        var name = type.ToDisplayString(withoutNamespace);
        var syntaxTree = syntaxReference?.SyntaxTree ?? type.Locations[0].SourceTree;
        var attributeLocation = syntaxReference is null ? type.Locations[0].SourceSpan : syntaxReference.Span;
        if (syntaxReference?.GetSyntax(ct) is AttributeSyntax attrSyntax)
        {
            // move fallback to just the attribute name
            attributeLocation = attrSyntax.Name.Span;
        }
        var linespan = syntaxReference?.SyntaxTree.GetLineSpan(attributeLocation, ct) ?? default;

        return new QuickMarkupTargetContext(
            Namespace: type.ContainingNamespace.ToString(),
            TypeName: type.ToDisplayString(withoutNamespace),
            FullTypeName: new FullType(type).TypeWithNamespace,
            FileName: syntaxReference?.SyntaxTree.FilePath,
            AttributeLocation: attributeLocation,
            AttributeLineSpan: new(linespan.StartLinePosition, linespan.EndLinePosition)
        );

    }
    /// <summary>
    /// Gets type symbol of the target attribute. May fail or throw exception
    /// </summary>
    /// <param name="compilation">The compilation</param>
    /// <returns>The type symbol of the target element</returns>
    public INamedTypeSymbol GetTypeSymbol(Compilation compilation)
    {
        INamedTypeSymbol? typeSymbol;
        string searchTypeName;
        if (FullTypeName.StartsWith("global::"))
        {
            searchTypeName = FullTypeName["global::".Length..];
        }
        else
        {
            searchTypeName = FullTypeName;
        }
        var idx = searchTypeName.IndexOf('<');
        if (idx >= 0)
        {
            searchTypeName = searchTypeName[..idx];
        }
        typeSymbol = compilation.GetTypeByMetadataName(searchTypeName);
        return typeSymbol ?? throw new NullReferenceException($"compilation.GetTypeByMetadataName(\"{searchTypeName}\") returns null");
    }
    /// <summary>
    /// Gets type symbol of the target attribute. On failure, will return null
    /// </summary>
    /// <param name="compilation">The compilation</param>
    /// <param name="failureReason">Output exception of why retriving type failed</param>
    /// <returns>The type symbol of the target element</returns>
    public bool TryGetTypeSymbol(Compilation compilation, [NotNullWhen(true)] out INamedTypeSymbol? symbol, [NotNullWhen(false)] out Exception? failureReason)
    {
        try
        {
            var type = GetTypeSymbol(compilation);
            failureReason = null;
            symbol = type;
            return true;
        }
        catch (Exception e)
        {
            failureReason = e;
            symbol = null;
            return false;
        }
    }
    public string TypeNameSourceGenOutputFriendlyFileName
    {
        get
        {
            var name = FullTypeName;
            if (name.StartsWith("global::"))
            {
                name = name["global::".Length..];
            }
            name = name.Replace('<', '[').Replace('>', ']');
            return name;
        }
    }
}
