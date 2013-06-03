using System;

namespace GroBuf
{
    public interface IGroBufCustomSerializer
    {
        int CountSize(object obj, bool writeEmpty);
        void Write(object obj, bool writeEmpty, IntPtr result, ref int index);
        void Read(IntPtr data, ref int index, int length, ref object result);
    }

    internal class GroBufCustomSerializerByAttribute : IGroBufCustomSerializer
    {
        private readonly SizeCounterDelegate sizeCounter;
        private readonly WriterDelegate writerDelegate;
        private readonly ReaderDelegate readerDelegate;

        public GroBufCustomSerializerByAttribute(SizeCounterDelegate sizeCounter, WriterDelegate writerDelegate, ReaderDelegate readerDelegate)
        {
            this.sizeCounter = sizeCounter;
            this.writerDelegate = writerDelegate;
            this.readerDelegate = readerDelegate;
            
        }

        public int CountSize(object obj, bool writeEmpty)
        {
            return sizeCounter(obj, writeEmpty);
        }

        public void Write(object obj, bool writeEmpty, IntPtr result, ref int index)
        {
            writerDelegate(obj, writeEmpty, result, ref index);
        }

        public void Read(IntPtr data, ref int index, int length, ref object result)
        {
            readerDelegate(data, ref index, length, ref result);
        }
    }
}