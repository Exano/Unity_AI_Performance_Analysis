using FrameAnalyzer.Runtime.Data;

namespace FrameAnalyzer.Runtime.Collectors
{
    public interface IFrameDataCollector
    {
        void Begin();
        void Collect(FrameSnapshot snapshot);
        void End();
    }
}
