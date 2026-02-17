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

public record class QMConstructor(
    string Method,
    IReadOnlyList<IQMValueSymbol> Parameters,
    bool ShouldUseNewKeyword
);

public record class QMNodeSymbol<T>(
    T Type,
    QMConstructor Constructor,
    IReadOnlyList<IQMMemberSymbol> Members,
    string? Name
) : IQMNodeChildSymbol, IQMValueSymbol;
public record class QMForNodeSymbol<T>(T? VarType, string VarName, IQMValueSymbol Iterable, IReadOnlyList<IQMMemberSymbol> Body) : IQMNodeChildSymbol;

public record class QMAssignChildMember(string ChildPropertyPath, IQMNodeChildSymbol Child) : IQMMemberSymbol;
public record class QMAddChildMember(string ChildPropertyPath, IQMNodeChildSymbol Child) : IQMMemberSymbol;
public record class QMAddPropertyMember<T>(T? PropertyType, string PropertyName, IQMValueSymbol Value, BindingModes BindingMode, bool IsDependencyProperty = false, string DependencyPropertyName = "", string TargetName = "") : IQMMemberSymbol;
public record class QMAddEventMember<T>(T? MemberType, string EventName, IQMValueSymbol Value, bool IsShorthand) : IQMMemberSymbol;
public record class QMExtensionMember(string Method) : IQMMemberSymbol;
public record class QMCallbackMember<T>(T? Type, string RawDelegateCode) : IQMMemberSymbol;

public record class QMValueSymbol<T>(T? Type, string ValueInFinalCode) : IQMValueSymbol;
public record class QMRangeSymbol(int Start, int End) : IQMValueSymbol;
public record class QMNestedValuesSymbol<T>(T? Type, IReadOnlyList<IQMMemberSymbol> Values) : IQMValueSymbol;