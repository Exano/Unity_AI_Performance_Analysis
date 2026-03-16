using System;
using System.Collections.Generic;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public struct MemoryAssetEntry
    {
        public string Name;
        public string TypeName;
        public long SizeBytes;
    }

    [Serializable]
    public class MemoryCategorySummary
    {
        public string Category;
        public int Count;
        public long TotalBytes;
    }

    /// <summary>
    /// Breakdown of what's actually loaded in memory, by type and by individual asset.
    /// </summary>
    [Serializable]
    public class MemoryBreakdownData
    {
        public bool WasCollected;
        public long TotalTrackedBytes;
        public List<MemoryCategorySummary> ByCategory = new List<MemoryCategorySummary>();
        public List<MemoryAssetEntry> TopAssets = new List<MemoryAssetEntry>(); // Top N by size
    }
}
