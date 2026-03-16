using System;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public struct CpuTimingData
    {
        public bool WasCollected;
        public double PlayerLoopMs;
        public double UpdateMs;
        public double LateUpdateMs;
        public double FixedUpdateMs;
        public double RenderingMs;
        public double PhysicsMs;
        public double ScriptsMs;
        public double AnimationMs;
        public double GcCollectMs;
    }
}
