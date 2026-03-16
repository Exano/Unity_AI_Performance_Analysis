using NUnit.Framework;
using FrameAnalyzer.Runtime.Collectors;
using FrameAnalyzer.Runtime.Data;

namespace FrameAnalyzer.Runtime.Tests
{
    public class MockCollector : IFrameDataCollector
    {
        public int BeginCount;
        public int CollectCount;
        public int EndCount;
        public CpuTimingData CpuData;

        public void Begin() => BeginCount++;
        public void End() => EndCount++;

        public void Collect(FrameSnapshot snapshot)
        {
            CollectCount++;
            snapshot.Cpu = CpuData;
        }
    }

    public class MockCollectorTests
    {
        [Test]
        public void MockCollector_ImplementsInterface()
        {
            IFrameDataCollector collector = new MockCollector();
            Assert.IsNotNull(collector);
        }

        [Test]
        public void MockCollector_Begin_IncrementsCount()
        {
            var mock = new MockCollector();
            mock.Begin();
            mock.Begin();
            Assert.AreEqual(2, mock.BeginCount);
        }

        [Test]
        public void MockCollector_Collect_WritesToSnapshot()
        {
            var mock = new MockCollector
            {
                CpuData = new CpuTimingData { WasCollected = true, PlayerLoopMs = 16.6 }
            };

            var snapshot = new FrameSnapshot();
            mock.Collect(snapshot);

            Assert.IsTrue(snapshot.Cpu.WasCollected);
            Assert.AreEqual(16.6, snapshot.Cpu.PlayerLoopMs, 0.001);
            Assert.AreEqual(1, mock.CollectCount);
        }

        [Test]
        public void MockCollector_End_IncrementsCount()
        {
            var mock = new MockCollector();
            mock.End();
            Assert.AreEqual(1, mock.EndCount);
        }

        [Test]
        public void MockCollector_FullLifecycle()
        {
            var mock = new MockCollector
            {
                CpuData = new CpuTimingData { WasCollected = true, PlayerLoopMs = 10.0 }
            };

            mock.Begin();
            var snapshot = new FrameSnapshot();
            mock.Collect(snapshot);
            mock.Collect(snapshot);
            mock.End();

            Assert.AreEqual(1, mock.BeginCount);
            Assert.AreEqual(2, mock.CollectCount);
            Assert.AreEqual(1, mock.EndCount);
        }
    }
}
