using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public class SessionSummary
    {
        // CPU
        public double AvgPlayerLoopMs, MinPlayerLoopMs, MaxPlayerLoopMs, P95PlayerLoopMs, P99PlayerLoopMs;
        public double AvgScriptsMs, AvgPhysicsMs, AvgRenderingMs, AvgAnimationMs;
        public double AvgGcCollectMs;

        // Memory
        public long AvgGcAllocBytes;
        public int AvgGcAllocCount;
        public long StartManagedHeap, EndManagedHeap;
        public long PeakGcAllocBytes;

        // Rendering
        public double AvgBatches, AvgDrawCalls, AvgSetPassCalls;
        public double AvgTriangles, AvgVertices;

        // GPU
        public double AvgCpuFrameTimeMs, AvgGpuFrameTimeMs;
        public double MaxCpuFrameTimeMs, MaxGpuFrameTimeMs;
        public double P95CpuFrameTimeMs, P95GpuFrameTimeMs;

        // URP passes (pass name → avg CPU ms, avg GPU ms)
        public List<UrpPassEntry> AvgUrpPasses = new List<UrpPassEntry>();

        // Bottleneck
        public int CpuBoundFrames, GpuBoundFrames, PresentLimitedFrames, BalancedFrames, IndeterminateFrames;

        // Spikes (frames where PlayerLoop > 2x average)
        public int SpikeCount;
        public double SpikeThresholdMs;

        // FPS
        public double AvgFps, MinFps, MaxFps;
    }

    [Serializable]
    public class CaptureSession
    {
        public string DeviceName;
        public string DeviceModel;
        public string GraphicsDeviceName;
        public string OperatingSystem;
        public int SystemMemoryMB;
        public int GraphicsMemoryMB;
        public int ScreenWidth;
        public int ScreenHeight;
        public int TargetFrameRate;
        public string QualityLevel;
        public string CaptureTimeIso; // ISO 8601 string (DateTime not serializable by JsonUtility)
        public int RequestedFrameCount;
        public bool ProfilerWasRecording;

        // Profiler overhead estimate — captured before our collectors start
        public long ProfilerOverheadBytes;  // Profiler's own memory footprint
        public long ManagedHeapAtCaptureStart; // Heap before any collection

        public List<FrameSnapshot> Frames = new List<FrameSnapshot>();
        public SessionSummary Summary;
        public ProfilerHierarchyData ProfilerHierarchy;
        public MemoryBreakdownData MemoryBreakdown;

        public void PopulateSystemInfo()
        {
            DeviceName = SystemInfo.deviceName;
            DeviceModel = SystemInfo.deviceModel;
            GraphicsDeviceName = SystemInfo.graphicsDeviceName;
            OperatingSystem = SystemInfo.operatingSystem;
            SystemMemoryMB = SystemInfo.systemMemorySize;
            GraphicsMemoryMB = SystemInfo.graphicsMemorySize;
            ScreenWidth = Screen.width;
            ScreenHeight = Screen.height;
            TargetFrameRate = Application.targetFrameRate;
            QualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()];
            CaptureTimeIso = DateTime.Now.ToString("o");

            // Capture profiler overhead baseline before collectors start
            ProfilerWasRecording = UnityEngine.Profiling.Profiler.enabled;
            ManagedHeapAtCaptureStart = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong();

            // Estimate profiler's own memory overhead:
            // Total allocated - total reserved gives us a rough native overhead,
            // but the profiler buffer itself is the main contributor.
            // Profiler.GetAllocatedMemoryForGraphicsDriver() + profiler buffer are
            // not directly queryable, so we note the total and let the AI discount it.
            ProfilerOverheadBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()
                                  - UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong();
        }

        public SessionSummary ComputeSummary()
        {
            var s = new SessionSummary();
            if (Frames.Count == 0)
            {
                Summary = s;
                return s;
            }

            // CPU timing
            var cpuFrames = Frames.Where(f => f.Cpu.WasCollected).ToList();
            if (cpuFrames.Count > 0)
            {
                var playerLoops = cpuFrames.Select(f => f.Cpu.PlayerLoopMs).OrderBy(v => v).ToList();
                s.AvgPlayerLoopMs = playerLoops.Average();
                s.MinPlayerLoopMs = playerLoops.First();
                s.MaxPlayerLoopMs = playerLoops.Last();
                s.P95PlayerLoopMs = Percentile(playerLoops, 0.95);
                s.P99PlayerLoopMs = Percentile(playerLoops, 0.99);
                s.AvgScriptsMs = cpuFrames.Average(f => f.Cpu.ScriptsMs);
                s.AvgPhysicsMs = cpuFrames.Average(f => f.Cpu.PhysicsMs);
                s.AvgRenderingMs = cpuFrames.Average(f => f.Cpu.RenderingMs);
                s.AvgAnimationMs = cpuFrames.Average(f => f.Cpu.AnimationMs);
                s.AvgGcCollectMs = cpuFrames.Average(f => f.Cpu.GcCollectMs);

                // Spike detection: frames > 2x average
                s.SpikeThresholdMs = s.AvgPlayerLoopMs * 2.0;
                s.SpikeCount = playerLoops.Count(v => v > s.SpikeThresholdMs);
            }

            // Memory
            var memFrames = Frames.Where(f => f.Memory.WasCollected).ToList();
            if (memFrames.Count > 0)
            {
                s.AvgGcAllocBytes = (long)memFrames.Average(f => f.Memory.GcAllocBytes);
                s.AvgGcAllocCount = (int)Math.Round(memFrames.Average(f => f.Memory.GcAllocCount));
                s.StartManagedHeap = memFrames.First().Memory.ManagedHeapBytes;
                s.EndManagedHeap = memFrames.Last().Memory.ManagedHeapBytes;
                s.PeakGcAllocBytes = memFrames.Max(f => f.Memory.GcAllocBytes);
            }

            // Rendering
            var renderFrames = Frames.Where(f => f.Rendering.WasCollected).ToList();
            if (renderFrames.Count > 0)
            {
                s.AvgBatches = renderFrames.Average(f => f.Rendering.Batches);
                s.AvgDrawCalls = renderFrames.Average(f => f.Rendering.DrawCalls);
                s.AvgSetPassCalls = renderFrames.Average(f => f.Rendering.SetPassCalls);
                s.AvgTriangles = renderFrames.Average(f => f.Rendering.Triangles);
                s.AvgVertices = renderFrames.Average(f => f.Rendering.Vertices);
            }

            // GPU timing
            var gpuFrames = Frames.Where(f => f.Gpu.WasCollected).ToList();
            if (gpuFrames.Count > 0)
            {
                var cpuTimes = gpuFrames.Select(f => f.Gpu.CpuFrameTimeMs).OrderBy(v => v).ToList();
                var gpuTimes = gpuFrames.Select(f => f.Gpu.GpuFrameTimeMs).OrderBy(v => v).ToList();
                s.AvgCpuFrameTimeMs = cpuTimes.Average();
                s.AvgGpuFrameTimeMs = gpuTimes.Average();
                s.MaxCpuFrameTimeMs = cpuTimes.Last();
                s.MaxGpuFrameTimeMs = gpuTimes.Last();
                s.P95CpuFrameTimeMs = Percentile(cpuTimes, 0.95);
                s.P95GpuFrameTimeMs = Percentile(gpuTimes, 0.95);
            }

            // FPS from GPU frame timing (prefer) or CPU PlayerLoop
            if (gpuFrames.Count > 0 && s.AvgCpuFrameTimeMs > 0)
            {
                var frameTimes = gpuFrames
                    .Select(f => Math.Max(f.Gpu.CpuFrameTimeMs, f.Gpu.GpuFrameTimeMs))
                    .Where(t => t > 0).ToList();
                if (frameTimes.Count > 0)
                {
                    s.AvgFps = 1000.0 / frameTimes.Average();
                    s.MaxFps = 1000.0 / frameTimes.Min();
                    s.MinFps = 1000.0 / frameTimes.Max();
                }
            }
            else if (cpuFrames.Count > 0 && s.AvgPlayerLoopMs > 0)
            {
                var loopTimes = cpuFrames.Select(f => f.Cpu.PlayerLoopMs).Where(t => t > 0).ToList();
                if (loopTimes.Count > 0)
                {
                    s.AvgFps = 1000.0 / loopTimes.Average();
                    s.MaxFps = 1000.0 / loopTimes.Min();
                    s.MinFps = 1000.0 / loopTimes.Max();
                }
            }

            // URP pass averages
            var urpFrames = Frames.Where(f => f.UrpPasses.WasCollected && f.UrpPasses.Passes != null).ToList();
            if (urpFrames.Count > 0)
            {
                var passNames = urpFrames
                    .SelectMany(f => f.UrpPasses.Passes)
                    .Select(p => p.PassName)
                    .Distinct()
                    .ToList();

                foreach (var name in passNames)
                {
                    var entries = urpFrames
                        .SelectMany(f => f.UrpPasses.Passes)
                        .Where(p => p.PassName == name)
                        .ToList();

                    s.AvgUrpPasses.Add(new UrpPassEntry
                    {
                        PassName = name,
                        CpuMs = entries.Average(e => e.CpuMs),
                        GpuMs = entries.Average(e => e.GpuMs)
                    });
                }

                // Sort by GPU time descending
                s.AvgUrpPasses.Sort((a, b) => b.GpuMs.CompareTo(a.GpuMs));
            }

            // Bottleneck classification
            var bnFrames = Frames.Where(f => f.Bottleneck.WasCollected).ToList();
            foreach (var f in bnFrames)
            {
                switch (f.Bottleneck.Bottleneck)
                {
                    case BottleneckType.CPU: s.CpuBoundFrames++; break;
                    case BottleneckType.GPU: s.GpuBoundFrames++; break;
                    case BottleneckType.PresentLimited: s.PresentLimitedFrames++; break;
                    case BottleneckType.Balanced: s.BalancedFrames++; break;
                    default: s.IndeterminateFrames++; break;
                }
            }

            Summary = s;
            return s;
        }

        static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            if (sorted.Count == 1) return sorted[0];
            double index = (sorted.Count - 1) * p;
            int lower = (int)Math.Floor(index);
            int upper = lower + 1;
            if (upper >= sorted.Count) return sorted[sorted.Count - 1];
            double weight = index - lower;
            return sorted[lower] * (1.0 - weight) + sorted[upper] * weight;
        }
    }
}
