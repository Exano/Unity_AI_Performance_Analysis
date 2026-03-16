using System;
using System.Collections.Generic;
using System.Linq;
using FrameAnalyzer.Runtime.Data;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace FrameAnalyzer.Editor.Capture
{
    /// <summary>
    /// Post-capture analysis: reads back profiler hierarchy data from Unity's Profiler
    /// to extract per-method timing, call counts, and GC allocations.
    /// Computes Profile Analyzer-quality statistics (median, P95, stddev, quartiles).
    /// The Profile Analyzer package's classes are internal, so we replicate the
    /// statistical approach using the same raw ProfilerDriver data.
    /// </summary>
    public static class ProfilerHierarchyAnalyzer
    {
        private const int TopN = 20;

        public static ProfilerHierarchyData Analyze(int framesToAnalyze)
        {
            var result = new ProfilerHierarchyData();

            try
            {
                if (!ProfilerDriver.enabled)
                {
                    Debug.LogWarning("[FrameAnalyzer] Profiler not enabled — skipping hierarchy analysis.");
                    return result;
                }

                int lastFrame = ProfilerDriver.lastFrameIndex;
                int firstFrame = Mathf.Max(ProfilerDriver.firstFrameIndex, lastFrame - framesToAnalyze + 1);
                int frameCount = lastFrame - firstFrame + 1;

                if (frameCount <= 0)
                    return result;

                // Key: marker name → list of per-frame entries
                var markerAccum = new Dictionary<string, List<ProfilerMarkerEntry>>();

                for (int f = firstFrame; f <= lastFrame; f++)
                    CollectFrameMarkers(f, markerAccum);

                result.FramesAnalyzed = frameCount;
                result.WasCollected = markerAccum.Count > 0;

                // Compute full statistics for each marker
                var allSummaries = new List<ProfilerMarkerSummary>();
                foreach (var kvp in markerAccum)
                    allSummaries.Add(ComputeMarkerStats(kvp.Key, kvp.Value, frameCount));

                // Top N by median self time (more stable than mean for profiler data)
                result.TopBySelfTime = allSummaries
                    .OrderByDescending(m => m.MedianSelfMs)
                    .Take(TopN)
                    .ToList();

                // Top N by GC allocation
                result.TopByGcAlloc = allSummaries
                    .Where(m => m.AvgGcAllocBytes > 0)
                    .OrderByDescending(m => m.AvgGcAllocBytes)
                    .Take(TopN)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FrameAnalyzer] Profiler hierarchy analysis failed: {e.Message}");
            }

            return result;
        }

        static ProfilerMarkerSummary ComputeMarkerStats(string name, List<ProfilerMarkerEntry> entries, int totalFrames)
        {
            var selfTimes = entries.Select(e => e.SelfMs).OrderBy(v => v).ToList();
            var totalTimes = entries.Select(e => e.TotalMs).OrderBy(v => v).ToList();
            var calls = entries.Select(e => (double)e.Calls).OrderBy(v => v).ToList();
            var gcAllocs = entries.Select(e => (double)e.GcAllocBytes).OrderBy(v => v).ToList();

            double avgSelf = selfTimes.Average();

            return new ProfilerMarkerSummary
            {
                Name = name,

                MedianSelfMs = Percentile(selfTimes, 0.5),
                AvgSelfMs = avgSelf,
                MinSelfMs = selfTimes.First(),
                MaxSelfMs = selfTimes.Last(),
                P95SelfMs = Percentile(selfTimes, 0.95),
                StdDevSelfMs = StdDev(selfTimes, avgSelf),

                MedianTotalMs = Percentile(totalTimes, 0.5),
                AvgTotalMs = totalTimes.Average(),

                MedianCalls = (int)Percentile(calls, 0.5),
                AvgCalls = (int)Math.Round(calls.Average()),
                MaxCalls = (int)calls.Last(),

                MedianGcAllocBytes = (long)Percentile(gcAllocs, 0.5),
                AvgGcAllocBytes = (long)gcAllocs.Average(),
                MaxGcAllocBytes = (long)gcAllocs.Last(),

                FrameCount = entries.Count,
                TotalFrames = totalFrames
            };
        }

        static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            if (sorted.Count == 1) return sorted[0];
            double idx = (sorted.Count - 1) * p;
            int lo = (int)Math.Floor(idx);
            int hi = lo + 1;
            if (hi >= sorted.Count) return sorted[sorted.Count - 1];
            double w = idx - lo;
            return sorted[lo] * (1.0 - w) + sorted[hi] * w;
        }

        static double StdDev(List<double> values, double mean)
        {
            if (values.Count <= 1) return 0;
            double sumSq = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSq / (values.Count - 1));
        }

        // ── Profiler data extraction ──

        static void CollectFrameMarkers(int frameIndex, Dictionary<string, List<ProfilerMarkerEntry>> accum)
        {
            using (var frameData = ProfilerDriver.GetHierarchyFrameDataView(
                frameIndex, 0, HierarchyFrameDataView.ViewModes.Default,
                HierarchyFrameDataView.columnSelfTime, false))
            {
                if (frameData == null || !frameData.valid)
                    return;

                int rootId = frameData.GetRootItemID();
                var children = new List<int>();
                TraverseHierarchy(frameData, rootId, children, accum, 0);
            }
        }

        static void TraverseHierarchy(HierarchyFrameDataView frameData, int itemId,
            List<int> childBuffer, Dictionary<string, List<ProfilerMarkerEntry>> accum, int depth)
        {
            if (depth > 30) return;

            childBuffer.Clear();
            frameData.GetItemChildren(itemId, childBuffer);
            var children = new List<int>(childBuffer);

            foreach (int childId in children)
            {
                string name = frameData.GetItemName(childId);
                float selfMs = frameData.GetItemColumnDataAsSingle(childId, HierarchyFrameDataView.columnSelfTime);
                float totalMs = frameData.GetItemColumnDataAsSingle(childId, HierarchyFrameDataView.columnTotalTime);
                int calls = (int)frameData.GetItemColumnDataAsSingle(childId, HierarchyFrameDataView.columnCalls);
                float gcAllocBytes = frameData.GetItemColumnDataAsSingle(childId, HierarchyFrameDataView.columnGcMemory);

                if (selfMs > 0.001f || gcAllocBytes > 0)
                {
                    if (!accum.TryGetValue(name, out var list))
                    {
                        list = new List<ProfilerMarkerEntry>();
                        accum[name] = list;
                    }
                    list.Add(new ProfilerMarkerEntry
                    {
                        Name = name,
                        SelfMs = selfMs,
                        TotalMs = totalMs,
                        Calls = calls,
                        GcAllocBytes = (long)gcAllocBytes
                    });
                }

                if (frameData.HasItemChildren(childId))
                    TraverseHierarchy(frameData, childId, childBuffer, accum, depth + 1);
            }
        }
    }
}
