using FrameAnalyzer.Runtime.Collectors;
using FrameAnalyzer.Runtime.Data;
using UnityEditor;

namespace FrameAnalyzer.Editor.Collectors
{
    /// <summary>
    /// Captures rendering statistics from UnityStats (editor-only API).
    /// </summary>
    public class RenderingStatsCollector : IFrameDataCollector
    {
        public void Begin() { }

        public void Collect(FrameSnapshot snapshot)
        {
            snapshot.Rendering = new RenderingData
            {
                WasCollected = true,
                Batches = UnityStats.batches,
                DrawCalls = UnityStats.drawCalls,
                SetPassCalls = UnityStats.setPassCalls,
                Triangles = UnityStats.triangles,
                Vertices = UnityStats.vertices,
                ShadowCasters = UnityStats.shadowCasters,
                RenderTextureChanges = UnityStats.renderTextureChanges,
                VisibleSkinnedMeshes = UnityStats.visibleSkinnedMeshes
            };
        }

        public void End() { }
    }
}
