using System.Collections.Generic;
using NUnit.Framework;
using FrameAnalyzer.Editor.Capture;
using FrameAnalyzer.Runtime.Collectors;
using FrameAnalyzer.Runtime.Data;
using FrameAnalyzer.Runtime.Tests; // MockCollector

namespace FrameAnalyzer.Editor.Tests
{
    public class CaptureOrchestratorTests
    {
        [Test]
        public void Constructor_StoresCollectors()
        {
            var collectors = new List<IFrameDataCollector> { new MockCollector() };
            var orch = new CaptureOrchestrator(collectors);
            Assert.AreEqual(CaptureOrchestrator.CaptureState.Idle, orch.State);
        }

        [Test]
        public void Constructor_NullCollectors_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new CaptureOrchestrator(null));
        }

        [Test]
        public void StartCapture_NotInPlayMode_Throws()
        {
            // In the test runner, Application.isPlaying is false
            var collectors = new List<IFrameDataCollector> { new MockCollector() };
            var orch = new CaptureOrchestrator(collectors);
            Assert.Throws<System.InvalidOperationException>(() => orch.StartCapture(10));
        }

        [Test]
        public void Cancel_WhenIdle_DoesNothing()
        {
            var mock = new MockCollector();
            var orch = new CaptureOrchestrator(new List<IFrameDataCollector> { mock });
            orch.Cancel(); // Should not throw
            Assert.AreEqual(CaptureOrchestrator.CaptureState.Idle, orch.State);
            Assert.AreEqual(0, mock.EndCount);
        }

        [Test]
        public void Progress_WhenIdle_IsZero()
        {
            var orch = new CaptureOrchestrator(new List<IFrameDataCollector>());
            Assert.AreEqual(0f, orch.Progress);
        }
    }
}
