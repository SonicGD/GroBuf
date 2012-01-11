using System;
using System.Collections;

namespace GroBuf.SizeCounters
{
    internal class SizeCounterCollection : ISizeCounterCollection
    {
        public ISizeCounterBuilder<T> GetSizeCounter<T>()
        {
            var type = typeof(T);
            var sizeCounterBuilder = (ISizeCounterBuilder<T>)sizeCounterBuilders[type];
            if(sizeCounterBuilder == null)
            {
                lock(sizeCounterBuildersLock)
                {
                    sizeCounterBuilder = (ISizeCounterBuilder<T>)sizeCounterBuilders[type];
                    if(sizeCounterBuilder == null)
                    {
                        sizeCounterBuilder = GetSizeCounterBuilder<T>();
                        sizeCounterBuilders[type] = sizeCounterBuilder;
                    }
                }
            }
            return sizeCounterBuilder;
        }

        private static ISizeCounterBuilder<T> GetSizeCounterBuilder<T>()
        {
            var type = typeof(T);
            ISizeCounterBuilder<T> sizeCounterBuilder;
            if(type == typeof(string))
                sizeCounterBuilder = (ISizeCounterBuilder<T>)new StringSizeCounterBuilder();
            else if(type == typeof(DateTime))
                sizeCounterBuilder = (ISizeCounterBuilder<T>)new DateTimeSizeCounterBuilder();
            else if(type == typeof(Guid))
                sizeCounterBuilder = (ISizeCounterBuilder<T>)new GuidSizeCounterBuilder();
            else if(type.IsEnum)
                sizeCounterBuilder = new EnumSizeCounterBuilder<T>();
            else if(type.IsPrimitive)
                sizeCounterBuilder = new PrimitivesSizeCounterBuilder<T>();
            else if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                sizeCounterBuilder = new NullableSizeCounterBuilder<T>();
            else if(type.IsArray)
                sizeCounterBuilder = new ArraySizeCounterBuilder<T>();
            else
                sizeCounterBuilder = new ClassSizeCounterBuilder<T>();
            return sizeCounterBuilder;
        }

        private readonly Hashtable sizeCounterBuilders = new Hashtable();
        private readonly object sizeCounterBuildersLock = new object();
    }
}