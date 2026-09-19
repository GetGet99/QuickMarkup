using System.Numerics;
using QuickMarkup.Infra.Markup;

namespace QuickMarkup.Infra.Test
{
    [TestClass]
    public sealed class BooleanishTests
    {
        [TestInitialize]
        public void Setup()
        {
            ReactiveScheduler.ResetForCurrentThread();
            ReactiveScheduler.Instance.Value!.AutoTick = false;
            ReactiveScheduler.Instance.Value!.ContinueOnException = false;
        }

        // bool

        [TestMethod]
        public void Condition_Bool_True_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(true));
        }

        [TestMethod]
        public void Condition_Bool_False_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(false));
        }

        [TestMethod]
        public void Condition_Bool_Nullable_True_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((bool?)true));
        }

        [TestMethod]
        public void Condition_Bool_Nullable_False_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((bool?)false));
        }

        [TestMethod]
        public void Condition_Bool_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((bool?)null));
        }

        // string

        [TestMethod]
        public void Condition_String_NonEmpty_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition("hello"));
        }

        [TestMethod]
        public void Condition_String_Empty_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(""));
        }

        [TestMethod]
        public void Condition_String_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((string?)null));
        }

        [TestMethod]
        public void Condition_String_Whitespace_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(" "));
        }

        // Non-nullable numeric types

        [TestMethod]
        public void Condition_SByte_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((sbyte)1));
        }

        [TestMethod]
        public void Condition_SByte_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((sbyte)0));
        }

        [TestMethod]
        public void Condition_SByte_Negative_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((sbyte)-1));
        }

        [TestMethod]
        public void Condition_Short_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((short)1));
        }

        [TestMethod]
        public void Condition_Short_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((short)0));
        }

        [TestMethod]
        public void Condition_Int_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(42));
        }

        [TestMethod]
        public void Condition_Int_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(0));
        }

        [TestMethod]
        public void Condition_Int_Negative_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(-1));
        }

        [TestMethod]
        public void Condition_Long_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(1L));
        }

        [TestMethod]
        public void Condition_Long_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(0L));
        }

        [TestMethod]
        public void Condition_Int128_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((Int128)1));
        }

        [TestMethod]
        public void Condition_Int128_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((Int128)0));
        }

        [TestMethod]
        public void Condition_BigInteger_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((BigInteger)1));
        }

        [TestMethod]
        public void Condition_BigInteger_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(BigInteger.Zero));
        }

        [TestMethod]
        public void Condition_NInt_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((nint)1));
        }

        [TestMethod]
        public void Condition_NInt_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((nint)0));
        }

        [TestMethod]
        public void Condition_Char_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition('a'));
        }

        [TestMethod]
        public void Condition_Char_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition('\0'));
        }

        [TestMethod]
        public void Condition_Byte_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((byte)1));
        }

        [TestMethod]
        public void Condition_Byte_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((byte)0));
        }

        [TestMethod]
        public void Condition_UShort_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((ushort)1));
        }

        [TestMethod]
        public void Condition_UShort_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((ushort)0));
        }

        [TestMethod]
        public void Condition_UInt_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(1u));
        }

        [TestMethod]
        public void Condition_UInt_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(0u));
        }

        [TestMethod]
        public void Condition_ULong_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(1ul));
        }

        [TestMethod]
        public void Condition_ULong_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(0ul));
        }

        [TestMethod]
        public void Condition_UInt128_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((UInt128)1));
        }

        [TestMethod]
        public void Condition_UInt128_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((UInt128)0));
        }

        [TestMethod]
        public void Condition_NUInt_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((nuint)1));
        }

        [TestMethod]
        public void Condition_NUInt_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((nuint)0));
        }

        [TestMethod]
        public void Condition_Half_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((Half)1));
        }

        [TestMethod]
        public void Condition_Half_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((Half)0));
        }

        [TestMethod]
        public void Condition_Half_NaN_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(Half.NaN));
        }

        [TestMethod]
        public void Condition_Float_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(1f));
        }

        [TestMethod]
        public void Condition_Float_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(0f));
        }

        [TestMethod]
        public void Condition_Float_NaN_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(float.NaN));
        }

        [TestMethod]
        public void Condition_Float_Negative_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(-1f));
        }

        [TestMethod]
        public void Condition_Double_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(1.0));
        }

        [TestMethod]
        public void Condition_Double_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(0.0));
        }

        [TestMethod]
        public void Condition_Double_NaN_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(double.NaN));
        }

        [TestMethod]
        public void Condition_Double_Negative_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(-1.0));
        }

        [TestMethod]
        public void Condition_Decimal_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(1m));
        }

        [TestMethod]
        public void Condition_Decimal_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition(0m));
        }

        [TestMethod]
        public void Condition_Decimal_Negative_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition(-1m));
        }

        // Nullable numeric types

        [TestMethod]
        public void Condition_SByte_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((sbyte?)1));
        }

        [TestMethod]
        public void Condition_SByte_Nullable_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((sbyte?)0));
        }

        [TestMethod]
        public void Condition_SByte_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((sbyte?)null));
        }

        [TestMethod]
        public void Condition_Short_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((short?)1));
        }

        [TestMethod]
        public void Condition_Short_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((short?)null));
        }

        [TestMethod]
        public void Condition_Int_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((int?)42));
        }

        [TestMethod]
        public void Condition_Int_Nullable_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((int?)0));
        }

        [TestMethod]
        public void Condition_Int_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((int?)null));
        }

        [TestMethod]
        public void Condition_Long_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((long?)1L));
        }

        [TestMethod]
        public void Condition_Long_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((long?)null));
        }

        [TestMethod]
        public void Condition_Int128_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((Int128?)1));
        }

        [TestMethod]
        public void Condition_Int128_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((Int128?)null));
        }

        [TestMethod]
        public void Condition_BigInteger_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((BigInteger?)1));
        }

        [TestMethod]
        public void Condition_BigInteger_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((BigInteger?)null));
        }

        [TestMethod]
        public void Condition_NInt_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((nint?)1));
        }

        [TestMethod]
        public void Condition_NInt_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((nint?)null));
        }

        [TestMethod]
        public void Condition_Char_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((char?)'a'));
        }

        [TestMethod]
        public void Condition_Char_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((char?)null));
        }

        [TestMethod]
        public void Condition_Byte_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((byte?)1));
        }

        [TestMethod]
        public void Condition_Byte_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((byte?)null));
        }

        [TestMethod]
        public void Condition_UShort_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((ushort?)1));
        }

        [TestMethod]
        public void Condition_UShort_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((ushort?)null));
        }

        [TestMethod]
        public void Condition_UInt_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((uint?)1u));
        }

        [TestMethod]
        public void Condition_UInt_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((uint?)null));
        }

        [TestMethod]
        public void Condition_ULong_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((ulong?)1ul));
        }

        [TestMethod]
        public void Condition_ULong_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((ulong?)null));
        }

        [TestMethod]
        public void Condition_UInt128_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((UInt128?)1));
        }

        [TestMethod]
        public void Condition_UInt128_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((UInt128?)null));
        }

        [TestMethod]
        public void Condition_NUInt_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((nuint?)1));
        }

        [TestMethod]
        public void Condition_NUInt_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((nuint?)null));
        }

        [TestMethod]
        public void Condition_Half_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((Half?)1));
        }

        [TestMethod]
        public void Condition_Half_Nullable_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((Half?)0));
        }

        [TestMethod]
        public void Condition_Half_Nullable_NaN_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((Half?)Half.NaN));
        }

        [TestMethod]
        public void Condition_Half_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((Half?)null));
        }

        [TestMethod]
        public void Condition_Float_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((float?)1f));
        }

        [TestMethod]
        public void Condition_Float_Nullable_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((float?)0f));
        }

        [TestMethod]
        public void Condition_Float_Nullable_NaN_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((float?)float.NaN));
        }

        [TestMethod]
        public void Condition_Float_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((float?)null));
        }

        [TestMethod]
        public void Condition_Double_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((double?)1.0));
        }

        [TestMethod]
        public void Condition_Double_Nullable_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((double?)0.0));
        }

        [TestMethod]
        public void Condition_Double_Nullable_NaN_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((double?)double.NaN));
        }

        [TestMethod]
        public void Condition_Double_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((double?)null));
        }

        [TestMethod]
        public void Condition_Decimal_Nullable_NonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((decimal?)1m));
        }

        [TestMethod]
        public void Condition_Decimal_Nullable_Zero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((decimal?)0m));
        }

        [TestMethod]
        public void Condition_Decimal_Nullable_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((decimal?)null));
        }

        // Generic class catch-all: Condition<T>(T?) where T : class

        [TestMethod]
        public void Condition_GenericClass_StringNonNull_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition<string>("hello"));
        }

        [TestMethod]
        public void Condition_GenericClass_StringNull_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition<string>(null));
        }

        [TestMethod]
        public void Condition_GenericClass_ListNonNull_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition<List<int>>(new()));
        }

        [TestMethod]
        public void Condition_GenericClass_ListNull_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition<List<int>>(null));
        }

        // Generic struct catch-all: Condition<T>(T?) where T : struct

        [TestMethod]
        public void Condition_GenericStruct_IntHasValue_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition<int>((int?)42));
        }

        [TestMethod]
        public void Condition_GenericStruct_IntNull_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition<int>((int?)null));
        }

        [TestMethod]
        public void Condition_GenericStruct_BoolHasValue_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition<bool>((bool?)true));
        }

        [TestMethod]
        public void Condition_GenericStruct_BoolNull_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition<bool>((bool?)null));
        }

        [TestMethod]
        public void Condition_GenericStruct_DoubleHasValue_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition<double>((double?)1.0));
        }

        [TestMethod]
        public void Condition_GenericStruct_DoubleNull_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition<double>((double?)null));
        }

        [TestMethod]
        public void Condition_GenericStruct_ByteHasValue_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition<byte>((byte?)1));
        }

        [TestMethod]
        public void Condition_GenericStruct_ByteNull_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition<byte>((byte?)null));
        }

        // object? switch dispatch

        [TestMethod]
        public void Condition_Object_Null_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)null));
        }

        [TestMethod]
        public void Condition_Object_BoolTrue_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)true));
        }

        [TestMethod]
        public void Condition_Object_BoolFalse_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)false));
        }

        [TestMethod]
        public void Condition_Object_IntNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)42));
        }

        [TestMethod]
        public void Condition_Object_IntZero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)0));
        }

        [TestMethod]
        public void Condition_Object_StringNonEmpty_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)"hello"));
        }

        [TestMethod]
        public void Condition_Object_StringEmpty_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)""));
        }

        [TestMethod]
        public void Condition_Object_DoubleNaN_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)double.NaN));
        }

        [TestMethod]
        public void Condition_Object_DoubleZero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)0.0));
        }

        [TestMethod]
        public void Condition_Object_DoubleNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)1.0));
        }

        [TestMethod]
        public void Condition_Object_DecimalZero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)0m));
        }

        [TestMethod]
        public void Condition_Object_DecimalNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)1m));
        }

        [TestMethod]
        public void Condition_Object_CharNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)'a'));
        }

        [TestMethod]
        public void Condition_Object_CharZero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)'\0'));
        }

        [TestMethod]
        public void Condition_Object_UnrecognizedType_ReturnsTrue()
        {
            // The default case in the switch is `_ => true`
            var obj = new DateTime(2025, 1, 1);
            Assert.IsTrue(Booleanish.Condition((object?)obj));
        }

        [TestMethod]
        public void Condition_Object_ByteNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)(byte)1));
        }

        [TestMethod]
        public void Condition_Object_ByteZero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)byte.MinValue));
        }

        [TestMethod]
        public void Condition_Object_ShortNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)(short)1));
        }

        [TestMethod]
        public void Condition_Object_LongNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)1L));
        }

        [TestMethod]
        public void Condition_Object_FloatNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)1f));
        }

        [TestMethod]
        public void Condition_Object_FloatNaN_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)float.NaN));
        }

        [TestMethod]
        public void Condition_Object_HalfNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)(Half)1));
        }

        [TestMethod]
        public void Condition_Object_ULongNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)1ul));
        }

        [TestMethod]
        public void Condition_Object_UIntNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)1u));
        }

        [TestMethod]
        public void Condition_Object_BigIntegerNonZero_ReturnsTrue()
        {
            Assert.IsTrue(Booleanish.Condition((object?)BigInteger.One));
        }

        [TestMethod]
        public void Condition_Object_BigIntegerZero_ReturnsFalse()
        {
            Assert.IsFalse(Booleanish.Condition((object?)BigInteger.Zero));
        }
    }
}
