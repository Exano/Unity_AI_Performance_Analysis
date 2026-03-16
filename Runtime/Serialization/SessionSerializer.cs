using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FrameAnalyzer.Runtime.Data;
using UnityEngine;

namespace FrameAnalyzer.Runtime.Serialization
{
    public static class SessionSerializer
    {
        // Use invariant culture for all numeric formatting to avoid comma-as-decimal issues
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static string ToJson(CaptureSession session)
        {
            return JsonUtility.ToJson(session, true);
        }

        public static CaptureSession FromJson(string json)
        {
            return JsonUtility.FromJson<CaptureSession>(json);
        }

        public static string ToCsv(CaptureSession session)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Frame,PlayerLoopMs,UpdateMs,LateUpdateMs,FixedUpdateMs,ScriptsMs,PhysicsMs,RenderingMs,AnimationMs,GcCollectMs," +
                          "GcAllocBytes,GcAllocCount,ManagedHeapBytes,ManagedUsedBytes,NativeMemoryBytes," +
                          "CpuFrameTimeMs,GpuFrameTimeMs," +
                          "Batches,DrawCalls,SetPassCalls,Triangles,Vertices," +
                          "Bottleneck");

            foreach (var f in session.Frames)
            {
                sb.Append(f.FrameIndex).Append(',');
                sb.Append(Fmt(f.Cpu.WasCollected ? f.Cpu.PlayerLoopMs : 0)).Append(',');
                sb.Append(Fmt(f.Cpu.WasCollected ? f.Cpu.UpdateMs : 0)).Append(',');
                sb.Append(Fmt(f.Cpu.WasCollected ? f.Cpu.LateUpdateMs : 0)).Append(',');
                sb.Append(Fmt(f.Cpu.WasCollected ? f.Cpu.FixedUpdateMs : 0)).Append(',');
                sb.Append(Fmt(f.Cpu.WasCollected ? f.Cpu.ScriptsMs : 0)).Append(',');
                sb.Append(Fmt(f.Cpu.WasCollected ? f.Cpu.PhysicsMs : 0)).Append(',');
                sb.Append(Fmt(f.Cpu.WasCollected ? f.Cpu.RenderingMs : 0)).Append(',');
                sb.Append(Fmt(f.Cpu.WasCollected ? f.Cpu.AnimationMs : 0)).Append(',');
                sb.Append(Fmt(f.Cpu.WasCollected ? f.Cpu.GcCollectMs : 0)).Append(',');
                sb.Append(f.Memory.WasCollected ? f.Memory.GcAllocBytes : 0).Append(',');
                sb.Append(f.Memory.WasCollected ? f.Memory.GcAllocCount : 0).Append(',');
                sb.Append(f.Memory.WasCollected ? f.Memory.ManagedHeapBytes : 0).Append(',');
                sb.Append(f.Memory.WasCollected ? f.Memory.ManagedUsedBytes : 0).Append(',');
                sb.Append(f.Memory.WasCollected ? f.Memory.NativeMemoryBytes : 0).Append(',');
                sb.Append(Fmt(f.Gpu.WasCollected ? f.Gpu.CpuFrameTimeMs : 0)).Append(',');
                sb.Append(Fmt(f.Gpu.WasCollected ? f.Gpu.GpuFrameTimeMs : 0)).Append(',');
                sb.Append(f.Rendering.WasCollected ? f.Rendering.Batches : 0).Append(',');
                sb.Append(f.Rendering.WasCollected ? f.Rendering.DrawCalls : 0).Append(',');
                sb.Append(f.Rendering.WasCollected ? f.Rendering.SetPassCalls : 0).Append(',');
                sb.Append(f.Rendering.WasCollected ? f.Rendering.Triangles : 0).Append(',');
                sb.Append(f.Rendering.WasCollected ? f.Rendering.Vertices : 0).Append(',');
                sb.Append(f.Bottleneck.WasCollected ? f.Bottleneck.Bottleneck.ToString() : "N/A");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static string ToAnalysisPrompt(CaptureSession session, string sceneSnapshot = null)
        {
            if (session.Summary == null)
                session.ComputeSummary();
            var s = session.Summary;
            var sb = new StringBuilder();

            // System info
            sb.AppendLine("## System Info");
            sb.AppendLine($"- Device: {session.DeviceName} ({session.DeviceModel})");
            sb.AppendLine($"- GPU: {session.GraphicsDeviceName}");
            sb.AppendLine($"- OS: {session.OperatingSystem}");
            sb.AppendLine($"- RAM: {session.SystemMemoryMB} MB / VRAM: {session.GraphicsMemoryMB} MB");
            sb.AppendLine($"- Resolution: {session.ScreenWidth}x{session.ScreenHeight}");
            sb.AppendLine($"- Quality: {session.QualityLevel}");
            sb.AppendLine($"- Target FPS: {(session.TargetFrameRate > 0 ? session.TargetFrameRate.ToString() : "Unlimited")}");
            sb.AppendLine($"- Frames captured: {session.Frames.Count}");
            sb.AppendLine($"- Profiler was recording: {session.ProfilerWasRecording}");
            sb.AppendLine($"- Managed heap at capture start: {FormatBytes(session.ManagedHeapAtCaptureStart)}");
            if (session.ProfilerWasRecording)
                sb.AppendLine("- **NOTE**: The Unity Profiler was active during capture. It adds its own memory overhead (profiler buffer, frame data history, marker storage) and minor CPU overhead. Managed heap figures include profiler allocations. Treat absolute memory numbers as an upper bound — real device usage will be lower. CPU timing is less affected but frame times may be slightly inflated.");
            sb.AppendLine();

            // Summary table
            sb.AppendLine("## Performance Summary");
            sb.AppendLine();

            // FPS
            if (s.AvgFps > 0)
            {
                sb.AppendLine("| Metric | Avg | Min | Max |");
                sb.AppendLine("|--------|-----|-----|-----|");
                sb.AppendLine(string.Format(Inv, "| FPS | {0:F1} | {1:F1} | {2:F1} |", s.AvgFps, s.MinFps, s.MaxFps));
                sb.AppendLine();
            }

            // CPU timing
            if (s.AvgPlayerLoopMs > 0)
            {
                sb.AppendLine("### CPU Timing (ms)");
                sb.AppendLine("| Metric | Avg | Min | Max | P95 | P99 |");
                sb.AppendLine("|--------|-----|-----|-----|-----|-----|");
                sb.AppendLine(string.Format(Inv, "| PlayerLoop | {0:F2} | {1:F2} | {2:F2} | {3:F2} | {4:F2} |",
                    s.AvgPlayerLoopMs, s.MinPlayerLoopMs, s.MaxPlayerLoopMs, s.P95PlayerLoopMs, s.P99PlayerLoopMs));
                sb.AppendLine(string.Format(Inv, "| Scripts | {0:F2} | | | | |", s.AvgScriptsMs));
                sb.AppendLine(string.Format(Inv, "| Physics | {0:F2} | | | | |", s.AvgPhysicsMs));
                sb.AppendLine(string.Format(Inv, "| Rendering | {0:F2} | | | | |", s.AvgRenderingMs));
                sb.AppendLine(string.Format(Inv, "| Animation | {0:F2} | | | | |", s.AvgAnimationMs));
                sb.AppendLine(string.Format(Inv, "| GC.Collect | {0:F2} | | | | |", s.AvgGcCollectMs));
                if (s.SpikeCount > 0)
                    sb.AppendLine(string.Format(Inv, "\nSpikes: {0} frames exceeded {1:F1}ms (2x avg)", s.SpikeCount, s.SpikeThresholdMs));
                sb.AppendLine();
            }

            // GPU timing
            if (s.AvgGpuFrameTimeMs > 0)
            {
                sb.AppendLine("### GPU Timing (ms)");
                sb.AppendLine("| Thread | Avg | Max | P95 |");
                sb.AppendLine("|--------|-----|-----|-----|");
                sb.AppendLine(string.Format(Inv, "| CPU Frame | {0:F2} | {1:F2} | {2:F2} |", s.AvgCpuFrameTimeMs, s.MaxCpuFrameTimeMs, s.P95CpuFrameTimeMs));
                sb.AppendLine(string.Format(Inv, "| GPU Frame | {0:F2} | {1:F2} | {2:F2} |", s.AvgGpuFrameTimeMs, s.MaxGpuFrameTimeMs, s.P95GpuFrameTimeMs));
                sb.AppendLine();
            }

            // Memory
            if (s.AvgGcAllocBytes > 0 || s.StartManagedHeap > 0)
            {
                sb.AppendLine("### Memory");
                sb.AppendLine($"- Avg GC Alloc/frame: {FormatBytes(s.AvgGcAllocBytes)} ({s.AvgGcAllocCount} allocations)");
                sb.AppendLine($"- Peak GC Alloc: {FormatBytes(s.PeakGcAllocBytes)}");
                sb.AppendLine($"- Managed Heap: {FormatBytes(s.StartManagedHeap)} \u2192 {FormatBytes(s.EndManagedHeap)}");
                long growth = s.EndManagedHeap - s.StartManagedHeap;
                if (growth > 0)
                    sb.AppendLine($"- Heap Growth: +{FormatBytes(growth)} (potential leak)");
                sb.AppendLine();
            }

            // Rendering stats
            if (s.AvgBatches > 0)
            {
                sb.AppendLine("### Rendering");
                sb.AppendLine(string.Format(Inv, "- Avg Batches: {0:F0}", s.AvgBatches));
                sb.AppendLine(string.Format(Inv, "- Avg Draw Calls: {0:F0}", s.AvgDrawCalls));
                sb.AppendLine(string.Format(Inv, "- Avg Set-Pass Calls: {0:F0}", s.AvgSetPassCalls));
                sb.AppendLine(string.Format(Inv, "- Avg Triangles: {0:F0}", s.AvgTriangles));
                sb.AppendLine(string.Format(Inv, "- Avg Vertices: {0:F0}", s.AvgVertices));
                sb.AppendLine();
            }

            // URP pass breakdown
            if (s.AvgUrpPasses.Count > 0)
            {
                sb.AppendLine("### URP Render Pass Breakdown (ms, sorted by GPU time)");
                sb.AppendLine("| Pass | CPU Avg | GPU Avg |");
                sb.AppendLine("|------|---------|---------|");
                foreach (var pass in s.AvgUrpPasses)
                    sb.AppendLine(string.Format(Inv, "| {0} | {1:F2} | {2:F2} |", pass.PassName, pass.CpuMs, pass.GpuMs));
                sb.AppendLine();
            }

            // Bottleneck
            int totalBn = s.CpuBoundFrames + s.GpuBoundFrames + s.PresentLimitedFrames + s.BalancedFrames + s.IndeterminateFrames;
            if (totalBn > 0)
            {
                sb.AppendLine("### Bottleneck Classification");
                sb.AppendLine("| Type | Frames | % |");
                sb.AppendLine("|------|--------|---|");
                AppendBottleneckRow(sb, "CPU", s.CpuBoundFrames, totalBn);
                AppendBottleneckRow(sb, "GPU", s.GpuBoundFrames, totalBn);
                AppendBottleneckRow(sb, "Present Limited", s.PresentLimitedFrames, totalBn);
                AppendBottleneckRow(sb, "Balanced", s.BalancedFrames, totalBn);
                AppendBottleneckRow(sb, "Indeterminate", s.IndeterminateFrames, totalBn);
                sb.AppendLine();
            }

            // Profiler hierarchy — top script markers with full statistics
            var ph = session.ProfilerHierarchy;
            if (ph != null && ph.WasCollected)
            {
                sb.AppendLine(string.Format(Inv, "_Analyzed {0} profiler frames._", ph.FramesAnalyzed));
                sb.AppendLine();

                if (ph.TopBySelfTime.Count > 0)
                {
                    sb.AppendLine("### Top Methods by Self Time (profiler hierarchy)");
                    sb.AppendLine("| Method | Median (ms) | Avg (ms) | Max (ms) | P95 (ms) | StdDev | Calls/frame | GC Alloc/frame | Frames |");
                    sb.AppendLine("|--------|-------------|----------|----------|----------|--------|-------------|----------------|--------|");
                    foreach (var m in ph.TopBySelfTime)
                    {
                        string presence = m.TotalFrames > 0
                            ? string.Format(Inv, "{0}/{1}", m.FrameCount, m.TotalFrames)
                            : m.FrameCount.ToString();
                        sb.AppendLine(string.Format(Inv,
                            "| {0} | {1:F3} | {2:F3} | {3:F3} | {4:F3} | {5:F3} | {6} | {7} | {8} |",
                            m.Name, m.MedianSelfMs, m.AvgSelfMs, m.MaxSelfMs, m.P95SelfMs,
                            m.StdDevSelfMs, m.MedianCalls, FormatBytes(m.MedianGcAllocBytes), presence));
                    }
                    sb.AppendLine();
                }

                if (ph.TopByGcAlloc.Count > 0)
                {
                    sb.AppendLine("### Top GC Allocators (profiler hierarchy)");
                    sb.AppendLine("| Method | Median Alloc | Avg Alloc | Max Alloc | Median Self (ms) | Calls/frame | Frames |");
                    sb.AppendLine("|--------|-------------|-----------|-----------|------------------|-------------|--------|");
                    foreach (var m in ph.TopByGcAlloc)
                    {
                        string presence = m.TotalFrames > 0
                            ? string.Format(Inv, "{0}/{1}", m.FrameCount, m.TotalFrames)
                            : m.FrameCount.ToString();
                        sb.AppendLine(string.Format(Inv,
                            "| {0} | {1} | {2} | {3} | {4:F3} | {5} | {6} |",
                            m.Name, FormatBytes(m.MedianGcAllocBytes), FormatBytes(m.AvgGcAllocBytes),
                            FormatBytes(m.MaxGcAllocBytes), m.MedianSelfMs, m.MedianCalls, presence));
                    }
                    sb.AppendLine();
                }

                if (!string.IsNullOrEmpty(ph.MemorySnapshotPath))
                {
                    sb.AppendLine($"A detailed memory snapshot was saved to: `{ph.MemorySnapshotPath}`");
                    sb.AppendLine("The user can open it in Window > Analysis > Memory Profiler for per-object inspection.");
                    sb.AppendLine();
                }
            }

            // Memory breakdown — what's actually loaded in memory
            var mb = session.MemoryBreakdown;
            if (mb != null && mb.WasCollected)
            {
                sb.AppendLine("### Loaded Asset Memory Breakdown");
                sb.AppendLine(string.Format(Inv, "Total tracked: {0}", FormatBytes(mb.TotalTrackedBytes)));
                sb.AppendLine();

                if (mb.ByCategory.Count > 0)
                {
                    sb.AppendLine("**By category:**");
                    sb.AppendLine("| Category | Count | Total Size |");
                    sb.AppendLine("|----------|-------|------------|");
                    foreach (var cat in mb.ByCategory)
                        sb.AppendLine(string.Format(Inv, "| {0} | {1} | {2} |",
                            cat.Category, cat.Count, FormatBytes(cat.TotalBytes)));
                    sb.AppendLine();
                }

                if (mb.TopAssets.Count > 0)
                {
                    sb.AppendLine("**Largest individual assets:**");
                    sb.AppendLine("| Asset | Type | Size |");
                    sb.AppendLine("|-------|------|------|");
                    foreach (var asset in mb.TopAssets)
                        sb.AppendLine(string.Format(Inv, "| {0} | {1} | {2} |",
                            asset.Name, asset.TypeName, FormatBytes(asset.SizeBytes)));
                    sb.AppendLine();
                }
            }

            // Per-frame CSV data
            sb.AppendLine("### Per-Frame Data (CSV)");
            sb.AppendLine("```csv");
            sb.Append(ToCsv(session));
            sb.AppendLine("```");

            // Scene snapshot
            if (!string.IsNullOrEmpty(sceneSnapshot))
            {
                sb.AppendLine();
                sb.AppendLine("## Scene Structure Analysis");
                sb.AppendLine(sceneSnapshot);
            }

            return sb.ToString();
        }

        static void AppendBottleneckRow(StringBuilder sb, string label, int count, int total)
        {
            if (count > 0)
                sb.AppendLine(string.Format(Inv, "| {0} | {1} | {2:F0}% |", label, count, 100.0 * count / total));
        }

        static string Fmt(double v) => v.ToString("F3", Inv);

        static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return string.Format(Inv, "{0} B", bytes);
            if (bytes < 1024 * 1024) return string.Format(Inv, "{0:F1} KB", bytes / 1024.0);
            return string.Format(Inv, "{0:F1} MB", bytes / (1024.0 * 1024.0));
        }
    }
}
