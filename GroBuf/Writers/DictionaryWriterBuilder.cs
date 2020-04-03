using System;
using System.Collections.Generic;
using System.Reflection;

using GrEmit;

namespace GroBuf.Writers
{
    internal class DictionaryWriterBuilder : WriterBuilderBase
    {
        public DictionaryWriterBuilder(Type type)
            : base(type)
        {
            if (!(Type.IsGenericType && Type.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
                throw new InvalidOperationException("Dictionary expected but was '" + Type + "'");
            keyType = Type.GetGenericArguments()[0];
            valueType = Type.GetGenericArguments()[1];
        }

        protected override bool CheckEmpty(WriterMethodBuilderContext context, GroboIL.Label notEmptyLabel)
        {
            context.LoadObj(); // stack: [obj]
            if (context.Context.GroBufWriter.Options.HasFlag(GroBufOptions.WriteEmptyObjects))
                context.Il.Brtrue(notEmptyLabel); // if(obj != null) goto notEmpty;
            else
            {
                var emptyLabel = context.Il.DefineLabel("empty");
                context.Il.Brfalse(emptyLabel); // if(obj == null) goto empty;
                context.LoadObj(); // stack: [obj]
                context.Il.Call(Type.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public).GetGetMethod()); // stack: [obj.Count]
                context.Il.Brtrue(notEmptyLabel); // if(obj.Count != 0) goto notEmpty;
                context.Il.MarkLabel(emptyLabel);
            }
            return true;
        }

        protected override bool IsReference => true;

        protected override void BuildConstantsInternal(WriterConstantsBuilderContext context)
        {
            context.BuildConstants(keyType);
            context.BuildConstants(valueType);
        }

        protected override void WriteNotEmpty(WriterMethodBuilderContext context)
        {
            var il = context.Il;
            context.WriteTypeCode(GroBufTypeCode.Dictionary);
            context.LoadIndex(); // stack: [index]
            var start = context.LocalInt;
            il.Stloc(start); // start = index
            il.Ldc_I4(8); // data length + dict size = 8
            context.AssertLength();
            context.IncreaseIndexBy4(); // index = index + 4
            context.GoToCurrentLocation(); // stack: [&result[index]]
            context.LoadObj(); // stack: [&result[index], obj]
            il.Call(Type.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public).GetGetMethod()); // stack: [&result[index], obj.Count]
            il.Stind(typeof(int)); // *(int*)&result[index] = obj.Count; stack: []
            context.IncreaseIndexBy4(); // index = index + 4; stack: []

            context.LoadObj(); // stack: [obj]
            // traverse all buckets
            il.Ldfld(Type.GetPrivateInstanceField(PlatformHelpers.DictionaryCountFieldNames)); // stack: [obj.count]
            var count = il.DeclareLocal(typeof(int));
            il.Stloc(count); // count = obj.count; stack: []

            var writeDataLengthLabel = il.DefineLabel("writeDataLength");
            il.Ldloc(count); // stack: [count]
            il.Brfalse(writeDataLengthLabel); // if(count == 0) goto writeDataLength; stack: []

            context.LoadObj(); // stack: [obj]
            var entryType = Type.GetNestedType("Entry", BindingFlags.NonPublic).MakeGenericType(Type.GetGenericArguments());
            var hashCodeType = entryType.GetField("hashCode", BindingFlags.Public | BindingFlags.Instance).FieldType;
            var isNetCore3 = hashCodeType == typeof(uint);
            var entries = il.DeclareLocal(entryType.MakeArrayType());
            il.Ldfld(Type.GetPrivateInstanceField(PlatformHelpers.DictionaryEntriesFieldNames)); // stack: [obj.entries]
            il.Stloc(entries); // entries = obj.entries; stack: []

            var i = il.DeclareLocal(typeof(int));
            il.Ldc_I4(0); // stack: [0]
            il.Stloc(i); // i = 0; stack: []
            var cycleStartLabel = il.DefineLabel("cycleStart");
            il.MarkLabel(cycleStartLabel);
            il.Ldloc(entries); // stack: [entries]
            il.Ldloc(i); // stack: [entries, i]
            il.Ldelema(entryType); // stack: [&entries[i]]
            il.Dup(); // stack: [&entries[i], &entries[i]]
            var entry = il.DeclareLocal(entryType.MakeByRefType());
            il.Stloc(entry); // entry = &entries[i]; stack: [entry]
            if (!isNetCore3)
            {
                il.Ldfld(entryType.GetField("hashCode")); // stack: [entry.hashCode]
                il.Ldc_I4(0); // stack: [entry.hashCode, 0]
            }
            else
            {
                il.Ldfld(entryType.GetField("next")); // stack: [entry.next]
                il.Ldc_I4(-1); // stack: [entry.next, -1]
            }
            var nextLabel = il.DefineLabel("next");
            il.Blt(nextLabel, false); // if(entry.hashCode < 0) goto next; stack: []

            il.Ldloc(entry); // stack: [entry]
            il.Ldfld(entryType.GetField("key")); // stack: [entry.key]
            il.Ldc_I4(1); // stack: [obj[i].key, true]
            context.LoadResult(); // stack: [obj[i].key, true, result]
            context.LoadIndexByRef(); // stack: [obj[i].key, true, result, ref index]
            context.LoadContext(); // stack: [obj[i].key, true, result, ref index, context]
            context.CallWriter(keyType); // write<keyType>(obj[i].key, true, result, ref index, context); stack: []

            il.Ldloc(entry); // stack: [entry]
            il.Ldfld(entryType.GetField("value")); // stack: [entry.value]
            il.Ldc_I4(1); // stack: [obj[i].value, true]
            context.LoadResult(); // stack: [obj[i].value, true, result]
            context.LoadIndexByRef(); // stack: [obj[i].value, true, result, ref index]
            context.LoadContext(); // stack: [obj[i].value, true, result, ref index, context]
            context.CallWriter(valueType); // writer<valueType>(obj[i].value, true, result, ref index, context); stack: []

            il.MarkLabel(nextLabel);
            il.Ldloc(count); // stack: [ count]
            il.Ldloc(i); // stack: [count, i]
            il.Ldc_I4(1); // stack: [count, i, 1]
            il.Add(); // stack: [count, i + 1]
            il.Dup(); // stack: [count, i + 1, i + 1]
            il.Stloc(i); // i = i + 1; stack: [count, i]
            il.Bgt(cycleStartLabel, false); // if(count > i) goto cycleStart; stack: []

            il.MarkLabel(writeDataLengthLabel);
            context.LoadResult(); // stack: [result]
            il.Ldloc(start); // stack: [result, start]
            il.Add(); // stack: [result + start]
            context.LoadIndex(); // stack: [result + start, index]
            il.Ldloc(start); // stack: [result + start, index, start]
            il.Sub(); // stack: [result + start, index - start]
            il.Ldc_I4(4); // stack: [result + start, index - start, 4]
            il.Sub(); // stack: [result + start, index - start - 4]
            il.Stind(typeof(int)); // *(int*)(result + start) = index - start - 4
        }

        private readonly Type keyType;
        private readonly Type valueType;
    }
}