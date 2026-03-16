using FrameAnalyzer.Runtime.Data;
using UnityEngine;

namespace FrameAnalyzer.Runtime.Collectors
{
    /// <summary>
    /// Captures GPU/CPU frame timing via FrameTimingManager.
    /// Also populates BottleneckData to avoid duplicate CaptureFrameTimings() calls.
    /// </summary>
    public class GpuTimingCollector : IFrameDataCollector
    {
        private readonly FrameTiming[] _timings = new FrameTiming[1];

        // Thresholds matching SRP Core's classification logic
        private const double BalancedThresholdRatio = 0.15;

        public void Begin() { }

        public void Collect(FrameSnapshot snapshot)
        {
            FrameTimingManager.CaptureFrameTimings();
            uint count = FrameTimingManager.GetLatestTimings(1, _timings);

            if (count > 0)
            {
                double cpuMs = _timings[0].cpuFrameTime;
                double gpuMs = _timings[0].gpuFrameTime;

                snapshot.Gpu = new GpuTimingData
                {
                    WasCollected = true,
                    CpuFrameTimeMs = cpuMs,
                    GpuFrameTimeMs = gpuMs,
                    CpuMainThreadMs = _timings[0].cpuMainThreadFrameTime,
                    CpuRenderThreadMs = _timings[0].cpuRenderThreadFrameTime
                };

                // Also classify bottleneck from the same timing data (avoids duplicate capture)
                snapshot.Bottleneck = new BottleneckData
                {
                    WasCollected = true,
                    CpuFrameTimeMs = cpuMs,
                    GpuFrameTimeMs = gpuMs,
                    Bottleneck = Classify(cpuMs, gpuMs)
                };
            }
            else
            {
                // No timing data available — mark both as not collected
                snapshot.Gpu = default;
                snapshot.Bottleneck = default;
            }
        }

        public void End() { }

        static BottleneckType Classify(double cpuMs, double gpuMs)
        {
            if (cpuMs <= 0 && gpuMs <= 0)
                return BottleneckType.Indeterminate;
            if (gpuMs <= 0) return BottleneckType.CPU;
            if (cpuMs <= 0) return BottleneckType.GPU;

            double maxTime = System.Math.Max(cpuMs, gpuMs);
            int refreshRate = Screen.currentResolution.refreshRate;
            double targetMs = Application.targetFrameRate > 0
                ? 1000.0 / Application.targetFrameRate
                : refreshRate > 0 ? 1000.0 / refreshRate : 16.667;

            if (maxTime < targetMs * 0.7)
                return BottleneckType.PresentLimited;

            double ratio = System.Math.Abs(cpuMs - gpuMs) / System.Math.Max(cpuMs, gpuMs);
            if (ratio < BalancedThresholdRatio)
                return BottleneckType.Balanced;

            return cpuMs > gpuMs ? BottleneckType.CPU : BottleneckType.GPU;
        }
    }
}
