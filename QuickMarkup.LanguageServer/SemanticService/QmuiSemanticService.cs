using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;
using QuickMarkup.LanguageServer.Contracts;

namespace QuickMarkup.LanguageServer.SemanticService;

/// <summary>
/// Provides shared semantic services for QuickMarkup language features.
/// Encapsulates parsing, binding, and type resolution logic.
/// </summary>
public class QmuiSemanticService : IQmuiSemanticService
{
    private readonly IQmuiWorkspaceService _workspace;

    public QmuiSemanticService(IQmuiWorkspaceService workspace)
    {
        _workspace = workspace;
    }

    public async Task<CursorResolutionResult?> TryResolveAtPositionAsync(
        string filePath,
        string content,
        int line,
        int character,
        CancellationToken ct = default)
    {
        var (sfc, parseErrors) = QuickMarkupProviderExtension.ParseWithErrors(content);
        if (sfc is null)
            return null;

        var compilation = await _workspace.GetEnrichedCompilationAsync(ct);
        if (compilation is null)
            return null;

        var refResult = TryResolveRefDeclarationAtPosition(sfc, line, character, compilation);
        if (refResult is not null)
            return new CursorResolutionResult(null, refResult);

        var generatedMembers = _workspace.GetGeneratedMemberTable();

        if (sfc.Template is not null)
        {
            var result = ResolveAtPositionInNode(sfc.Template, null, sfc, compilation, generatedMembers, line, character);
            if (result is not null)
                return result;
        }

        return null;
    }

    private CursorResolutionResult? ResolveAtPositionInNode(
        IQMNodeChild node,
        QuickMarkupParsedTag? parentTag,
        QuickMarkupSFC sfc,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable generatedMembers,
        int line,
        int character)
    {
        if (node is QuickMarkupParsedTag tag)
        {
            if (tag.TagStart is QuickMarkupConstructor constructor)
            {
                var identifierAst = constructor.TagIdentifierAST as PositionedIdentifier;
                if (identifierAst is not null &&
                    identifierAst.Start.Line <= line && identifierAst.End.Line >= line &&
                    identifierAst.Start.Char <= character && identifierAst.End.Char >= character)
                {
                    var tagResult = ResolveTag(compilation, sfc, tag);
                    return new CursorResolutionResult(tagResult, null);
                }
            }

            if (tag.EndTagName is not null)
            {
                if (tag.EndTagName.Start.Line <= line && tag.EndTagName.End.Line >= line &&
                    tag.EndTagName.Start.Char <= character && tag.EndTagName.End.Char >= character)
                {
                    var tagResult = ResolveTag(compilation, sfc, tag);
                    return new CursorResolutionResult(tagResult, null);
                }
            }

            if (parentTag is not null)
            {
                if (tag.TagStart is QuickMarkupPropertyTagStart propertyTagStart)
                {
                    if (propertyTagStart.Start.Line <= line && propertyTagStart.End.Line >= line &&
                        propertyTagStart.Start.Char <= character && propertyTagStart.End.Char >= character)
                    {
                        var propResult = ResolvePropertyTagAtPosition(parentTag, sfc, compilation, generatedMembers, propertyTagStart.TagName);
                        if (propResult is not null)
                            return new CursorResolutionResult(null, propResult);
                    }
                }
                else if (tag.TagStart is QuickMarkupAttachedPropertyTagStart attachedPropertyTagStart)
                {
                    if (attachedPropertyTagStart.Start.Line <= line && attachedPropertyTagStart.End.Line >= line &&
                        attachedPropertyTagStart.Start.Char <= character && attachedPropertyTagStart.End.Char >= character)
                    {
                        var propResult = ResolveAttachedPropertyTagAtPosition(parentTag, sfc, compilation, generatedMembers, attachedPropertyTagStart);
                        if (propResult is not null)
                            return new CursorResolutionResult(null, propResult);
                    }
                }
            }

            if (tag.InlineMembers is not null)
            {
                foreach (var member in tag.InlineMembers)
                {
                    if (member is QuickMarkupParsedProperty property)
                    {
                        var keyLength = property.Key.Length;
                        var keyStart = property.Start;
                        var keyEnd = new Get.PLShared.Position(keyStart.Line, keyStart.Char + keyLength);

                        if (keyStart.Line <= line && keyEnd.Line >= line &&
                            keyStart.Char <= character && keyEnd.Char >= character)
                        {
                            var propResult = ResolvePropertyAtPosition(tag, sfc, compilation, generatedMembers, property);
                            if (propResult is not null)
                                return new CursorResolutionResult(null, propResult);
                        }
                    }
                }
            }

            if (tag.Children is not null)
            {
                foreach (var child in tag.Children)
                {
                    var childResult = ResolveAtPositionInNode(child, tag, sfc, compilation, generatedMembers, line, character);
                    if (childResult is not null)
                        return childResult;
                }
            }
        }
        else if (node is QuickMarkupParsedIfNode ifNode)
        {
            var result = ResolveAtPositionInNode(ifNode.BodyWhenTrue, parentTag, sfc, compilation, generatedMembers, line, character);
            if (result is not null) return result;

            if (ifNode.BodyWhenFalse is not null)
            {
                result = ResolveAtPositionInNode(ifNode.BodyWhenFalse, parentTag, sfc, compilation, generatedMembers, line, character);
                if (result is not null) return result;
            }
        }
        else if (node is QuickMarkupParsedForNode forNode)
        {
            var result = ResolveAtPositionInNode(forNode.Body, parentTag, sfc, compilation, generatedMembers, line, character);
            if (result is not null) return result;
        }
        else if (node is QuickMarkupParsedFragmentNode fragmentNode)
        {
            foreach (var child in fragmentNode.Children)
            {
                var childResult = ResolveAtPositionInNode(child, parentTag, sfc, compilation, generatedMembers, line, character);
                if (childResult is not null)
                    return childResult;
            }
        }

        return null;
    }

    private TagResolutionResult ResolveTag(Compilation compilation, QuickMarkupSFC sfc, QuickMarkupParsedTag tag)
    {
        var tagName = tag.TagStart.TagName;

        if (tagName == "root" && sfc.ClassDeclaration is not null)
        {
            var componentClass = ResolveComponentClass(compilation, sfc);
            var displayString = componentClass != null
                ? componentClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : sfc.ClassDeclaration.Name;

            return new TagResolutionResult(
                TagIdentifierAST: tag.TagStart.TagIdentifierAST,
                RawTagName: "root",
                ResolvedSymbol: componentClass,
                DisplayString: displayString);
        }

        var resolvedSymbol = TryResolveTagType(compilation, sfc, tag);
        var displayString2 = resolvedSymbol != null
            ? resolvedSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : $"{(resolvedSymbol == null ? "(unresolved) " : "")}{tagName}";

        return new TagResolutionResult(
            TagIdentifierAST: tag.TagStart.TagIdentifierAST,
            RawTagName: tagName,
            ResolvedSymbol: resolvedSymbol,
            DisplayString: displayString2);
    }

    private PropertyResolutionResult? TryResolveRefDeclarationAtPosition(
        QuickMarkupSFC sfc,
        int line,
        int character,
        Compilation compilation)
    {
        foreach (var refDecl in sfc.Refs)
        {
            // Check if cursor is on the type reference (before the name on the same line)
            var typeStr = refDecl.Type.Type;
            var typeStartChar = refDecl.Name.Start.Char - typeStr.Length - 1;
            if (refDecl.Name.Start.Line == line &&
                typeStartChar >= 0 &&
                character >= typeStartChar && character < refDecl.Name.Start.Char)
            {
                var typeSymbol = ResolveRefTypeSymbol(typeStr, sfc, compilation);
                var typeName = typeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? typeStr;

                return new PropertyResolutionResult(
                    PropertyAST: null,
                    RawPropertyName: typeStr,
                    RoslynSymbol: null,
                    GeneratedSymbol: null,
                    DisplayString: typeName,
                    Kind: PropertyResolutionKind.RefDeclarationType,
                    ResolvedTypeSymbol: typeSymbol);
            }

            // Check if cursor is on the ref name
            if (refDecl.Name.Start.Line <= line && refDecl.Name.End.Line >= line &&
                refDecl.Name.Start.Char <= character && refDecl.Name.End.Char >= character)
            {
                var typeSymbol = ResolveRefTypeSymbol(typeStr, sfc, compilation);

                var prefix = refDecl.IsComputedDeclaration ? "(computed)" : "(reactive)";
                var typeName = typeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? typeStr;
                var displayString = $"{prefix} {typeName} {refDecl.Name.Name}";

                return new PropertyResolutionResult(
                    PropertyAST: refDecl.Name,
                    RawPropertyName: refDecl.Name.Name,
                    RoslynSymbol: null,
                    GeneratedSymbol: null,
                    DisplayString: displayString,
                    Kind: PropertyResolutionKind.RefDeclaration,
                    ResolvedTypeSymbol: typeSymbol);
            }
        }

        return null;
    }

    private static INamedTypeSymbol? ResolveRefTypeSymbol(string typeStr, QuickMarkupSFC sfc, Compilation compilation)
    {
        var typeSymbol = compilation.GetTypeByMetadataName(typeStr);
        if (typeSymbol is null)
        {
            var ns = sfc.Namespace?.Name ?? "";
            var fullName = string.IsNullOrEmpty(ns) ? typeStr : $"{ns}.{typeStr}";
            typeSymbol = compilation.GetTypeByMetadataName(fullName);
        }
        return typeSymbol;
    }

    private PropertyResolutionResult? ResolvePropertyAtPosition(
        QuickMarkupParsedTag tag,
        QuickMarkupSFC sfc,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable generatedMembers,
        QuickMarkupParsedProperty property)
    {
        var tagType = TryResolveTagType(compilation, sfc, tag);
        if (tagType is null)
            return null;

        var resolver = new CodeTypeResolver(compilation, sfc.Usings, sfc.Namespace?.Name ?? "", generatedMembers);
        var resolvedProperty = resolver.FindProperty(tagType, property.Key);

        if (resolvedProperty is { } prop)
        {
            return BuildPropertyResolutionResult(property, prop, property.Key, PropertyResolutionKind.TagAttribute, tagType);
        }

        var componentKind = resolver.GetComponentKind(tagType, out var componentOutputType);
        if (componentKind == QMComponentKind.Single && componentOutputType is not null)
        {
            var outputProp = resolver.FindProperty(componentOutputType, property.Key);
            if (outputProp is { } outProp)
            {
                return BuildPropertyResolutionResult(property, outProp, $"{CodeTypeResolver.ComponentOutputPropertyName}.{property.Key}", PropertyResolutionKind.TagAttribute, componentOutputType as INamedTypeSymbol ?? tagType);
            }
        }

        return null;
    }

    private PropertyResolutionResult? ResolvePropertyTagAtPosition(
        QuickMarkupParsedTag parentTag,
        QuickMarkupSFC sfc,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable generatedMembers,
        string propertyName)
    {
        var tagType = TryResolveTagType(compilation, sfc, parentTag);
        if (tagType is null)
            return null;

        var resolver = new CodeTypeResolver(compilation, sfc.Usings, sfc.Namespace?.Name ?? "", generatedMembers);
        var resolvedProperty = resolver.FindProperty(tagType, propertyName);

        if (resolvedProperty is { } prop)
        {
            return BuildPropertyResolutionResultFromTag(parentTag, prop, propertyName, PropertyResolutionKind.PropertyTag, tagType);
        }

        return null;
    }

    private PropertyResolutionResult? ResolveAttachedPropertyTagAtPosition(
        QuickMarkupParsedTag parentTag,
        QuickMarkupSFC sfc,
        Compilation compilation,
        QuickMarkupGeneratedMemberTable generatedMembers,
        QuickMarkupAttachedPropertyTagStart attachedTagStart)
    {
        var attachedType = compilation.GetTypeByMetadataName(attachedTagStart.TypeName);
        if (attachedType is null)
            return null;

        var resolver = new CodeTypeResolver(compilation, sfc.Usings, sfc.Namespace?.Name ?? "", generatedMembers);
        if (resolver.TryGetAttachedPropertyInfo(attachedType, attachedTagStart.PropertyName, out var valueType, out _, out _))
        {
            var fullPropertyName = $"{attachedTagStart.TypeName}.{attachedTagStart.PropertyName}";
            return new PropertyResolutionResult(
                PropertyAST: attachedTagStart,
                RawPropertyName: fullPropertyName,
                RoslynSymbol: null,
                GeneratedSymbol: null,
                DisplayString: $"{valueType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "unknown"} {attachedTagStart.PropertyName}",
                Kind: PropertyResolutionKind.AttachedPropertyTag);
        }

        return null;
    }

    private PropertyResolutionResult BuildPropertyResolutionResult(
        QuickMarkupParsedProperty property,
        ResolvedProperty resolvedProperty,
        string propertyName,
        PropertyResolutionKind kind,
        INamedTypeSymbol? ownerTypeSymbol)
    {
        string displayString;
        QuickMarkupGeneratedPropertySymbol? generatedSymbol = null;
        IPropertySymbol? roslynSymbol = null;

        roslynSymbol = resolvedProperty.RoslynSymbol;

        string typeName;
        if (resolvedProperty.Type is not null)
        {
            typeName = resolvedProperty.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (resolvedProperty.Type.NullableAnnotation is NullableAnnotation.Annotated && !typeName.EndsWith('?'))
            {
                typeName += "?";
            }
        } else
        {
            typeName = "unknown";
        }

        if (resolvedProperty.GeneratedSymbol is { } genSym)
        {
            generatedSymbol = genSym;
            var prefix = genSym.Kind switch
            {
                QuickMarkupGeneratedPropertyKind.ComputedValue => "(computed)",
                QuickMarkupGeneratedPropertyKind.RefValue => "(reactive)",
                QuickMarkupGeneratedPropertyKind.ComponentOutput => "",
                _ => ""
            };
            displayString = string.IsNullOrEmpty(prefix)
                ? $"{typeName} {propertyName}"
                : $"{prefix} {typeName} {propertyName}";
        }
        else
        {
            displayString = roslynSymbol is not null
                ? roslynSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : $"{typeName} {propertyName}";
        }

        return new PropertyResolutionResult(
            PropertyAST: property,
            RawPropertyName: propertyName,
            RoslynSymbol: roslynSymbol,
            GeneratedSymbol: generatedSymbol,
            DisplayString: displayString,
            Kind: kind,
            OwnerTypeSymbol: ownerTypeSymbol);
    }

    private PropertyResolutionResult BuildPropertyResolutionResultFromTag(
        QuickMarkupParsedTag tag,
        ResolvedProperty resolvedProperty,
        string propertyName,
        PropertyResolutionKind kind,
        INamedTypeSymbol? ownerTypeSymbol)
    {
        string displayString;
        QuickMarkupGeneratedPropertySymbol? generatedSymbol = null;
        IPropertySymbol? roslynSymbol = null;

        if (resolvedProperty.GeneratedSymbol is { } genSym)
        {
            generatedSymbol = genSym;
            var typeName = resolvedProperty.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "unknown";
            var prefix = genSym.Kind switch
            {
                QuickMarkupGeneratedPropertyKind.ComputedValue => "(computed)",
                QuickMarkupGeneratedPropertyKind.RefValue => "(reactive)",
                QuickMarkupGeneratedPropertyKind.ComponentOutput => "",
                _ => ""
            };
            displayString = string.IsNullOrEmpty(prefix)
                ? $"{typeName} {propertyName}"
                : $"{prefix} {typeName} {propertyName}";
        }
        else if (resolvedProperty.RoslynSymbol is { } roslynProp)
        {
            roslynSymbol = roslynProp;
            displayString = roslynProp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        else
        {
            var typeName = resolvedProperty.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "unknown";
            displayString = $"{typeName} {propertyName}";
        }

        return new PropertyResolutionResult(
            PropertyAST: tag.TagStart.TagIdentifierAST,
            RawPropertyName: propertyName,
            RoslynSymbol: roslynSymbol,
            GeneratedSymbol: generatedSymbol,
            DisplayString: displayString,
            Kind: kind,
            OwnerTypeSymbol: ownerTypeSymbol);
    }

    private INamedTypeSymbol? TryResolveTagType(Compilation compilation, QuickMarkupSFC sfc, QuickMarkupParsedTag tag)
    {
        if (tag.TagStart is not QuickMarkupConstructor constructor)
            return null;

        var tagName = constructor.TagIdentifierAST.ToString();
        if (string.IsNullOrEmpty(tagName))
            return null;

        var ns = sfc.Namespace?.Name ?? "";
        var fullName = string.IsNullOrEmpty(ns) ? tagName : $"{ns}.{tagName}";
        var typeSymbol = compilation.GetTypeByMetadataName(fullName);
        if (typeSymbol is not null)
            return typeSymbol;

        foreach (var entry in _workspace.GetQmuiEntriesByShortName(tagName))
        {
            typeSymbol = compilation.GetTypeByMetadataName(entry.FullTypeName);
            if (typeSymbol is not null)
                return typeSymbol;
        }

        return null;
    }

    private INamedTypeSymbol? ResolveComponentClass(Compilation compilation, QuickMarkupSFC sfc)
    {
        if (sfc.ClassDeclaration is null)
            return null;

        var ns = sfc.Namespace?.Name ?? "";
        var typeName = sfc.ClassDeclaration.Name;
        var fullName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";

        return compilation.GetTypeByMetadataName(fullName);
    }
}
