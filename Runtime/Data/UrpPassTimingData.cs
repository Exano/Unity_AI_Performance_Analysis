using System;
using System.Collections.Generic;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public struct UrpPassEntry
    {
        public string PassName;
        public double CpuMs;
        public double GpuMs;
    }

    [Serializable]
    public struct UrpPassTimingData
    {
        public bool WasCollected;
        public List<UrpPassEntry> Passes;

        public static UrpPassTimingData Create()
        {
            return new UrpPassTimingData
            {
                WasCollected = false,
                Passes = new List<UrpPassEntry>()
            };
        }
    }
}
