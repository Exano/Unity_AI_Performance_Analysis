using System;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public struct MemoryData
    {
        public bool WasCollected;
        public long ManagedHeapBytes;
        public long ManagedUsedBytes;
        public long NativeMemoryBytes;
        public long GcAllocBytes;
        public int GcAllocCount;
    }
}
