using Microsoft.CodeAnalysis;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.CodeAnalysis.Helpers;
using QuickMarkup.Language.Symbols;

namespace QuickMarkup.CodeAnalysis;

public sealed class QuickMarkupFileAnalysis
{
    public QuickMarkupSFC SyntaxTree { get; }
    public QuickMarkupTargetContext TargetContext { get; }
    public IReadOnlyList<QMRefDeclarationSymbol<ITypeSymbol?>> RefDeclarations { get; }
    public QMNodeSymbol<ITypeSymbol?>? BoundTemplate { get; }
    public IReadOnlyList<QMDiagnostic> Diagnostics { get; }
    public QuickMarkupGeneratedTypeMembers? GeneratedMembers { get; }
    public bool IsComponent { get; }

    public QuickMarkupFileAnalysis(
        QuickMarkupSFC syntaxTree,
        QuickMarkupTargetContext targetContext,
        IReadOnlyList<QMRefDeclarationSymbol<ITypeSymbol?>> refDeclarations,
        QMNodeSymbol<ITypeSymbol?>? boundTemplate,
        IReadOnlyList<QMDiagnostic> diagnostics,
        QuickMarkupGeneratedTypeMembers? generatedMembers,
        bool isComponent)
    {
        SyntaxTree = syntaxTree;
        TargetContext = targetContext;
        RefDeclarations = refDeclarations;
        BoundTemplate = boundTemplate;
        Diagnostics = diagnostics;
        GeneratedMembers = generatedMembers;
        IsComponent = isComponent;
    }
}
