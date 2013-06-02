using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

using GrEmit;

namespace GroBuf.Readers
{
    internal class PrimitivesListReaderBuilder : ReaderBuilderBase
    {
        public PrimitivesListReaderBuilder(Type type)
            : base(type)
        {
            if(!(Type.IsGenericType && Type.GetGenericTypeDefinition() == typeof(List<>)))
                throw new InvalidOperationException("Expected list but was '" + Type + "'");
            elementType = Type.GetGenericArguments()[0];
            if(!elementType.IsPrimitive)
                throw new NotSupportedException("List of primitive type expected but was '" + Type + "'");
        }

        protected override void BuildConstantsInternal(ReaderConstantsBuilderContext context)
        {
            context.BuildConstants(elementType);
        }

        protected override unsafe void ReadNotEmpty(ReaderMethodBuilderContext context)
        {
            context.IncreaseIndexBy1();
            context.AssertTypeCode(GroBufTypeCodeMap.GetTypeCode(Type));

            var il = context.Il;
            var size = il.DeclareLocal(typeof(int));

            il.Ldc_I4(4);
            context.AssertLength();

            context.GoToCurrentLocation(); // stack: [&data[index]]
            il.Ldind(typeof(uint)); // stack: [data length]
            il.Dup(); // stack: [data length, data length]
            il.Stloc(size); // size = data length; stack: [data length]
            context.IncreaseIndexBy4(); // index = index + 4; stack: [data length]
            context.AssertLength();

            var length = context.Length;
            il.Ldloc(size); // stack: [size]
            CountArrayLength(elementType, il); // stack: [array length]
            il.Stloc(length); // length = array length

            var createArrayLabel = il.DefineLabel("createArray");
            context.LoadResult(Type); // stack: [result]
            il.Brfalse(createArrayLabel); // if(result == null) goto createArray; stack: []
            context.LoadResult(Type); // stack: [result]
            il.Ldfld(Type.GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)); // stack: [result._items]
            il.Ldlen(); // stack: [result._items.Length]
            il.Ldloc(length); // stack: [result.Length, length]

            var arrayCreatedLabel = il.DefineLabel("arrayCreated");
            il.Bge(typeof(int), arrayCreatedLabel); // if(result.Length >= length) goto arrayCreated;

            context.LoadResult(Type); // stack: [result]
            il.Ldflda(Type.GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)); // stack: [ref result._items]
            il.Ldloc(length); // stack: [ref result, length]
            il.Call(resizeMethod.MakeGenericMethod(elementType)); // Array.Resize(ref result, length)
            il.Br(arrayCreatedLabel); // goto arrayCreated

            il.MarkLabel(createArrayLabel);
            context.LoadResultByRef(); // stack: [ref result]
            il.Ldloc(length); // stack: [ref result, length]
            il.Newobj(Type.GetConstructor(new[] {typeof(int)})); // stack: [ref result, new List(length)]
            il.Stind(Type); // result = new List(length); stack: []

            il.MarkLabel(arrayCreatedLabel);

            il.Ldloc(length);
            var doneLabel = il.DefineLabel("done");
            il.Brfalse(doneLabel); // if(length == 0) goto allDone; stack: []

            var arr = il.DeclareLocal(elementType.MakeByRefType(), true);
            context.LoadResult(Type); // stack: [result]
            il.Ldfld(Type.GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)); // stack: [result._items]
            il.Ldc_I4(0); // stack: [result._items, 0]
            il.Ldelema(elementType); // stack: [&result._items[0]]
            il.Stloc(arr); // arr = &result._items[0]; stack: []
            il.Ldloc(arr); // stack: [arr]
            context.GoToCurrentLocation(); // stack: [arr, &data[index]]
            il.Ldloc(length); // stack: [arr, &data[index], length]
            CountArraySize(elementType, il); // stack: [arr, &data[index], size]
            if(sizeof(IntPtr) == 8)
                il.Unaligned(1L);
            il.Cpblk(); // arr = &data[index]
            il.Ldc_I4(0); // stack: [0]
            il.Conv_U(); // stack: [(uint)0]
            il.Stloc(arr); // arr = (uint)0;
            context.LoadIndexByRef(); // stack: [ref index]
            context.LoadIndex(); // stack: [ref index, index]
            il.Ldloc(size); // stack: [ref index, index, size]
            il.Add(); // stack: [ref index, index + size]
            il.Stind(typeof(int)); // index = index + size

            context.LoadResult(Type); // stack: [result]
            il.Ldfld(Type.GetField("_size", BindingFlags.Instance | BindingFlags.NonPublic)); // stack: [result.Count]
            il.Ldloc(length); // stack: [result.Count, length]
            il.Bge(typeof(int), doneLabel); // if(result.Count >= length) goto done; stack: []
            context.LoadResult(Type); // stack: [result]
            il.Ldloc(length); // stack: [result.Count, length]
            il.Stfld(Type.GetField("_size", BindingFlags.Instance | BindingFlags.NonPublic)); // result._size = length; stack: []

            il.MarkLabel(doneLabel); // stack: []
        }

        private static void CountArraySize(Type elementType, GroboIL il)
        {
            var typeCode = GroBufTypeCodeMap.GetTypeCode(elementType);
            switch(typeCode)
            {
            case GroBufTypeCode.Int8:
            case GroBufTypeCode.UInt8:
            case GroBufTypeCode.Boolean:
                break;
            case GroBufTypeCode.Int16:
            case GroBufTypeCode.UInt16:
                il.Ldc_I4(1);
                il.Shl();
                break;
            case GroBufTypeCode.Int32:
            case GroBufTypeCode.UInt32:
                il.Ldc_I4(2);
                il.Shl();
                break;
            case GroBufTypeCode.Int64:
            case GroBufTypeCode.UInt64:
                il.Ldc_I4(3);
                il.Shl();
                break;
            case GroBufTypeCode.Single:
                il.Ldc_I4(2);
                il.Shl();
                break;
            case GroBufTypeCode.Double:
                il.Ldc_I4(3);
                il.Shl();
                break;
            default:
                throw new NotSupportedException("Type '" + elementType + "' is not supported");
            }
        }

        private static void CountArrayLength(Type elementType, GroboIL il)
        {
            var typeCode = GroBufTypeCodeMap.GetTypeCode(elementType);
            switch(typeCode)
            {
            case GroBufTypeCode.Int8:
            case GroBufTypeCode.UInt8:
            case GroBufTypeCode.Boolean:
                break;
            case GroBufTypeCode.Int16:
            case GroBufTypeCode.UInt16:
                il.Ldc_I4(1);
                il.Shr(typeof(int));
                break;
            case GroBufTypeCode.Int32:
            case GroBufTypeCode.UInt32:
                il.Ldc_I4(2);
                il.Shr(typeof(int));
                break;
            case GroBufTypeCode.Int64:
            case GroBufTypeCode.UInt64:
                il.Ldc_I4(3);
                il.Shr(typeof(int));
                break;
            case GroBufTypeCode.Single:
                il.Ldc_I4(2);
                il.Shr(typeof(int));
                break;
            case GroBufTypeCode.Double:
                il.Ldc_I4(3);
                il.Shr(typeof(int));
                break;
            default:
                throw new NotSupportedException("Type '" + elementType + "' is not supported");
            }
        }

        private static readonly MethodInfo resizeMethod = ((MethodCallExpression)((Expression<Action<int[]>>)(arr => Array.Resize(ref arr, 0))).Body).Method.GetGenericMethodDefinition();
        private readonly Type elementType;
    }
}