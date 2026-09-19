using System.Numerics;
using System.Runtime.CompilerServices;

namespace QuickMarkup.Infra.Markup;

partial class Booleanish
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(sbyte value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(short value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(int value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(long value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(BigInteger value) => value != BigInteger.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(nint value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(char value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(byte value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(ushort value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(uint value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(ulong value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(nuint value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(float value) => value != 0 && !float.IsNaN(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(double value) => value != 0 && !double.IsNaN(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(decimal value) => value != 0;

#if NET5_0_OR_GREATER

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(Int128 value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(UInt128 value) => value != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Condition(Half value) => value != default /* 0 */ && !Half.IsNaN(value);
#endif
}