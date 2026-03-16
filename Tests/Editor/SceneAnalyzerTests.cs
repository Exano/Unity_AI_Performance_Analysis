using System.Collections.Generic;
using NUnit.Framework;
using FrameAnalyzer.Editor.SceneAnalysis;

namespace FrameAnalyzer.Editor.Tests
{
    public class SceneAnalyzerTests
    {
        [Test]
        public void FormatSnapshot_ContainsGameObjectCount()
        {
            var snap = MakeSnapshot(totalGOs: 150);
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("Total GameObjects: 150"));
        }

        [Test]
        public void FormatSnapshot_ContainsHierarchyDepth()
        {
            var snap = MakeSnapshot();
            snap.MaxHierarchyDepth = 8;
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("Max Hierarchy Depth: 8"));
        }

        [Test]
        public void FormatSnapshot_ContainsRendererCount()
        {
            var snap = MakeSnapshot();
            snap.RendererCount = 45;
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("Renderers: 45"));
        }

        [Test]
        public void FormatSnapshot_ContainsMeshColliderBreakdown()
        {
            var snap = MakeSnapshot();
            snap.ColliderCount = 30;
            snap.MeshColliderConvexCount = 5;
            snap.MeshColliderNonConvexCount = 3;
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("non-convex: 3"));
            Assert.IsTrue(text.Contains("convex: 5"));
        }

        [Test]
        public void FormatSnapshot_ContainsMaterialAnalysis()
        {
            var snap = MakeSnapshot();
            snap.PropertyBlockRendererCount = 10;
            snap.SharedMaterialCount = 25;
            snap.UniqueShaderCount = 5;
            snap.ShaderNames = new List<string> { "Universal Render Pipeline/Lit", "Custom/MyShader" };
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("MaterialPropertyBlock"));
            Assert.IsTrue(text.Contains("Universal Render Pipeline/Lit"));
        }

        [Test]
        public void FormatSnapshot_ContainsSrpBatcherIncompatible()
        {
            var snap = MakeSnapshot();
            snap.SrpBatcherIncompatibleShaders = new List<string> { "Legacy Shaders/Diffuse" };
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("SRP Batcher INCOMPATIBLE"));
            Assert.IsTrue(text.Contains("Legacy Shaders/Diffuse"));
        }

        [Test]
        public void FormatSnapshot_ContainsStaticFlags()
        {
            var snap = MakeSnapshot();
            snap.StaticBatchingCount = 40;
            snap.StaticLightmapCount = 35;
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("Batching Static: 40"));
            Assert.IsTrue(text.Contains("Lightmap Static: 35"));
        }

        [Test]
        public void FormatSnapshot_ContainsLODCoverage()
        {
            var snap = MakeSnapshot();
            snap.RendererCount = 100;
            snap.LODGroupCount = 25;
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("LOD Coverage"));
            Assert.IsTrue(text.Contains("25%"));
        }

        [Test]
        public void FormatSnapshot_ZeroLODGroups_ShowsZeroCoverage()
        {
            var snap = MakeSnapshot();
            snap.RendererCount = 50;
            snap.LODGroupCount = 0;
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("LOD Coverage"));
            Assert.IsTrue(text.Contains("0% coverage"));
        }

        [Test]
        public void FormatSnapshot_ContainsCanvasAndRaycastInfo()
        {
            var snap = MakeSnapshot();
            snap.CanvasCount = 3;
            snap.RaycastTargetCount = 42;
            var text = SceneAnalyzer.FormatSnapshot(snap);
            Assert.IsTrue(text.Contains("Canvas: 3"));
            Assert.IsTrue(text.Contains("Raycast Targets: 42"));
        }

        static SceneAnalyzer.SceneSnapshot MakeSnapshot(int totalGOs = 100)
        {
            return new SceneAnalyzer.SceneSnapshot
            {
                TotalGameObjects = totalGOs,
                MaxHierarchyDepth = 5,
                ComponentCounts = new Dictionary<string, int>(),
                ShaderNames = new List<string>(),
                SrpBatcherIncompatibleShaders = new List<string>(),
                RendererCount = 20,
                LODGroupCount = 0
            };
        }
    }
}
