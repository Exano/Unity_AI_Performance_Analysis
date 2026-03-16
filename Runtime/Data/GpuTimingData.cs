using System;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public struct GpuTimingData
    {
        public bool WasCollected;
        public double CpuFrameTimeMs;
        public double GpuFrameTimeMs;
        public double CpuMainThreadMs;
        public double CpuRenderThreadMs;
    }
}
