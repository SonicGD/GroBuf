using System;

namespace GroBuf
{
    internal class InternalSerializer : IGroBufCustomSerializer
    {
        public InternalSerializer(GroBufWriter groBufWriter, GroBufReader groBufReader, Type type, bool ignoreCustomSerializer)
        {
            this.groBufWriter = groBufWriter;
            this.groBufReader = groBufReader;
            this.type = type;
            this.ignoreCustomSerializer = ignoreCustomSerializer;
        }

        public int CountSize(object obj, bool writeEmpty, WriterContext context)
        {
            return groBufWriter.GetSize(type, ignoreCustomSerializer, obj, writeEmpty, context);
        }

        public void Write(object obj, bool writeEmpty, IntPtr result, ref int index, WriterContext context)
        {
            groBufWriter.Write(type, ignoreCustomSerializer, obj, writeEmpty, result, ref index, context);
        }

        public void Read(IntPtr data, ref int index, ref object result, ReaderContext context)
        {
            groBufReader.Read(type, ignoreCustomSerializer, data, ref index, ref result, context);
        }

        private readonly GroBufWriter groBufWriter;
        private readonly GroBufReader groBufReader;
        private readonly Type type;
        private readonly bool ignoreCustomSerializer;
    }
}