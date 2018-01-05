using System;

namespace GroBuf.SizeCounters
{
    internal class GuidSizeCounterBuilder : SizeCounterBuilderBase
    {
        public GuidSizeCounterBuilder()
            : base(typeof(Guid))
        {
        }

        protected override void BuildConstantsInternal(SizeCounterConstantsBuilderContext context)
        {
        }

        protected override void CountSizeNotEmpty(SizeCounterMethodBuilderContext context)
        {
            context.Il.Ldc_I4(17); // stack: [17]
        }

        protected override bool IsReference { get { return false; } }
    }
}