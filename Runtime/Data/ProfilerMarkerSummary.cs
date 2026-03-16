using System;
using System.Collections.Generic;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public struct ProfilerMarkerEntry
    {
        public string Name;
        public double SelfMs;
        public double TotalMs;
        public int Calls;
        public long GcAllocBytes;
    }

    /// <summary>
    /// Aggregated profiler marker data across captured frames.
    /// Includes Profile Analyzer-quality statistics: median, quartiles, stddev.
    /// </summary>
    [Serializable]
    public class ProfilerMarkerSummary
    {
        public string Name;

        // Self time (excluding children) — the actual cost of this method
        public double MedianSelfMs;
        public double AvgSelfMs;
        public double MaxSelfMs;
        public double MinSelfMs;
        public double P95SelfMs;
        public double StdDevSelfMs;

        // Total time (including children)
        public double MedianTotalMs;
        public double AvgTotalMs;

        // Call counts
        public int MedianCalls;
        public int AvgCalls;
        public int MaxCalls;

        // GC allocations
        public long MedianGcAllocBytes;
        public long AvgGcAllocBytes;
        public long MaxGcAllocBytes;

        // Presence
        public int FrameCount;       // Frames this marker appeared in
        public int TotalFrames;      // Total frames analyzed (for % presence)
    }

    /// <summary>
    /// Post-capture analysis results from the profiler hierarchy.
    /// Stored at the session level (not per-frame).
    /// </summary>
    [Serializable]
    public class ProfilerHierarchyData
    {
        public bool WasCollected;
        public int FramesAnalyzed;
        public List<ProfilerMarkerSummary> TopBySelfTime = new List<ProfilerMarkerSummary>();
        public List<ProfilerMarkerSummary> TopByGcAlloc = new List<ProfilerMarkerSummary>();
        public string MemorySnapshotPath;
    }
}
