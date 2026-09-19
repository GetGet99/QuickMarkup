using System.Numerics;
using System.Runtime.CompilerServices;

namespace QuickMarkup.Infra.Markup;

partial class Booleanish
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(sbyte? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(short? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(int? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(long? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(BigInteger? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(nint? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(char? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(byte? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(ushort? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(uint? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(ulong? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(nuint? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(float? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(double? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(decimal? value) => value is {} num && Condition(num);

#if NET5_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(Int128? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(UInt128? value) => value is {} num && Condition(num);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(Half? value) => value is {} num && Condition(num);
#endif
}