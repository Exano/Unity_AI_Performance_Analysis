using System.Collections.Generic;
using NUnit.Framework;
using FrameAnalyzer.Runtime.Data;
using FrameAnalyzer.Runtime.Serialization;

namespace FrameAnalyzer.Runtime.Tests
{
    public class SessionSerializerTests
    {
        [Test]
        public void ToCsv_HasHeaderRow()
        {
            var session = MakeSession();
            var csv = SessionSerializer.ToCsv(session);
            var lines = csv.Split('\n');

            Assert.IsTrue(lines[0].StartsWith("Frame,"));
            Assert.IsTrue(lines[0].Contains("PlayerLoopMs"));
            Assert.IsTrue(lines[0].Contains("GpuFrameTimeMs"));
            Assert.IsTrue(lines[0].Contains("Bottleneck"));
        }

        [Test]
        public void ToCsv_CorrectRowCount()
        {
            var session = MakeSession();
            var csv = SessionSerializer.ToCsv(session);
            var lines = csv.Trim().Split('\n');

            // Header + 2 data rows
            Assert.AreEqual(3, lines.Length);
        }

        [Test]
        public void ToCsv_UncollectedData_ShowsDefaultValues()
        {
            var session = new CaptureSession();
            var frame = new FrameSnapshot { FrameIndex = 0 };
            // Nothing collected
            session.Frames.Add(frame);
            var csv = SessionSerializer.ToCsv(session);

            // Bottleneck should show N/A when not collected
            Assert.IsTrue(csv.Contains("N/A"));
            // Numeric fields should show 0
            Assert.IsTrue(csv.Contains("0.000,0.000"));
        }

        [Test]
        public void ToAnalysisPrompt_ContainsSystemInfo()
        {
            var session = MakeSession();
            session.DeviceName = "TestPC";
            session.GraphicsDeviceName = "TestGPU";
            var prompt = SessionSerializer.ToAnalysisPrompt(session);

            Assert.IsTrue(prompt.Contains("## System Info"));
            Assert.IsTrue(prompt.Contains("TestPC"));
            Assert.IsTrue(prompt.Contains("TestGPU"));
        }

        [Test]
        public void ToAnalysisPrompt_ContainsSummaryTable()
        {
            var session = MakeSession();
            var prompt = SessionSerializer.ToAnalysisPrompt(session);

            Assert.IsTrue(prompt.Contains("## Performance Summary"));
            Assert.IsTrue(prompt.Contains("### CPU Timing"));
            Assert.IsTrue(prompt.Contains("PlayerLoop"));
        }

        [Test]
        public void ToAnalysisPrompt_ContainsCsv()
        {
            var session = MakeSession();
            var prompt = SessionSerializer.ToAnalysisPrompt(session);

            Assert.IsTrue(prompt.Contains("```csv"));
            Assert.IsTrue(prompt.Contains("### Per-Frame Data (CSV)"));
        }

        [Test]
        public void ToAnalysisPrompt_ContainsUrpBreakdown()
        {
            var session = MakeSessionWithUrp();
            var prompt = SessionSerializer.ToAnalysisPrompt(session);

            Assert.IsTrue(prompt.Contains("### URP Render Pass Breakdown"));
            Assert.IsTrue(prompt.Contains("DrawOpaqueObjects"));
            Assert.IsTrue(prompt.Contains("Bloom"));
        }

        [Test]
        public void ToAnalysisPrompt_OmitsUncollectedSections()
        {
            var session = new CaptureSession();
            session.Frames.Add(new FrameSnapshot
            {
                FrameIndex = 0,
                Cpu = new CpuTimingData { WasCollected = true, PlayerLoopMs = 16.0 }
            });
            var prompt = SessionSerializer.ToAnalysisPrompt(session);

            // Should have CPU timing but NOT GPU, URP, Rendering, Memory sections
            Assert.IsTrue(prompt.Contains("### CPU Timing"));
            Assert.IsFalse(prompt.Contains("### GPU Timing"));
            Assert.IsFalse(prompt.Contains("### URP Render Pass"));
            Assert.IsFalse(prompt.Contains("### Rendering"));
            Assert.IsFalse(prompt.Contains("### Memory"));
        }

        [Test]
        public void ToAnalysisPrompt_IncludesSceneSnapshot()
        {
            var session = MakeSession();
            var sceneData = "Total GameObjects: 150\nRenderers: 45";
            var prompt = SessionSerializer.ToAnalysisPrompt(session, sceneData);

            Assert.IsTrue(prompt.Contains("## Scene Structure Analysis"));
            Assert.IsTrue(prompt.Contains("Total GameObjects: 150"));
        }

        [Test]
        public void ToAnalysisPrompt_OmitsSceneWhenNull()
        {
            var session = MakeSession();
            var prompt = SessionSerializer.ToAnalysisPrompt(session, null);

            Assert.IsFalse(prompt.Contains("## Scene Structure Analysis"));
        }

        [Test]
        public void ToAnalysisPrompt_ContainsBottleneckClassification()
        {
            var session = MakeSessionWithBottleneck();
            var prompt = SessionSerializer.ToAnalysisPrompt(session);

            Assert.IsTrue(prompt.Contains("### Bottleneck Classification"));
            Assert.IsTrue(prompt.Contains("GPU"));
        }

        [Test]
        public void ToJson_RoundTrips()
        {
            var session = MakeSession();
            session.DeviceName = "TestPC";
            var json = SessionSerializer.ToJson(session);
            var restored = SessionSerializer.FromJson(json);

            Assert.AreEqual("TestPC", restored.DeviceName);
            Assert.AreEqual(2, restored.Frames.Count);
            Assert.AreEqual(16.6, restored.Frames[0].Cpu.PlayerLoopMs, 0.01);
        }

        // ── Helpers ──

        static CaptureSession MakeSession()
        {
            var session = new CaptureSession
            {
                DeviceName = "PC",
                GraphicsDeviceName = "GPU",
                OperatingSystem = "Win10",
                QualityLevel = "High"
            };
            session.Frames.Add(new FrameSnapshot
            {
                FrameIndex = 0,
                Cpu = new CpuTimingData
                {
                    WasCollected = true, PlayerLoopMs = 16.6, ScriptsMs = 5.0,
                    PhysicsMs = 2.0, RenderingMs = 8.0, AnimationMs = 1.0
                },
                Memory = new MemoryData
                {
                    WasCollected = true, ManagedHeapBytes = 1000000,
                    GcAllocBytes = 512, GcAllocCount = 3
                },
                Gpu = new GpuTimingData
                {
                    WasCollected = true, CpuFrameTimeMs = 8.0, GpuFrameTimeMs = 12.0
                }
            });
            session.Frames.Add(new FrameSnapshot
            {
                FrameIndex = 1,
                Cpu = new CpuTimingData
                {
                    WasCollected = true, PlayerLoopMs = 18.0, ScriptsMs = 6.0,
                    PhysicsMs = 2.5, RenderingMs = 8.5, AnimationMs = 1.0
                },
                Memory = new MemoryData
                {
                    WasCollected = true, ManagedHeapBytes = 1050000,
                    GcAllocBytes = 1024, GcAllocCount = 5
                },
                Gpu = new GpuTimingData
                {
                    WasCollected = true, CpuFrameTimeMs = 9.0, GpuFrameTimeMs = 13.0
                }
            });
            return session;
        }

        static CaptureSession MakeSessionWithUrp()
        {
            var session = MakeSession();
            foreach (var f in session.Frames)
            {
                f.UrpPasses = UrpPassTimingData.Create();
                f.UrpPasses.WasCollected = true;
                f.UrpPasses.Passes.Add(new UrpPassEntry { PassName = "DrawOpaqueObjects", CpuMs = 3.0, GpuMs = 6.0 });
                f.UrpPasses.Passes.Add(new UrpPassEntry { PassName = "Bloom", CpuMs = 1.0, GpuMs = 2.0 });
            }
            return session;
        }

        static CaptureSession MakeSessionWithBottleneck()
        {
            var session = MakeSession();
            session.Frames[0].Bottleneck = new BottleneckData { WasCollected = true, Bottleneck = BottleneckType.GPU };
            session.Frames[1].Bottleneck = new BottleneckData { WasCollected = true, Bottleneck = BottleneckType.GPU };
            return session;
        }
    }
}
