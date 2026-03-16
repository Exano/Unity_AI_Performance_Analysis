using FrameAnalyzer.Runtime.Data;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace FrameAnalyzer.Runtime.Collectors
{
    public class MemoryCollector : IFrameDataCollector
    {
        private ProfilerRecorder _gcAllocPerFrame;
        private ProfilerRecorder _gcAllocCountPerFrame;

        public void Begin()
        {
            _gcAllocPerFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcAllocCountPerFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
        }

        public void Collect(FrameSnapshot snapshot)
        {
            snapshot.Memory = new MemoryData
            {
                WasCollected = true,
                ManagedHeapBytes = Profiler.GetMonoHeapSizeLong(),
                ManagedUsedBytes = Profiler.GetMonoUsedSizeLong(),
                NativeMemoryBytes = Profiler.GetTotalAllocatedMemoryLong(),
                GcAllocBytes = _gcAllocPerFrame.Valid ? _gcAllocPerFrame.LastValue : 0,
                GcAllocCount = _gcAllocCountPerFrame.Valid ? (int)_gcAllocCountPerFrame.LastValue : 0
            };
        }

        public void End()
        {
            _gcAllocPerFrame.Dispose();
            _gcAllocCountPerFrame.Dispose();
        }
    }
}
