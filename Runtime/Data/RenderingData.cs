using System;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public struct RenderingData
    {
        public bool WasCollected;
        public int Batches;
        public int DrawCalls;
        public int SetPassCalls;
        public int Triangles;
        public int Vertices;
        public int ShadowCasters;
        public int RenderTextureChanges;
        public int VisibleSkinnedMeshes;
    }
}
