using System.Numerics;
using System.Runtime.CompilerServices;

namespace QuickMarkup.Infra.Markup;

public static partial class Booleanish
{
}

// Can't put both <T> in the same class, so a different one
public static class BooleanishExtension
{
    const string StructBehaviorRedundant = 
        "The logic here is redundant as given struct type will always be true in QuickMarkup.";
    extension(Booleanish)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete(StructBehaviorRedundant)]
        public static bool Condition<T>(T _) where T : struct => true;

        public static bool Condition(object? value)
            => value switch
            {
                null => false,
                bool x => Booleanish.Condition(x),
                sbyte x => Booleanish.Condition(x),
                short x => Booleanish.Condition(x),
                int x => Booleanish.Condition(x),
                long x => Booleanish.Condition(x),
                Int128 x => Booleanish.Condition(x),
                BigInteger x => Booleanish.Condition(x),
                nint x => Booleanish.Condition(x),
                char x => Booleanish.Condition(x),
                byte x => Booleanish.Condition(x),
                ushort x => Booleanish.Condition(x),
                uint x => Booleanish.Condition(x),
                ulong x => Booleanish.Condition(x),
                UInt128 x => Booleanish.Condition(x),
                nuint x => Booleanish.Condition(x),
                Half x => Booleanish.Condition(x),
                float x => Booleanish.Condition(x),
                double x => Booleanish.Condition(x),
                decimal x => Booleanish.Condition(x),
                string x => Booleanish.Condition(x),
                _ => true
            };
    }
}

public static class BooleanishExtensionFastPathClass
{
    extension(Booleanish)
    {
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Condition<T>(T? value) where T : class => value is not null;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Condition<T>(T? value) where T : struct => value.HasValue;
    }
}