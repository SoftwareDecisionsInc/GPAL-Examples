// =============================================================================
// GPAL - Generally Positive Automation Library
// Copyright © 2026 Software Decisions, Inc. All rights reserved.
//
// This file is part of GPAL.
// Licensed under the Business Source License 1.1
//
// Primary development, architecture, and vision by Michael B. Vederman,
// CEO of Software Decisions, Inc., Texas.
//
// Internal development maintained privately.
// Public releases appear on GitHub: https://github.com/SoftwareDecisionsInc/GPAL.
//
// See LICENSE for full terms, including Additional Use Grant.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Net;

namespace ConverterTestor
{
    // 1. Deeply nested generic tree node (class) - great for recursion testing
    public class TreeNode<T>
    {
        public T Value { get; set; }
        public TreeNode<T> Left { get; set; }
        public TreeNode<T> Right { get; set; }
        public TreeNode<T> Parent { get; set; } // Potential back-reference
        public List<TreeNode<T>> Children { get; set; }
    }

    // 2. Self-referential struct with array of itself (careful: structs with self-reference via array)
    public struct RecursiveStruct
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public RecursiveStruct[] Children { get; set; } // Array of same struct
        public List<RecursiveStruct> Alternatives { get; set; }
    }

    // 3. Mixed hierarchy: class containing struct containing class (inception!)
    public struct InnerContainer
    {
        public string Label { get; set; }
        public DateTime Created { get; set; }
        public WeirdPayload Payload { get; set; } // Struct contains a class!
    }

    public class WeirdPayload
    {
        public Guid CorrelationId { get; set; }
        public List<InnerContainer> History { get; set; } // Class contains list of structs that contain this class type
        public Dictionary<string, object> DynamicData { get; set; } // object for maximum ambiguity
        public WeirdPayload SelfReference { get; set; } // Direct self-reference (cycle risk)
    }

    // 4. Highly generic constrained entity
    public class ConstrainedEntity<TKey, TValue>
        where TKey : notnull
        where TValue : class, new()
    {
        public TKey Key { get; set; }
        public TValue Value { get; set; }
        public List<ConstrainedEntity<TKey, TValue>> Related { get; set; }
        public Dictionary<TKey, List<TValue>> Lookup { get; set; }
        public TValue[] ValueArray { get; set; }
    }

    // 5. Esoteric tuple-heavy nested structure with arrays/lists of tuples
    public class TupleMadness
    {
        public (int Id, string Name, DateTime Timestamp) BasicTuple { get; set; }
        public List<(Guid Key, double Score, bool? Flag)> ScoredItems { get; set; }
        public (string Tag, List<(int X, int Y)> Points)[] TaggedPolygons { get; set; }
        public Dictionary<string, (Complex ComplexValue, BigInteger HugeInt)> StrangeMap { get; set; }
        public ValueTuple<int, RecursiveStruct>[] MixedTupleArray { get; set; }
    }

    // Plain enum and a [Flags] enum. gpal.yaml already proves a plain enum round-trips, so these exist for the
    // cases it cannot reach: a flags combination, a nullable enum, and enums sitting inside a collection.
    public enum Priority { None = 0, Low = 1, Normal = 2, High = 3 }

    [Flags]
    public enum Access { None = 0, Read = 1, Write = 2, Delete = 4, All = Read | Write | Delete }

    public class AllDataTypes
    {
        // COMPLEX
        public Complex[] ComplexArrayField { get; set; }
        public List<Complex> ComplexListField { get; set; }
        public List<Complex>[] ComplexListArrayField { get; set; }
        public Complex ComplexClassField { get; set; }

        public BigInteger BigIntegerField { get; set; }
        public BigInteger? NullableBigIntegerField { get; set; }

        public bool BoolField { get; set; }
        public byte ByteField { get; set; }
        public sbyte SByteField { get; set; }
        public short Int16Field { get; set; }
        public ushort UInt16Field { get; set; }
        public int Int32Field { get; set; }
        public uint UInt32Field { get; set; }
        public long Int64Field { get; set; }
        public ulong UInt64Field { get; set; }
        public float FloatField { get; set; }
        public double DoubleField { get; set; }
        public decimal DecimalField { get; set; }
        public char CharField { get; set; }
        public string StringField { get; set; }
        public DateTime DateTimeField { get; set; }
        public Guid GuidField { get; set; }
        public TimeSpan TimeSpanField { get; set; }
        public Uri UriField { get; set; }
        public IPAddress IpAddressField { get; set; }
        public Version VersionField { get; set; }
        public DateTimeOffset DateTimeOffsetField { get; set; }
        public Priority PriorityField { get; set; }
        public Access AccessField { get; set; }

        // Nullable Value Types
        public bool? NullableBooleanField { get; set; }
        public byte? NullableByteField { get; set; }
        public sbyte? NullableSByteField { get; set; }
        public char? NullableCharField { get; set; }
        public short? NullableInt16Field { get; set; }
        public ushort? NullableUInt16Field { get; set; }
        public int? NullableInt32Field { get; set; }
        public uint? NullableUInt32Field { get; set; }
        public long? NullableInt64Field { get; set; }
        public ulong? NullableUInt64Field { get; set; }
        public float? NullableSingleField { get; set; }
        public double? NullableDoubleField { get; set; }
        public decimal? NullableDecimalField { get; set; }
        public DateTime? NullableDateTimeField { get; set; }
        public TimeSpan? NullableTimeSpanField { get; set; }
        public Guid? NullableGuidField { get; set; }
        public DateTimeOffset? NullableDateTimeOffsetField { get; set; }
        public Priority? NullablePriorityField { get; set; }
        public Access? NullableAccessField { get; set; }

        public int[] IntArrayField { get; set; }
        public bool[] BoolArrayField { get; set; }
        public byte[] ByteArrayField { get; set; }
        public sbyte[] SByteArrayField { get; set; }
        public short[] Int16ArrayField { get; set; }
        public ushort[] UInt16ArrayField { get; set; }
        public int[] Int32ArrayField { get; set; }
        public uint[] UInt32ArrayField { get; set; }
        public long[] Int64ArrayField { get; set; }
        public ulong[] UInt64ArrayField { get; set; }
        public float[] FloatArrayField { get; set; }
        public double[] DoubleArrayField { get; set; }
        public decimal[] DecimalArrayField { get; set; }
        public char[] CharArrayField { get; set; }
        public string[] StringArrayField { get; set; }
        public DateTime[] DateTimeArrayField { get; set; }
        public Guid[] GuidArrayField { get; set; }

        public List<bool> BoolList { get; set; }
        public List<byte> ByteList { get; set; }
        public List<sbyte> SByteList { get; set; }
        public List<short> Int16List { get; set; }
        public List<ushort> UInt16List { get; set; }
        public List<int> Int32List { get; set; }
        public List<uint> UInt32List { get; set; }
        public List<long> Int64List { get; set; }
        public List<ulong> UInt64List { get; set; }
        public List<float> FloatList { get; set; }
        public List<double> DoubleList { get; set; }
        public List<decimal> DecimalList { get; set; }
        public List<char> CharList { get; set; }
        public List<string> StringList { get; set; }
        public List<DateTime> DateTimeList { get; set; }
        public List<Guid> GuidList { get; set; }

        public List<string>[] StringListArrayField { get; set; }
        public List<int>[] IntListArrayField { get; set; }

        // NEW: List<T>[] for all scalar types
        public List<bool>[] BoolListArrayField { get; set; }
        public List<byte>[] ByteListArrayField { get; set; }
        public List<sbyte>[] SByteListArrayField { get; set; }
        public List<short>[] Int16ListArrayField { get; set; }
        public List<ushort>[] UInt16ListArrayField { get; set; }
        public List<uint>[] UInt32ListArrayField { get; set; }
        public List<long>[] Int64ListArrayField { get; set; }
        public List<ulong>[] UInt64ListArrayField { get; set; }
        public List<float>[] FloatListArrayField { get; set; }
        public List<double>[] DoubleListArrayField { get; set; }
        public List<decimal>[] DecimalListArrayField { get; set; }
        public List<char>[] CharListArrayField { get; set; }
        public List<DateTime>[] DateTimeListArrayField { get; set; }
        public List<Guid>[] GuidListArrayField { get; set; }
        public List<TimeSpan>[] TimeSpanListArrayField { get; set; }
        public List<BigInteger>[] BigIntegerListArrayField { get; set; }

        // NEW: List<T?>[] for nullable scalar types
        public List<bool?>[] NullableBoolListArrayField { get; set; }
        public List<byte?>[] NullableByteListArrayField { get; set; }
        public List<sbyte?>[] NullableSByteListArrayField { get; set; }
        public List<short?>[] NullableInt16ListArrayField { get; set; }
        public List<ushort?>[] NullableUInt16ListArrayField { get; set; }
        public List<int?>[] NullableInt32ListArrayField { get; set; }
        public List<uint?>[] NullableUInt32ListArrayField { get; set; }
        public List<long?>[] NullableInt64ListArrayField { get; set; }
        public List<ulong?>[] NullableUInt64ListArrayField { get; set; }
        public List<float?>[] NullableFloatListArrayField { get; set; }
        public List<double?>[] NullableDoubleListArrayField { get; set; }
        public List<decimal?>[] NullableDecimalListArrayField { get; set; }
        public List<char?>[] NullableCharListArrayField { get; set; }
        public List<DateTime?>[] NullableDateTimeListArrayField { get; set; }
        public List<TimeSpan?>[] NullableTimeSpanListArrayField { get; set; }
        public List<Guid?>[] NullableGuidListArrayField { get; set; }
        public List<DateTimeOffset?>[] NullableDateTimeOffsetListArrayField { get; set; }
        public List<Priority?>[] NullablePriorityListArrayField { get; set; }
        public List<BigInteger?>[] NullableBigIntegerListArrayField { get; set; }

        // === NEW: Fields to hold the esoteric nested structures ===
        public TreeNode<string> StringTree { get; set; }
        public TreeNode<int> IntTree { get; set; }

        public RecursiveStruct RootRecursiveStruct { get; set; }
        public RecursiveStruct[] RecursiveStructArray { get; set; }

        public InnerContainer ContainerWithPayload { get; set; }
        public List<InnerContainer> ContainerList { get; set; }

        public WeirdPayload StrangePayload { get; set; }

        public ConstrainedEntity<Guid, List<string>> EntityWithGuidKey { get; set; }
        public ConstrainedEntity<string, TupleMadness> EntityWithComplexValue { get; set; }

        public TupleMadness TupleInsanity { get; set; }
        // Initialize method to set default values
        public void Initialize()
        {
            BoolField = true;
            ByteField = 42;
            SByteField = -42;
            Int16Field = 12345;
            UInt16Field = 56789;
            Int32Field = 123456789;
            UInt32Field = 987654321;
            Int64Field = 1234567890123456789;
            UInt64Field = 9876543210987654321;
            FloatField = 3.14f;
            DoubleField = 2.71828;
            DecimalField = 123.456m;
            CharField = 'A';
            StringField = "Hello, World!";
            DateTimeField = DateTime.Now;
            GuidField = Guid.NewGuid();
            TimeSpanField = TimeSpan.FromHours(1);
            UriField = new Uri("https://example.com");
            BigIntegerField = BigInteger.Pow(10, 100);
            IpAddressField = IPAddress.Parse("192.168.0.1");
            VersionField = new Version(1, 2, 3, 4);
            DateTimeOffsetField = new DateTimeOffset(2026, 8, 1, 12, 30, 45, TimeSpan.FromHours(-5));   // a non-UTC offset, so the offset itself has to survive
            PriorityField = Priority.High;
            AccessField = Access.Read | Access.Write;   // a combination with no name of its own

            // Nullable Value Types
            NullableBooleanField = true;
            NullableByteField = 42;
            NullableSByteField = -42;
            NullableCharField = 'A';
            NullableInt16Field = 12345;
            NullableUInt16Field = 56789;
            NullableInt32Field = 123456789;
            NullableUInt32Field = 987654321;
            NullableInt64Field = 1234567890123456789;
            NullableUInt64Field = 9876543210987654321;
            NullableSingleField = 3.14f;
            NullableDoubleField = 2.71828;
            NullableDecimalField = 123.456m;
            NullableDateTimeField = DateTime.Now;
            NullableTimeSpanField = TimeSpan.FromHours(1);
            NullableGuidField = Guid.NewGuid();
            NullableDateTimeOffsetField = DateTimeOffset.Now;
            NullablePriorityField = Priority.Low;
            NullableAccessField = Access.All;
            NullableBigIntegerField = BigInteger.Pow(2, 128);

            IntArrayField = new int[] { 1, 2, 3 };
            BoolArrayField = new bool[] { true, false, true };
            ByteArrayField = new byte[] { 0x01, 0x02, 0x03 };
            SByteArrayField = new sbyte[] { -1, 0, 1 };
            Int16ArrayField = new short[] { 10, 20, 30 };
            UInt16ArrayField = new ushort[] { 100, 200, 300 };
            Int32ArrayField = new int[] { 1000, 2000, 3000 };
            UInt32ArrayField = new uint[] { 10000, 20000, 30000 };
            Int64ArrayField = new long[] { 100000, 200000, 300000 };
            UInt64ArrayField = new ulong[] { 1000000, 2000000, 3000000 };
            FloatArrayField = new float[] { 1.1f, 2.2f, 3.3f };
            DoubleArrayField = new double[] { 1.1, 2.2, 3.3 };
            DecimalArrayField = new decimal[] { 1.1m, 2.2m, 3.3m };
            CharArrayField = new char[] { 'A', 'B', 'C' };
            StringArrayField = new string[] { "One One", "Two Two", "Three Three Three" };
            DateTimeArrayField = new DateTime[]
            {
                DateTime.Now, DateTime.Now.AddDays(1), DateTime.Now.AddDays(2)
            };
            GuidArrayField = new Guid[]
            {
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            };

            BoolList = new List<bool> { true, false, true };
            ByteList = new List<byte> { 42, 43, 44 };
            SByteList = new List<sbyte> { -42, -43, -44 };
            Int16List = new List<short> { 12345, 12346, 12347 };
            UInt16List = new List<ushort> { 56789, 56790, 56791 };
            Int32List = new List<int> { 123456789, 123456790, 123456791 };
            UInt32List = new List<uint> { 987654321, 987654322, 987654323 };
            Int64List = new List<long> { 1234567890123456789, 1234567890123456790, 1234567890123456791 };
            UInt64List = new List<ulong> { 9876543210987654321, 9876543210987654322, 9876543210987654323 };
            FloatList = new List<float> { 3.14f, 3.15f, 3.16f };
            DoubleList = new List<double> { 2.71828, 2.71829, 2.71830 };
            DecimalList = new List<decimal> { 123.456m, 123.457m, 123.458m };
            CharList = new List<char> { 'A', 'B', 'C' };
            StringList = new List<string> { "Hello Hello", "World World", "OpenAI OpenAI" };
            DateTimeList = new List<DateTime>
            {
                DateTime.Now, DateTime.Now.AddDays(1), DateTime.Now.AddDays(2)
            };
            GuidList = new List<Guid>
            {
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            };

            StringListArrayField = new List<string>[]
            {
                new List<string> { "A", "B" },
                new List<string> { "C C", "D D", "E E" },
                new List<string> { "F" }
            };
            IntListArrayField = new List<int>[]
            {
                new List<int> { 7, 8 },
                new List<int> { 9, 10, 11 },
                new List<int> { 12 }
            };

            // COMPLEX
            ComplexClassField = new Complex(1.0, 2.0);
            ComplexArrayField = new Complex[]
            {
                new Complex(3.0, 4.0),
                new Complex(5.0, 6.0),
                new Complex(7.0, 8.0)
            };
            ComplexListField = new List<Complex>
            {
                new Complex(9.0, 10.0),
                new Complex(11.0, 12.0),
                new Complex(13.0, 14.0)
            };
            ComplexListArrayField = new List<Complex>[]
            {
                new List<Complex>
                {
                    new Complex(15.0, -16.0),
                    new Complex(-17.0, 18.0)
                },
                new List<Complex>
                {
                    new Complex(-19.0, -20.0),
                    new Complex(21.0, 22.0),
                    new Complex(23.0, 24.0)
                },
                new List<Complex>
                {
                    new Complex(25.0, 26.0)
                }
            };

            // NEW: Initializations for List<T>[] fields
            BoolListArrayField = new List<bool>[]
            {
                new List<bool> { true, false },
                new List<bool> { true, true, false },
                new List<bool> { false }
            };

            ByteListArrayField = new List<byte>[]
            {
                new List<byte> { 1, 2 },
                new List<byte> { 3, 4, 5 },
                new List<byte> { 255 }
            };

            SByteListArrayField = new List<sbyte>[]
            {
                new List<sbyte> { -1, 0 },
                new List<sbyte> { 1, -2, 3 },
                new List<sbyte> { -128 }
            };

            Int16ListArrayField = new List<short>[]
            {
                new List<short> { 100, 200 },
                new List<short> { 300, 400, 500 },
                new List<short> { -1000 }
            };

            UInt16ListArrayField = new List<ushort>[]
            {
                new List<ushort> { 1000, 2000 },
                new List<ushort> { 3000, 4000, 5000 },
                new List<ushort> { 65535 }
            };

            UInt32ListArrayField = new List<uint>[]
            {
                new List<uint> { 100000, 200000 },
                new List<uint> { 300000, 400000, 500000 },
                new List<uint> { 4294967295 }
            };

            Int64ListArrayField = new List<long>[]
            {
                new List<long> { 1000000000, 2000000000 },
                new List<long> { 3000000000, 4000000000, 5000000000 },
                new List<long> { -1000000000000 }
            };

            UInt64ListArrayField = new List<ulong>[]
            {
                new List<ulong> { 1000000000, 2000000000 },
                new List<ulong> { 3000000000, 4000000000, 5000000000 },
                new List<ulong> { 18446744073709551615 }
            };

            FloatListArrayField = new List<float>[]
            {
                new List<float> { 1.1f, 2.2f },
                new List<float> { 3.3f, 4.4f, 5.5f },
                new List<float> { 3.14159f }
            };

            DoubleListArrayField = new List<double>[]
            {
                new List<double> { 1.1, 2.2 },
                new List<double> { 3.3, 4.4, 5.5 },
                new List<double> { Math.PI }
            };

            DecimalListArrayField = new List<decimal>[]
            {
                new List<decimal> { 100.50m, 200.75m },
                new List<decimal> { 300.25m, 400.50m, 500.75m },
                new List<decimal> { 1234567890.0987654321m }
            };

            CharListArrayField = new List<char>[]
            {
                new List<char> { 'X', 'Y' },
                new List<char> { 'A', 'B', 'C' },
                new List<char> { 'Z' }
            };

            DateTimeListArrayField = new List<DateTime>[]
            {
                new List<DateTime> { DateTime.Now, DateTime.Now.AddDays(1) },
                new List<DateTime> { DateTime.UtcNow, DateTime.UtcNow.AddHours(5) },
                new List<DateTime> { new DateTime(2000, 1, 1) }
            };

            GuidListArrayField = new List<Guid>[]
            {
                new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
                new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
                new List<Guid> { Guid.Empty }
            };

            TimeSpanListArrayField = new List<TimeSpan>[]
            {
                new List<TimeSpan> { TimeSpan.FromHours(1), TimeSpan.FromMinutes(30) },
                new List<TimeSpan> { TimeSpan.FromDays(1), TimeSpan.FromHours(12) },
                new List<TimeSpan> { TimeSpan.Zero }
            };

            BigIntegerListArrayField = new List<BigInteger>[]
            {
                new List<BigInteger> { BigInteger.Pow(2, 50), BigInteger.Pow(2, 60) },
                new List<BigInteger> { BigInteger.One, BigInteger.Zero, BigInteger.MinusOne },
                new List<BigInteger> { BigInteger.Pow(10, 50) }
            };

            // NEW: Initializations for List<T?>[] (nullable) fields
            NullableBoolListArrayField = new List<bool?>[]
            {
                new List<bool?> { true, null, false },
                new List<bool?> { null, true },
                new List<bool?> { false, false }
            };

            NullableByteListArrayField = new List<byte?>[]
            {
                new List<byte?> { 1, null, 3 },
                new List<byte?> { null, 255 },
                new List<byte?> { 42 }
            };

            NullableSByteListArrayField = new List<sbyte?>[]
            {
                new List<sbyte?> { -1, null, 1 },
                new List<sbyte?> { null, -128 },
                new List<sbyte?> { 42 }
            };

            NullableInt16ListArrayField = new List<short?>[]
            {
                new List<short?> { 100, null, -100 },
                new List<short?> { null, 32767 },
                new List<short?> { -32768 }
            };

            NullableUInt16ListArrayField = new List<ushort?>[]
            {
                new List<ushort?> { 1000, null, 2000 },
                new List<ushort?> { null, 65535 },
                new List<ushort?> { 42 }
            };

            NullableInt32ListArrayField = new List<int?>[]
            {
                new List<int?> { 1, null, 3 },
                new List<int?> { null, 5, 6 },
                new List<int?> { 100 }
            };

            NullableUInt32ListArrayField = new List<uint?>[]
            {
                new List<uint?> { 100000, null, 200000 },
                new List<uint?> { null, 4294967295 },
                new List<uint?> { 42 }
            };

            NullableInt64ListArrayField = new List<long?>[]
            {
                new List<long?> { 1000000000, null, -1000000000 },
                new List<long?> { null, long.MaxValue },
                new List<long?> { long.MinValue }
            };

            NullableUInt64ListArrayField = new List<ulong?>[]
            {
                new List<ulong?> { 1000000000, null, 2000000000 },
                new List<ulong?> { null, ulong.MaxValue },
                new List<ulong?> { 42 }
            };

            NullableFloatListArrayField = new List<float?>[]
            {
                new List<float?> { 1.1f, null, 2.2f },
                new List<float?> { null, float.NaN },
                new List<float?> { float.PositiveInfinity }
            };

            NullableDoubleListArrayField = new List<double?>[]
            {
                new List<double?> { 1.1, null, 3.3 },
                new List<double?> { null, Math.PI },
                new List<double?> { double.NaN }
            };

            NullableDecimalListArrayField = new List<decimal?>[]
            {
                new List<decimal?> { 100.50m, null, 200.75m },
                new List<decimal?> { null, 123.456m },
                new List<decimal?> { 0m }
            };

            NullableCharListArrayField = new List<char?>[]
            {
                new List<char?> { 'A', null, 'Z' },
                new List<char?> { null, ' ' },
                new List<char?> { '\0' }
            };

            NullableDateTimeListArrayField = new List<DateTime?>[]
            {
                new List<DateTime?> { DateTime.Now, null, DateTime.UtcNow },
                new List<DateTime?> { null, new DateTime(2020, 1, 1) },
                new List<DateTime?> { DateTime.MinValue }
            };

            NullableTimeSpanListArrayField = new List<TimeSpan?>[]
            {
                new List<TimeSpan?> { TimeSpan.FromHours(1), null, TimeSpan.FromMinutes(30) },
                new List<TimeSpan?> { null, TimeSpan.Zero },
                new List<TimeSpan?> { TimeSpan.MaxValue }
            };

            NullableGuidListArrayField = new List<Guid?>[]
            {
                new List<Guid?> { Guid.NewGuid(), null, Guid.NewGuid() },
                new List<Guid?> { null, Guid.Empty },
                new List<Guid?> { Guid.NewGuid() }
            };

            NullableDateTimeOffsetListArrayField = new List<DateTimeOffset?>[]
            {
                new List<DateTimeOffset?> { DateTimeOffset.Now, null, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(5.5)) },
                new List<DateTimeOffset?> { null, DateTimeOffset.MinValue },
                new List<DateTimeOffset?> { DateTimeOffset.MaxValue }
            };

            NullablePriorityListArrayField = new List<Priority?>[]
            {
                new List<Priority?> { Priority.High, null, Priority.None },  // None is zero, and must not be confused with null
                new List<Priority?> { null, Priority.Normal },
                new List<Priority?> { Priority.Low }
            };

            NullableBigIntegerListArrayField = new List<BigInteger?>[]
            {
                new List<BigInteger?> { BigInteger.Pow(2, 100), null, BigInteger.Zero },
                new List<BigInteger?> { null, BigInteger.One },
                new List<BigInteger?> { BigInteger.Pow(10, 100) }
            };

            // TreeNode<string>
            StringTree = new TreeNode<string>
            {
                Value = "Root",
                Children = new List<TreeNode<string>>
                {
                    new TreeNode<string>
                    {
                        Value = "Left",
                        Parent = null // will set after
                    },
                    new TreeNode<string>
                    {
                        Value = "Right",
                        Children = new List<TreeNode<string>>
                        {
                            new TreeNode<string> { Value = "Deep" }
                        }
                    }
                }
            };
            StringTree.Children[0].Parent = StringTree;
            StringTree.Children[1].Parent = StringTree;

            // TreeNode<int>
            IntTree = new TreeNode<int>
            {
                Value = 42,
                Left = new TreeNode<int> { Value = 10 },
                Right = new TreeNode<int> { Value = 99 }
            };

            // RecursiveStruct (struct nesting)
            var child1 = new RecursiveStruct { Id = 2, Name = "Child" };
            RootRecursiveStruct = new RecursiveStruct
            {
                Id = 1,
                Name = "Parent",
                Children = new[] { child1 },
                Alternatives = new List<RecursiveStruct> { child1, new RecursiveStruct { Id = 3, Name = "Alt" } }
            };

            RecursiveStructArray = new[]
            {
                RootRecursiveStruct,
                new RecursiveStruct { Id = 100, Name = "Standalone" }
            };

            // WeirdPayload + InnerContainer cycle-like nesting
            StrangePayload = new WeirdPayload
            {
                CorrelationId = Guid.NewGuid(),
                DynamicData = new Dictionary<string, object>
                {
                    { "pi", Math.PI },
                    { "list", new[] { 1, 2, 3 } },
                    { "nested", new Dictionary<string, object> { { "x", "y" } } }
                },
                SelfReference = null // avoid real cycle for now, but field exists
            };

            ContainerWithPayload = new InnerContainer
            {
                Label = "TestContainer",
                Created = DateTime.UtcNow,
                Payload = StrangePayload
            };

            StrangePayload.History = new List<InnerContainer>
            {
                ContainerWithPayload,
                new InnerContainer { Label = "Old", Created = new DateTime(2000, 1, 1), Payload = new WeirdPayload { CorrelationId = Guid.Empty } }
            };

            ContainerList = new List<InnerContainer> { ContainerWithPayload };

            // ConstrainedEntity examples
            EntityWithGuidKey = new ConstrainedEntity<Guid, List<string>>
            {
                Key = Guid.NewGuid(),
                Value = new List<string> { "alpha", "beta" },
                Related = new List<ConstrainedEntity<Guid, List<string>>>
                {
                    new ConstrainedEntity<Guid, List<string>>
                    {
                        Key = Guid.NewGuid(),
                        Value = new List<string> { "gamma" }
                    }
                },
                Lookup = new Dictionary<Guid, List<List<string>>>
                {
                    {
                        Guid.NewGuid(),
                        new List<List<string>>
                        {
                            new List<string> { "one", "two" },
                            new List<string> { "alpha", "beta" }
                        }
                    },
                    {
                        Guid.NewGuid(),
                        new List<List<string>>
                        {
                            new List<string> { "single" }
                        }
                    }
                }
            };

            EntityWithComplexValue = new ConstrainedEntity<string, TupleMadness>
            {
                Key = "MadnessRoot",
                Value = new TupleMadness(), // will populate below
                ValueArray = new[] { new TupleMadness() }
            };

            // TupleMadness
            TupleInsanity = new TupleMadness
            {
                BasicTuple = (42, "Grok", DateTime.Now),
                ScoredItems = new List<(Guid, double, bool?)>
                {
                    (Guid.NewGuid(), 0.99, true),
                    (Guid.NewGuid(), 0.50, null),
                    (Guid.NewGuid(), 0.01, false)
                },
                TaggedPolygons = new[]
                {
                    ("Triangle", new List<(int X, int Y)> { (0,0), (10,0), (5,15) }),
                    ("Square", new List<(int X, int Y)> { (0,0), (10,0), (10,10), (0,10) })
                },
                StrangeMap = new Dictionary<string, (Complex, BigInteger)>
                {
                    { "origin", (new Complex(0, 0), BigInteger.Zero) },
                    { "unit", (new Complex(1, 1), BigInteger.One) }
                },
                MixedTupleArray = new[]
                {
                    (1, new RecursiveStruct { Id = 10, Name = "InTuple" }),
                    (2, new RecursiveStruct())
                }
            };

            EntityWithComplexValue.Value = TupleInsanity;
            EntityWithComplexValue.ValueArray[0] = TupleInsanity;
            EntityWithComplexValue.Related = new List<ConstrainedEntity<string, TupleMadness>>
            {
                new ConstrainedEntity<string, TupleMadness> { Key = "Child1", Value = TupleInsanity, ValueArray = new[] { TupleInsanity } },
                new ConstrainedEntity<string, TupleMadness> { Key = "Child2", Value = new TupleMadness { BasicTuple = (7, "Leaf", DateTime.MinValue) } }
            };
            EntityWithComplexValue.Lookup = new Dictionary<string, List<TupleMadness>>
            {
                { "alpha", new List<TupleMadness> { TupleInsanity } },
                { "beta",  new List<TupleMadness> { new TupleMadness { BasicTuple = (99, "Beta", DateTime.MaxValue) }, TupleInsanity } }
            };
        }
        public void Clear()
        {
            // Simple value types and their nullable counterparts -> default is fine (0, false, null, etc.)
            BoolField = default;
            ByteField = default;
            SByteField = default;
            Int16Field = default;
            UInt16Field = default;
            Int32Field = default;
            UInt32Field = default;
            Int64Field = default;
            UInt64Field = default;
            FloatField = default;
            DoubleField = default;
            DecimalField = default;
            CharField = default;
            BigIntegerField = default;
            DateTimeField = default;
            GuidField = default;
            TimeSpanField = default;

            // Reference types -> set to null
            StringField = null;
            UriField = null;
            IpAddressField = null;
            VersionField = null;
            ComplexClassField = default; // struct, but okay to set to default
            DateTimeOffsetField = default;  // not nullable, so default rather than null
            PriorityField = default;
            AccessField = default;

            // Nullable value types -> set to null
            NullableBooleanField = null;
            NullableByteField = null;
            NullableSByteField = null;
            NullableCharField = null;
            NullableInt16Field = null;
            NullableUInt16Field = null;
            NullableInt32Field = null;
            NullableUInt32Field = null;
            NullableInt64Field = null;
            NullableUInt64Field = null;
            NullableSingleField = null;
            NullableDoubleField = null;
            NullableDecimalField = null;
            NullableDateTimeField = null;
            NullableTimeSpanField = null;
            NullableGuidField = null;
            NullableBigIntegerField = null;

            // Arrays -> set to null (or new T[0] if you prefer empty array instead of null)
            IntArrayField = null;
            BoolArrayField = null;
            ByteArrayField = null;
            SByteArrayField = null;
            Int16ArrayField = null;
            UInt16ArrayField = null;
            Int32ArrayField = null;
            UInt32ArrayField = null;
            Int64ArrayField = null;
            UInt64ArrayField = null;
            FloatArrayField = null;
            DoubleArrayField = null;
            DecimalArrayField = null;
            CharArrayField = null;
            StringArrayField = null;
            DateTimeArrayField = null;
            GuidArrayField = null;
            ComplexArrayField = null;

            // Lists -> set to null (or new List<T>() if you want empty lists)
            BoolList = null;
            ByteList = null;
            SByteList = null;
            Int16List = null;
            UInt16List = null;
            Int32List = null;
            UInt32List = null;
            Int64List = null;
            UInt64List = null;
            FloatList = null;
            DoubleList = null;
            DecimalList = null;
            CharList = null;
            StringList = null;
            DateTimeList = null;
            GuidList = null;
            ComplexListField = null;

            // Arrays of lists (List<T>[])
            StringListArrayField = null;
            IntListArrayField = null;
            BoolListArrayField = null;
            ByteListArrayField = null;
            SByteListArrayField = null;
            Int16ListArrayField = null;
            UInt16ListArrayField = null;
            UInt32ListArrayField = null;
            Int64ListArrayField = null;
            UInt64ListArrayField = null;
            FloatListArrayField = null;
            DoubleListArrayField = null;
            DecimalListArrayField = null;
            CharListArrayField = null;
            DateTimeListArrayField = null;
            GuidListArrayField = null;
            TimeSpanListArrayField = null;
            BigIntegerListArrayField = null;

            // Arrays of nullable lists
            NullableBoolListArrayField = null;
            NullableByteListArrayField = null;
            NullableSByteListArrayField = null;
            NullableInt16ListArrayField = null;
            NullableUInt16ListArrayField = null;
            NullableInt32ListArrayField = null;
            NullableUInt32ListArrayField = null;
            NullableInt64ListArrayField = null;
            NullableUInt64ListArrayField = null;
            NullableFloatListArrayField = null;
            NullableDoubleListArrayField = null;
            NullableDecimalListArrayField = null;
            NullableCharListArrayField = null;
            NullableDateTimeListArrayField = null;
            NullableTimeSpanListArrayField = null;
            NullableGuidListArrayField = null;
            NullableBigIntegerListArrayField = null;

            ComplexListArrayField = null;

            // Clear new fields
            StringTree = null;
            IntTree = null;
            RootRecursiveStruct = default;
            RecursiveStructArray = null;
            ContainerWithPayload = default;
            ContainerList = null;
            StrangePayload = null;
            EntityWithGuidKey = null;
            EntityWithComplexValue = null;
            TupleInsanity = null;
        }
    }
}
