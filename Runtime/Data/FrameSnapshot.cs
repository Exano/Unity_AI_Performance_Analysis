using System;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public class FrameSnapshot
    {
        public int FrameIndex;
        public CpuTimingData Cpu;
        public MemoryData Memory;
        public RenderingData Rendering;
        public GpuTimingData Gpu;
        public UrpPassTimingData UrpPasses;
        public BottleneckData Bottleneck;

        public FrameSnapshot()
        {
            UrpPasses = UrpPassTimingData.Create();
        }
    }
}
