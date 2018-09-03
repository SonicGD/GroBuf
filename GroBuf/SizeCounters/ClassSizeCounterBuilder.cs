using System;
using System.Reflection;

namespace GroBuf.SizeCounters
{
    internal class ClassSizeCounterBuilder : SizeCounterBuilderBase
    {
        public ClassSizeCounterBuilder(Type type)
            : base(type)
        {
        }

        protected override void BuildConstantsInternal(SizeCounterConstantsBuilderContext context)
        {
            foreach (var member in context.GetDataMembers(Type))
            {
                Type memberType;
                switch (member.Member.MemberType)
                {
                case MemberTypes.Property:
                    memberType = ((PropertyInfo)member.Member).PropertyType;
                    break;
                case MemberTypes.Field:
                    memberType = ((FieldInfo)member.Member).FieldType;
                    break;
                default:
                    throw new NotSupportedException("Data member of type " + member.Member.MemberType + " is not supported");
                }
                context.BuildConstants(memberType);
            }
        }

        protected override void CountSizeNotEmpty(SizeCounterMethodBuilderContext context)
        {
            var il = context.Il;

            il.Ldc_I4(0); // stack: [0 = size]

            var dataMembers = context.Context.GetDataMembers(Type);
            foreach (var member in dataMembers)
            {
                if (Type.IsValueType)
                    context.LoadObjByRef(); // stack: [size, ref obj]
                else
                    context.LoadObj(); // stack: [size, obj]
                Type memberType;
                switch (member.Member.MemberType)
                {
                case MemberTypes.Property:
                    var property = (PropertyInfo)member.Member;
                    var getter = property.GetGetMethod(true);
                    if (getter == null)
                        throw new MissingMethodException(Type.Name, property.Name + "_get");
                    il.Call(getter, Type); // stack: [size, obj.prop]
                    memberType = property.PropertyType;
                    break;
                case MemberTypes.Field:
                    var field = (FieldInfo)member.Member;
                    il.Ldfld(field); // stack: [size, obj.field]
                    memberType = field.FieldType;
                    break;
                default:
                    throw new NotSupportedException("Data member of type " + member.Member.MemberType + " is not supported");
                }
                il.Ldc_I4(0); // stack: [size, obj.member, false]
                context.LoadContext(); // stack: [size, obj.member, false, context]
                context.CallSizeCounter(memberType); // stack: [size, writers[i](obj.member, false, context) = memberSize]
                il.Dup(); // stack: [size, memberSize, memberSize]
                var nextLabel = il.DefineLabel("next");
                il.Brfalse(nextLabel); // if(memberSize = 0) goto next; stack: [size, memberSize]

                il.Ldc_I4(8); // stack: [size, memberSize, 8]
                il.Add(); // stack: [size, memberSize + 8]
                il.MarkLabel(nextLabel);
                il.Add(); // stack: [size + curSize]
            }

            if (!context.Context.GroBufWriter.Options.HasFlag(GroBufOptions.WriteEmptyObjects))
            {
                var countLengthLabel = il.DefineLabel("countLength");
                il.Dup(); // stack: [size, size]
                il.Brtrue(countLengthLabel); // if(size != 0) goto countLength; stack: [size]
                il.Pop(); // stack: []
                context.ReturnForNull();
                il.Ret();
                il.MarkLabel(countLengthLabel);
            }
            il.Ldc_I4(5); // stack: [size, 5]
            il.Add(); // stack: [size + 5]
        }

        protected override bool IsReference => true;
    }
}