using System;

namespace FrameAnalyzer.Runtime.Data
{
    public enum BottleneckType
    {
        Indeterminate,
        PresentLimited,
        CPU,
        GPU,
        Balanced
    }

    [Serializable]
    public struct BottleneckData
    {
        public bool WasCollected;
        public BottleneckType Bottleneck;
        public double CpuFrameTimeMs;
        public double GpuFrameTimeMs;
    }
}
