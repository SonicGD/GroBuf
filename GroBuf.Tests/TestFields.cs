﻿using GroBuf.DataMembersExtracters;

using NUnit.Framework;

namespace GroBuf.Tests
{
    [TestFixture]
    public class TestFields
    {
        [SetUp]
        public void SetUp()
        {
            serializer = new Serializer(new FieldsExtractor());
        }

        [Test]
        public void Test()
        {
            var o = new A {Bool = true, Ints = new[] {10, 2, 4}, B = new B {S = "zzz", Long = 123456789123456789}};
            byte[] data = serializer.Serialize(o);
            var oo = serializer.Deserialize<A>(data);
            Assert.AreEqual(true, oo.Bool);
            Assert.IsNotNull(oo.Ints);
            Assert.AreEqual(3, oo.Ints.Length);
            Assert.AreEqual(10, oo.Ints[0]);
            Assert.AreEqual(2, oo.Ints[1]);
            Assert.AreEqual(4, oo.Ints[2]);
            Assert.IsNotNull(oo.B);
            Assert.AreEqual("zzz", oo.B.S);
            Assert.AreEqual(123456789123456789, oo.B.Long);
        }

        [Test]
        public void TestReadonlyField()
        {
            var o = new C("zzz");
            byte[] data = serializer.Serialize(o);
            var oo = serializer.Deserialize<C>(data);
            Assert.AreEqual("zzz", oo.S);
        }

        public class A
        {
            public int[] Ints;
            public bool? Bool;
            public B B;
        }

        public class B
        {
            public string S;
            public long? Long;
        }

        public class C
        {
            public C(string s)
            {
                S = s;
            }

            public C()
            {
            }

            public readonly string S;
        }

        private Serializer serializer;
    }
}