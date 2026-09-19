using System.Runtime.CompilerServices;

namespace QuickMarkup.Infra.Markup;

public static partial class Booleanish
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(bool value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(bool? value) => value ?? false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(string? value) => !string.IsNullOrEmpty(value);
}