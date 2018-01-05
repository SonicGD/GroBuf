﻿using System;
using System.Runtime.InteropServices;

using GroBuf.DataMembersExtracters;
using GroBuf.Tests.TestTools;

using NUnit.Framework;

namespace GroBuf.Tests
{
    [TestFixture]
    public class TestCustomReadWrite
    {
        [SetUp]
        public void SetUp()
        {
            serializer = new Serializer(new PropertiesExtractor());
        }

        [Test]
        public void TestSizeCounter()
        {
            var a = new A {B = new C {Z = "zzz"}};
            var size = serializer.GetSize(a);
            Assert.AreEqual((1 + 4 + 8) + (1 + 4 + 2) + (1 + 4 + 8 + 1 + 4 + 6) + 5, size);
        }

        [Test]
        public void TestWriter()
        {
            var a = new A {B = new C {Z = "zzz"}};
            var data = serializer.Serialize(a);
            Assert.AreEqual((1 + 4 + 8) + (1 + 4 + 2) + (1 + 4 + 8 + 1 + 4 + 6) + 5, data.Length);
        }

        [Test]
        public void TestReaderC()
        {
            var a = new A {B = new C {Z = "zzz"}, ArrB = new BB[] {new C {Z = "qxx"}, new D {Z = 123}}};
            var data = serializer.Serialize(a);
            var za = serializer.Deserialize<A>(data);
            za.AssertEqualsTo(a);
        }

        [Test]
        public void TestReaderD()
        {
            var a = new A {B = new D {Z = 146}, ArrB = new BB[] {new C {Z = "qxx"}, new D {Z = 123}}};
            var data = serializer.Serialize(a);
            var za = serializer.Deserialize<A>(data);
            za.AssertEqualsTo(a);
        }

        [Test]
        public void TestStruct()
        {
            var date = new Date {Year = 2012, Month = 12, Day = 21};
            var data = serializer.Serialize(date);
            var zdate = serializer.Deserialize<Date>(data);
            zdate.AssertEqualsTo(date);
        }

        [Test]
        public void TestBase()
        {
            var z = new Z {X = 5, S = "zzz"};
            var data = serializer.Serialize(z);
            var zz = serializer.Deserialize<Z>(data);
            Assert.AreEqual(5, zz.X);
            Assert.AreEqual("5", zz.S);
        }

        [GroBufCustomSerialization]
        public class Z
        {
            [GroBufSizeCounter]
            public static SizeCounterDelegate GetSizeCounter(Func<Type, SizeCounterDelegate> sizeCountersFactory, SizeCounterDelegate baseSizeCounter)
            {
                return baseSizeCounter;
            }

            [GroBufWriter]
            public static WriterDelegate GetWriter(Func<Type, WriterDelegate> writersFactory, WriterDelegate baseWriter)
            {
                return baseWriter;
            }

            [GroBufReader]
            public static ReaderDelegate GetReader(Func<Type, ReaderDelegate> readersFactory, ReaderDelegate baseReader)
            {
                return (IntPtr data, ref int index, ref object result, ReaderContext context) =>
                           {
                               baseReader(data, ref index, ref result, context);
                               var z = result as Z;
                               if(z != null)
                                   z.S = z.X.ToString();
                               return;
                           };
            }

            public int X { get; set; }
            public string S;
        }

        public class A
        {
            public BB B { get; set; }
            public BB[] ArrB { get; set; }
        }

        [GroBufCustomSerialization]
        public abstract class B
        {
            [GroBufSizeCounter]
            public static SizeCounterDelegate GetSizeCounter(Func<Type, SizeCounterDelegate> sizeCountersFactory, SizeCounterDelegate baseSizeCounter)
            {
                return (o, writeEmpty, context) =>
                           {
                               Type type = o.GetType();
                               return sizeCountersFactory(typeof(string))(type.Name, writeEmpty, context) + sizeCountersFactory(type)(o, writeEmpty, context);
                           };
            }

            [GroBufWriter]
            public static WriterDelegate GetWriter(Func<Type, WriterDelegate> writersFactory, WriterDelegate baseWriter)
            {
                return (object o, bool writeEmpty, IntPtr result, ref int index, WriterContext context) =>
                           {
                               Type type = o.GetType();
                               writersFactory(typeof(string))(type.Name, writeEmpty, result, ref index, context);
                               writersFactory(type)(o, writeEmpty, result, ref index, context);
                           };
            }

            [GroBufReader]
            public static ReaderDelegate GetReader(Func<Type, ReaderDelegate> readersFactory, ReaderDelegate baseReader)
            {
                return (IntPtr data, ref int index, ref object result, ReaderContext context) =>
                           {
                               object type = null;
                               readersFactory(typeof(string))(data, ref index, ref type, context);
                               if((string)type == typeof(C).Name)
                                   result = new C();
                               else if((string)type == typeof(D).Name)
                                   result = new D();
                               else throw new InvalidOperationException("Unknown type " + type);
                               readersFactory(result.GetType())(data, ref index, ref result, context);
                           };
            }
        }

        [GroBufCustomSerialization]
        public abstract class BB : B
        {
        }

        public class C : BB
        {
            public string Z { get; set; }
        }

        public class D : BB
        {
            public int Z { get; set; }
        }

        public struct Date
        {
            [GroBufSizeCounter]
            public static SizeCounterDelegate GetSizeCounter(Func<Type, SizeCounterDelegate> sizeCountersFactory, SizeCounterDelegate baseSizeCounter)
            {
                return (o, writeEmpty, context) => 8;
            }

            [GroBufWriter]
            public static WriterDelegate GetWriter(Func<Type, WriterDelegate> writersFactory, WriterDelegate baseWriter)
            {
                return (object o, bool writeEmpty, IntPtr result, ref int index, WriterContext context) =>
                           {
                               var date = (Date)o;
                               var bytes = BitConverter.GetBytes(new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc).Ticks);
                               Marshal.Copy(bytes, 0, result + index, bytes.Length);
                               index += bytes.Length;
                           };
            }

            [GroBufReader]
            public static ReaderDelegate GetReader(Func<Type, ReaderDelegate> readersFactory, ReaderDelegate baseReader)
            {
                return (IntPtr data, ref int index, ref object result, ReaderContext context) =>
                           {
                               var bytes = new byte[8];
                               Marshal.Copy(data + index, bytes, 0, 8);
                               var dateTime = new DateTime(BitConverter.ToInt64(bytes, 0), DateTimeKind.Utc);
                               result = new Date {Year = dateTime.Year, Month = dateTime.Month, Day = dateTime.Day};
                               index += 8;
                           };
            }

            public int Year { get; set; }
            public int Month { get; set; }
            public int Day { get; set; }
        }

        private Serializer serializer;
    }
}