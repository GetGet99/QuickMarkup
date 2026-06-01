namespace QuickMarkup.Language.Symbols;

public interface IQMMemberSymbol;
public interface IQMNodeChildSymbol;
public interface IQMValueSymbol : IQMNodeChildSymbol;

public enum ChildrenModes
{
    None,
    Assignment,
    Add
}

public enum BindingModes
{
    OneTime, // =
    SourceToTarget, // = when used with foreign value
    TargetToSource, // =>
    TwoWay, // in the future, perhaps <=>
}

public enum ChildCollectionLowering
{
    DirectAdd,
    Blocks
}

public enum QMForKind
{
    StaticRange,
    ReactiveCollection
}

public enum QMComponentKind
{
    None,
    Single,
    Fragment
}

public record class QMConstructor(
    string Method,
    IReadOnlyList<IQMValueSymbol> Parameters,
    bool ShouldUseNewKeyword
);

public record class QMNodeSymbol<T>(
    T Type,
    QMConstructor Constructor,
    IReadOnlyList<IQMMemberSymbol> Members,
    string? Name,
    QMComponentKind ComponentKind = QMComponentKind.None,
    T? ComponentOutputType = default,
    string ComponentOutputPropertyName = "MarkupNode",
    bool IsRef = false
) : IQMNodeChildSymbol, IQMValueSymbol;
public record class QMForNodeSymbol<T>(
    QMForKind Kind,
    T? VarType,
    string VarName,
    IQMValueSymbol Iterable,
    IReadOnlyList<IQMMemberSymbol> Body,
    string? IndexVarName = null,
    IQMValueSymbol? Key = null
) : IQMNodeChildSymbol
{
    public QMForNodeSymbol(T? VarType, string VarName, IQMValueSymbol Iterable, IReadOnlyList<IQMMemberSymbol> Body)
        : this(QMForKind.ReactiveCollection, VarType, VarName, Iterable, Body, null, null)
    {
    }
}
public record class QMIfNodeSymbol<T>(
    IQMValueSymbol Condition,
    IReadOnlyList<IQMMemberSymbol> BodyWhenTrue,
    IReadOnlyList<IQMMemberSymbol>? BodyWhenFalse
) : IQMNodeChildSymbol;
public record class QMConditionalValueSymbol<T>(
    IQMValueSymbol Condition,
    IQMNodeChildSymbol ValueWhenTrue,
    IQMNodeChildSymbol ValueWhenFalse
) : IQMNodeChildSymbol;
public record class QMFragmentNodeSymbol(
    IReadOnlyList<IQMMemberSymbol> Body
) : IQMNodeChildSymbol;

public record class QMComponentRootMember<T>(
    QMComponentKind Kind,
    T? OutputType,
    IQMNodeChildSymbol Output,
    string OutputPropertyName = "MarkupNode"
) : IQMMemberSymbol;
public record class QMAssignChildMember<T>(string ChildPropertyPath, IQMNodeChildSymbol Child, T? ChildType = default) : IQMMemberSymbol;
public record class QMAddChildMember<T>(
    string ChildPropertyPath,
    IQMNodeChildSymbol Child,
    ChildCollectionLowering CollectionLowering = ChildCollectionLowering.DirectAdd,
    T? ChildElementType = default
) : IQMMemberSymbol;
public record class QMAddPropertyMember<T>(T? PropertyType, string PropertyName, IQMValueSymbol Value, BindingModes BindingMode, bool IsDependencyProperty = false, string DependencyPropertyName = "", string TargetName = "") : IQMMemberSymbol;
public record class QMAttachedPropertyMember<T>(T? PropertyType, string AttachedTypeFullName, string PropertyName, IQMValueSymbol Value, BindingModes BindingMode, bool IsDependencyProperty = false, string DependencyPropertyName = "") : IQMMemberSymbol;
public record class QMAddEventMember<T>(T? MemberType, string EventName, IQMValueSymbol Value, bool IsShorthand) : IQMMemberSymbol;
public record class QMExtensionMember(string Method, string TargetPath = "") : IQMMemberSymbol;
public record class QMCallbackMember<T>(T? Type, string RawDelegateCode) : IQMMemberSymbol;

public record class QMValueSymbol<T>(T? Type, string ValueInFinalCode, IReadOnlyCollection<string>? CapturedLocalNames = null) : IQMValueSymbol;
public record class QMRangeSymbol(int Start, int End) : IQMValueSymbol;
public record class QMNestedValuesSymbol<T>(T? Type, IReadOnlyList<IQMMemberSymbol> Values) : IQMValueSymbol;

/// <summary>Named argument inside a compile-time ref attribute's argument list (phase 1: binding only).</summary>
public record class QMAttributeNamedArgumentSymbol(string Name, IQMValueSymbol Value);

/// <summary>One compile-time attribute application on a ref/computed declaration.</summary>
public record class QMCompileTimeAttributeSymbol(
    string? TargetSpecifier,
    string AttributeName,
    IReadOnlyList<IQMValueSymbol> PositionalArguments,
    IReadOnlyList<QMAttributeNamedArgumentSymbol> NamedArguments
);

/// <summary>Bound ref/computed prop line, including compile-time attributes (phase 1: not emitted to C# refs). Use <see cref="RefType"/> nullable annotation when <c>T</c> is <c>ITypeSymbol</c>.</summary>
public record class QMRefDeclarationSymbol<T>(
    T? RefType,
    string Name,
    IQMValueSymbol? DefaultValue,
    bool IsPrivate,
    bool IsStatic,
    bool IsComputedDeclaration,
    IReadOnlyList<QMCompileTimeAttributeSymbol> CompileTimeAttributes);
