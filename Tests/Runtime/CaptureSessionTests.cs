using System.Collections.Generic;
using NUnit.Framework;
using FrameAnalyzer.Runtime.Data;

namespace FrameAnalyzer.Runtime.Tests
{
    public class CaptureSessionTests
    {
        [Test]
        public void ComputeSummary_EmptyFrames_ReturnsDefaults()
        {
            var session = new CaptureSession();
            var s = session.ComputeSummary();

            Assert.AreEqual(0, s.AvgPlayerLoopMs);
            Assert.AreEqual(0, s.SpikeCount);
            Assert.AreEqual(0, s.CpuBoundFrames);
            Assert.AreEqual(0, s.GpuBoundFrames);
            Assert.IsNotNull(s.AvgUrpPasses);
            Assert.AreEqual(0, s.AvgUrpPasses.Count);
        }

        [Test]
        public void ComputeSummary_SingleFrame_MinMaxAvgEqual()
        {
            var session = new CaptureSession();
            session.Frames.Add(MakeCpuFrame(0, 16.6));
            var s = session.ComputeSummary();

            Assert.AreEqual(16.6, s.AvgPlayerLoopMs, 0.001);
            Assert.AreEqual(16.6, s.MinPlayerLoopMs, 0.001);
            Assert.AreEqual(16.6, s.MaxPlayerLoopMs, 0.001);
            Assert.AreEqual(16.6, s.P95PlayerLoopMs, 0.001);
            Assert.AreEqual(16.6, s.P99PlayerLoopMs, 0.001);
        }

        [Test]
        public void ComputeSummary_MultipleFrames_CorrectAvgMinMax()
        {
            var session = new CaptureSession();
            session.Frames.Add(MakeCpuFrame(0, 10.0));
            session.Frames.Add(MakeCpuFrame(1, 20.0));
            session.Frames.Add(MakeCpuFrame(2, 30.0));
            var s = session.ComputeSummary();

            Assert.AreEqual(20.0, s.AvgPlayerLoopMs, 0.001);
            Assert.AreEqual(10.0, s.MinPlayerLoopMs, 0.001);
            Assert.AreEqual(30.0, s.MaxPlayerLoopMs, 0.001);
        }

        [Test]
        public void ComputeSummary_P95_CorrectPercentile()
        {
            var session = new CaptureSession();
            // 20 frames: 19 at 10ms, 1 spike at 100ms
            for (int i = 0; i < 19; i++)
                session.Frames.Add(MakeCpuFrame(i, 10.0));
            session.Frames.Add(MakeCpuFrame(19, 100.0));
            var s = session.ComputeSummary();

            // P95 should be close to the spike for 20 samples
            Assert.Greater(s.P95PlayerLoopMs, 10.0);
            Assert.LessOrEqual(s.P95PlayerLoopMs, 100.0);
            // P99 should be even closer to the spike
            Assert.GreaterOrEqual(s.P99PlayerLoopMs, s.P95PlayerLoopMs);
        }

        [Test]
        public void ComputeSummary_SpikeDetection_CountsCorrectly()
        {
            var session = new CaptureSession();
            // 8 frames at 10ms, 2 spikes at 25ms (> 2x avg of ~13ms)
            for (int i = 0; i < 8; i++)
                session.Frames.Add(MakeCpuFrame(i, 10.0));
            session.Frames.Add(MakeCpuFrame(8, 25.0));
            session.Frames.Add(MakeCpuFrame(9, 25.0));
            var s = session.ComputeSummary();

            // Avg = (80+50)/10 = 13ms, threshold = 26ms → 0 spikes (25 < 26)
            Assert.AreEqual(0, s.SpikeCount);

            // Now add a clear spike
            session.Frames.Add(MakeCpuFrame(10, 100.0));
            s = session.ComputeSummary();
            // Avg is now higher but 100ms is clearly > 2x avg
            Assert.GreaterOrEqual(s.SpikeCount, 1);
        }

        [Test]
        public void ComputeSummary_Memory_TracksHeapGrowth()
        {
            var session = new CaptureSession();
            session.Frames.Add(MakeMemoryFrame(0, heapBytes: 1000000, gcAllocBytes: 512, gcAllocCount: 3));
            session.Frames.Add(MakeMemoryFrame(1, heapBytes: 1100000, gcAllocBytes: 1024, gcAllocCount: 5));
            var s = session.ComputeSummary();

            Assert.AreEqual(1000000, s.StartManagedHeap);
            Assert.AreEqual(1100000, s.EndManagedHeap);
            Assert.AreEqual(768, s.AvgGcAllocBytes); // (512+1024)/2
            Assert.AreEqual(4, s.AvgGcAllocCount);   // (3+5)/2
            Assert.AreEqual(1024, s.PeakGcAllocBytes);
        }

        [Test]
        public void ComputeSummary_GpuTiming_CorrectAverages()
        {
            var session = new CaptureSession();
            session.Frames.Add(MakeGpuFrame(0, cpuMs: 8.0, gpuMs: 12.0));
            session.Frames.Add(MakeGpuFrame(1, cpuMs: 10.0, gpuMs: 14.0));
            var s = session.ComputeSummary();

            Assert.AreEqual(9.0, s.AvgCpuFrameTimeMs, 0.001);
            Assert.AreEqual(13.0, s.AvgGpuFrameTimeMs, 0.001);
            Assert.AreEqual(10.0, s.MaxCpuFrameTimeMs, 0.001);
            Assert.AreEqual(14.0, s.MaxGpuFrameTimeMs, 0.001);
        }

        [Test]
        public void ComputeSummary_UrpPasses_AveragesCorrectly()
        {
            var session = new CaptureSession();
            session.Frames.Add(MakeUrpFrame(0,
                ("DrawOpaqueObjects", 2.0, 5.0),
                ("Bloom", 0.5, 3.0)));
            session.Frames.Add(MakeUrpFrame(1,
                ("DrawOpaqueObjects", 4.0, 7.0),
                ("Bloom", 1.5, 1.0)));
            var s = session.ComputeSummary();

            Assert.AreEqual(2, s.AvgUrpPasses.Count);
            // Sorted by GPU time descending
            var opaques = s.AvgUrpPasses.Find(p => p.PassName == "DrawOpaqueObjects");
            Assert.AreEqual(3.0, opaques.CpuMs, 0.001);  // (2+4)/2
            Assert.AreEqual(6.0, opaques.GpuMs, 0.001);  // (5+7)/2

            var bloom = s.AvgUrpPasses.Find(p => p.PassName == "Bloom");
            Assert.AreEqual(1.0, bloom.CpuMs, 0.001);
            Assert.AreEqual(2.0, bloom.GpuMs, 0.001);
        }

        [Test]
        public void ComputeSummary_Bottleneck_CountsCorrectly()
        {
            var session = new CaptureSession();
            session.Frames.Add(MakeBottleneckFrame(0, BottleneckType.GPU));
            session.Frames.Add(MakeBottleneckFrame(1, BottleneckType.GPU));
            session.Frames.Add(MakeBottleneckFrame(2, BottleneckType.CPU));
            session.Frames.Add(MakeBottleneckFrame(3, BottleneckType.Balanced));
            session.Frames.Add(MakeBottleneckFrame(4, BottleneckType.PresentLimited));
            var s = session.ComputeSummary();

            Assert.AreEqual(1, s.CpuBoundFrames);
            Assert.AreEqual(2, s.GpuBoundFrames);
            Assert.AreEqual(1, s.BalancedFrames);
            Assert.AreEqual(1, s.PresentLimitedFrames);
        }

        [Test]
        public void ComputeSummary_Fps_ComputedFromGpuTiming()
        {
            var session = new CaptureSession();
            // CPU 8ms, GPU 16ms → frame limited by GPU → ~62.5 FPS
            session.Frames.Add(MakeGpuFrame(0, cpuMs: 8.0, gpuMs: 16.0));
            session.Frames.Add(MakeGpuFrame(1, cpuMs: 8.0, gpuMs: 16.0));
            var s = session.ComputeSummary();

            Assert.AreEqual(62.5, s.AvgFps, 0.5);
        }

        [Test]
        public void ComputeSummary_UncollectedData_SkippedGracefully()
        {
            var session = new CaptureSession();
            // Frame with only CPU data collected, nothing else
            var frame = new FrameSnapshot { FrameIndex = 0 };
            frame.Cpu = new CpuTimingData { WasCollected = true, PlayerLoopMs = 16.6 };
            // Memory, Rendering, GPU, URP, Bottleneck all WasCollected = false
            session.Frames.Add(frame);
            var s = session.ComputeSummary();

            Assert.AreEqual(16.6, s.AvgPlayerLoopMs, 0.001);
            Assert.AreEqual(0, s.AvgGcAllocBytes);
            Assert.AreEqual(0, s.AvgBatches);
            Assert.AreEqual(0, s.AvgGpuFrameTimeMs);
            Assert.AreEqual(0, s.AvgUrpPasses.Count);
        }

        [Test]
        public void ComputeSummary_Rendering_CorrectAverages()
        {
            var session = new CaptureSession();
            session.Frames.Add(MakeRenderingFrame(0, batches: 100, draws: 200, setPasses: 50, tris: 50000, verts: 30000));
            session.Frames.Add(MakeRenderingFrame(1, batches: 200, draws: 300, setPasses: 70, tris: 70000, verts: 40000));
            var s = session.ComputeSummary();

            Assert.AreEqual(150, s.AvgBatches, 0.001);
            Assert.AreEqual(250, s.AvgDrawCalls, 0.001);
            Assert.AreEqual(60, s.AvgSetPassCalls, 0.001);
            Assert.AreEqual(60000, s.AvgTriangles, 0.001);
            Assert.AreEqual(35000, s.AvgVertices, 0.001);
        }

        // ── Helpers ──

        static FrameSnapshot MakeCpuFrame(int idx, double playerLoopMs)
        {
            return new FrameSnapshot
            {
                FrameIndex = idx,
                Cpu = new CpuTimingData
                {
                    WasCollected = true,
                    PlayerLoopMs = playerLoopMs,
                    ScriptsMs = playerLoopMs * 0.3,
                    PhysicsMs = playerLoopMs * 0.1,
                    RenderingMs = playerLoopMs * 0.4,
                    AnimationMs = playerLoopMs * 0.05
                }
            };
        }

        static FrameSnapshot MakeMemoryFrame(int idx, long heapBytes, long gcAllocBytes, int gcAllocCount)
        {
            return new FrameSnapshot
            {
                FrameIndex = idx,
                Memory = new MemoryData
                {
                    WasCollected = true,
                    ManagedHeapBytes = heapBytes,
                    GcAllocBytes = gcAllocBytes,
                    GcAllocCount = gcAllocCount
                }
            };
        }

        static FrameSnapshot MakeGpuFrame(int idx, double cpuMs, double gpuMs)
        {
            return new FrameSnapshot
            {
                FrameIndex = idx,
                Gpu = new GpuTimingData
                {
                    WasCollected = true,
                    CpuFrameTimeMs = cpuMs,
                    GpuFrameTimeMs = gpuMs,
                    CpuMainThreadMs = cpuMs,
                    CpuRenderThreadMs = cpuMs * 0.3
                }
            };
        }

        static FrameSnapshot MakeUrpFrame(int idx, params (string name, double cpuMs, double gpuMs)[] passes)
        {
            var frame = new FrameSnapshot { FrameIndex = idx };
            frame.UrpPasses.WasCollected = true;
            foreach (var (name, cpuMs, gpuMs) in passes)
                frame.UrpPasses.Passes.Add(new UrpPassEntry { PassName = name, CpuMs = cpuMs, GpuMs = gpuMs });
            return frame;
        }

        static FrameSnapshot MakeBottleneckFrame(int idx, BottleneckType type)
        {
            return new FrameSnapshot
            {
                FrameIndex = idx,
                Bottleneck = new BottleneckData { WasCollected = true, Bottleneck = type }
            };
        }

        static FrameSnapshot MakeRenderingFrame(int idx, int batches, int draws, int setPasses, int tris, int verts)
        {
            return new FrameSnapshot
            {
                FrameIndex = idx,
                Rendering = new RenderingData
                {
                    WasCollected = true,
                    Batches = batches,
                    DrawCalls = draws,
                    SetPassCalls = setPasses,
                    Triangles = tris,
                    Vertices = verts
                }
            };
        }
    }
}
