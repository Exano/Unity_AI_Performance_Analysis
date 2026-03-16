using NUnit.Framework;
using FrameAnalyzer.Editor.Claude;
using FrameAnalyzer.Runtime.Data;

namespace FrameAnalyzer.Editor.Tests
{
    public class AnalysisPromptBuilderTests
    {
        [Test]
        public void Build_ContainsAgentContent()
        {
            var session = MakeSession();
            var prompt = AnalysisPromptBuilder.Build(session);

            // Should contain the fallback agent prompt or loaded agent file content
            // The fallback contains "Unity performance analysis expert"
            Assert.IsTrue(prompt.Contains("performance analysis expert") ||
                          prompt.Contains("Unity Performance Analysis Agent"),
                $"Expected agent prompt content, got: {prompt.Substring(0, System.Math.Min(200, prompt.Length))}");
        }

        [Test]
        public void Build_ContainsSessionData()
        {
            var session = MakeSession();
            session.DeviceName = "TestDevice";
            var prompt = AnalysisPromptBuilder.Build(session);

            Assert.IsTrue(prompt.Contains("TestDevice"));
            Assert.IsTrue(prompt.Contains("Captured Performance Data"));
        }

        [Test]
        public void Build_ContainsAnalysisInstructions()
        {
            var session = MakeSession();
            var prompt = AnalysisPromptBuilder.Build(session);

            Assert.IsTrue(prompt.Contains("Your Task"));
            Assert.IsTrue(prompt.Contains("Executive Summary"));
            Assert.IsTrue(prompt.Contains("Critical Issues"));
        }

        [Test]
        public void Build_IncludesSceneData()
        {
            var session = MakeSession();
            var sceneData = "Total GameObjects: 250\nRenderers: 80";
            var prompt = AnalysisPromptBuilder.Build(session, sceneData);

            Assert.IsTrue(prompt.Contains("Total GameObjects: 250"));
            Assert.IsTrue(prompt.Contains("Scene Structure Issues"));
        }

        [Test]
        public void Build_OmitsSceneSection_WhenNoSceneData()
        {
            var session = MakeSession();
            var prompt = AnalysisPromptBuilder.Build(session, null);

            Assert.IsFalse(prompt.Contains("Scene Structure Issues"));
        }

        [Test]
        public void Build_McpAvailable_AddsMcpInstructions()
        {
            var session = MakeSession();
            var prompt = AnalysisPromptBuilder.Build(session, null, mcpAvailable: true);

            Assert.IsTrue(prompt.Contains("MCP"));
        }

        [Test]
        public void Build_McpNotAvailable_NoMcpInstructions()
        {
            var session = MakeSession();
            var prompt = AnalysisPromptBuilder.Build(session, null, mcpAvailable: false);

            Assert.IsFalse(prompt.Contains("Unity MCP server"));
        }

        [Test]
        public void Build_UserNotes_IncludedInPrompt()
        {
            var session = MakeSession();
            var prompt = AnalysisPromptBuilder.Build(session, null, false,
                "Ignore static batching, we know about it. Focus on GC allocations.");

            Assert.IsTrue(prompt.Contains("Ignore static batching"));
            Assert.IsTrue(prompt.Contains("Developer notes"));
        }

        [Test]
        public void Build_NullNotes_NoNotesSection()
        {
            var session = MakeSession();
            var prompt = AnalysisPromptBuilder.Build(session, null, false, null);

            Assert.IsFalse(prompt.Contains("Developer notes"));
        }

        [Test]
        public void Build_ContainsEditorProfilingCaveat()
        {
            var session = MakeSession();
            var prompt = AnalysisPromptBuilder.Build(session);

            Assert.IsTrue(prompt.Contains("Unity Editor, not on a target device"));
        }

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
                Cpu = new CpuTimingData { WasCollected = true, PlayerLoopMs = 16.6 }
            });
            return session;
        }
    }
}
