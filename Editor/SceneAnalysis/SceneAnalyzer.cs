using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using Object = UnityEngine.Object;

namespace FrameAnalyzer.Editor.SceneAnalysis
{
    public static class SceneAnalyzer
    {
        public struct SceneSnapshot
        {
            public int TotalGameObjects;
            public int MaxHierarchyDepth;
            public Dictionary<string, int> ComponentCounts;
            public int RendererCount;
            public int MeshFilterCount;
            public int ColliderCount;
            public int MeshColliderConvexCount;
            public int MeshColliderNonConvexCount;
            public int RigidbodyCount;
            public int AnimatorCount;
            public int CanvasCount;
            public int RaycastTargetCount;
            public int ParticleSystemCount;
            public int LODGroupCount;
            public int LightCount;
            public int ShadowCastingLightCount;
            public int UniqueShaderCount;
            public int PropertyBlockRendererCount;
            public int SharedMaterialCount;
            public List<string> ShaderNames;
            public List<string> SrpBatcherIncompatibleShaders;
            public int StaticBatchingCount;
            public int StaticLightmapCount;
            public int StaticOccludeeCount;
            public int StaticOccluderCount;
            public long EstimatedTextureMB;
        }

        public static SceneSnapshot CaptureSnapshot()
        {
            var snap = new SceneSnapshot
            {
                ComponentCounts = new Dictionary<string, int>(),
                ShaderNames = new List<string>(),
                SrpBatcherIncompatibleShaders = new List<string>()
            };

            var allGOs = GetAllGameObjects();
            snap.TotalGameObjects = allGOs.Length;

            var shaderSet = new HashSet<string>();
            var materialSet = new HashSet<int>();
            var sharedMaterialSet = new HashSet<int>();
            var srpIncompatible = new HashSet<string>();

            foreach (var go in allGOs)
            {
                // Hierarchy depth
                int depth = GetDepth(go.transform);
                if (depth > snap.MaxHierarchyDepth)
                    snap.MaxHierarchyDepth = depth;

                // Static flags
                var flags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & UnityEditor.StaticEditorFlags.BatchingStatic) != 0) snap.StaticBatchingCount++;
                if ((flags & UnityEditor.StaticEditorFlags.ContributeGI) != 0) snap.StaticLightmapCount++;
                if ((flags & UnityEditor.StaticEditorFlags.OccludeeStatic) != 0) snap.StaticOccludeeCount++;
                if ((flags & UnityEditor.StaticEditorFlags.OccluderStatic) != 0) snap.StaticOccluderCount++;

                // Components
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue; // Missing script
                    var typeName = comp.GetType().Name;
                    snap.ComponentCounts.TryGetValue(typeName, out int count);
                    snap.ComponentCounts[typeName] = count + 1;

                    switch (comp)
                    {
                        case Renderer renderer:
                            snap.RendererCount++;
                            AnalyzeMaterials(renderer, materialSet, sharedMaterialSet, shaderSet, srpIncompatible);
                            break;
                        case MeshFilter _:
                            snap.MeshFilterCount++;
                            break;
                        case MeshCollider mc:
                            snap.ColliderCount++;
                            if (mc.convex) snap.MeshColliderConvexCount++;
                            else snap.MeshColliderNonConvexCount++;
                            break;
                        case Collider _:
                            snap.ColliderCount++;
                            break;
                        case Rigidbody _:
                            snap.RigidbodyCount++;
                            break;
                        case Animator _:
                            snap.AnimatorCount++;
                            break;
                        case Canvas _:
                            snap.CanvasCount++;
                            break;
                        case ParticleSystem _:
                            snap.ParticleSystemCount++;
                            break;
                        case LODGroup _:
                            snap.LODGroupCount++;
                            break;
                        case Light light:
                            snap.LightCount++;
                            if (light.shadows != LightShadows.None)
                                snap.ShadowCastingLightCount++;
                            break;
                    }

                    // Raycast targets on UI elements
                    if (comp is UnityEngine.UI.Graphic graphic && graphic.raycastTarget)
                        snap.RaycastTargetCount++;
                }
            }

            snap.PropertyBlockRendererCount = materialSet.Count;
            snap.SharedMaterialCount = sharedMaterialSet.Count;
            snap.UniqueShaderCount = shaderSet.Count;
            snap.ShaderNames = shaderSet.ToList();
            snap.SrpBatcherIncompatibleShaders = srpIncompatible.ToList();
            snap.EstimatedTextureMB = EstimateTextureMemory();

            return snap;
        }

        public static string FormatSnapshot(SceneSnapshot snap)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"- Total GameObjects: {snap.TotalGameObjects}");
            sb.AppendLine($"- Max Hierarchy Depth: {snap.MaxHierarchyDepth}");
            sb.AppendLine();

            sb.AppendLine("### Component Summary");
            sb.AppendLine($"- Renderers: {snap.RendererCount}");
            sb.AppendLine($"- MeshFilters: {snap.MeshFilterCount}");
            sb.AppendLine($"- Colliders: {snap.ColliderCount} (MeshCollider convex: {snap.MeshColliderConvexCount}, non-convex: {snap.MeshColliderNonConvexCount})");
            sb.AppendLine($"- Rigidbodies: {snap.RigidbodyCount}");
            sb.AppendLine($"- Animators: {snap.AnimatorCount}");
            sb.AppendLine($"- Canvas: {snap.CanvasCount}");
            sb.AppendLine($"- Raycast Targets: {snap.RaycastTargetCount}");
            sb.AppendLine($"- ParticleSystems: {snap.ParticleSystemCount}");
            sb.AppendLine($"- LODGroups: {snap.LODGroupCount}");
            sb.AppendLine($"- Lights: {snap.LightCount} (shadow-casting: {snap.ShadowCastingLightCount})");
            sb.AppendLine();

            sb.AppendLine("### Materials & Shaders");
            sb.AppendLine($"- Renderers with MaterialPropertyBlock (breaks SRP Batcher): {snap.PropertyBlockRendererCount}");
            sb.AppendLine($"- Shared Materials: {snap.SharedMaterialCount}");
            sb.AppendLine($"- Unique Shaders: {snap.UniqueShaderCount}");
            if (snap.ShaderNames.Count > 0)
            {
                sb.AppendLine($"- Shaders in use: {string.Join(", ", snap.ShaderNames)}");
            }
            if (snap.SrpBatcherIncompatibleShaders.Count > 0)
            {
                sb.AppendLine($"- SRP Batcher INCOMPATIBLE: {string.Join(", ", snap.SrpBatcherIncompatibleShaders)}");
            }
            sb.AppendLine($"- Estimated Texture Memory: ~{snap.EstimatedTextureMB} MB");
            sb.AppendLine();

            sb.AppendLine("### Static Flags");
            sb.AppendLine($"- Batching Static: {snap.StaticBatchingCount}");
            sb.AppendLine($"- Lightmap Static: {snap.StaticLightmapCount}");
            sb.AppendLine($"- Occludee Static: {snap.StaticOccludeeCount}");
            sb.AppendLine($"- Occluder Static: {snap.StaticOccluderCount}");

            // LOD coverage
            if (snap.RendererCount > 0)
            {
                double lodPct = snap.LODGroupCount > 0 ? (100.0 * snap.LODGroupCount / snap.RendererCount) : 0;
                sb.AppendLine();
                sb.AppendLine($"### LOD Coverage");
                sb.AppendLine($"- LOD Groups: {snap.LODGroupCount} / {snap.RendererCount} renderers ({lodPct:F0}% coverage)");
            }

            return sb.ToString();
        }

        static GameObject[] GetAllGameObjects()
        {
            return Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        static int GetDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }

        static void AnalyzeMaterials(Renderer renderer, HashSet<int> materialSet,
            HashSet<int> sharedMaterialSet, HashSet<string> shaderSet, HashSet<string> srpIncompatible)
        {
            var sharedMats = renderer.sharedMaterials;
            foreach (var mat in sharedMats)
            {
                if (mat == null) continue;
                int id = mat.GetInstanceID();
                sharedMaterialSet.Add(id);

                if (mat.shader != null)
                {
                    string shaderName = mat.shader.name;
                    shaderSet.Add(shaderName);

                    // Flag known SRP-Batcher-incompatible shader families.
                    // Unity has no public editor API for programmatic compatibility checks,
                    // so we detect by prefix. Claude can investigate further via MCP if needed.
                    if (shaderName.StartsWith("Legacy Shaders/") ||
                        shaderName.StartsWith("Mobile/") ||
                        shaderName.StartsWith("Particles/") ||
                        shaderName.StartsWith("Hidden/Internal"))
                    {
                        srpIncompatible.Add(shaderName);
                    }
                }
            }

            // Track renderers with MaterialPropertyBlocks (break SRP Batcher)
            if (renderer.HasPropertyBlock())
                materialSet.Add(renderer.GetInstanceID());
        }

        static long EstimateTextureMemory()
        {
            long totalBytes = 0;
            // Resources.FindObjectsOfTypeAll finds all loaded textures (assets + scene)
            // unlike FindObjectsByType which only finds scene objects
            var textures = Resources.FindObjectsOfTypeAll<Texture>();
            foreach (var tex in textures)
            {
                if (tex is Texture2D t2d)
                {
                    long pixels = (long)t2d.width * t2d.height;
                    int bpp = GetBitsPerPixel(t2d.format);
                    long texBytes = pixels * bpp / 8;
                    if (t2d.mipmapCount > 1)
                        texBytes = texBytes * 4 / 3; // Mipmaps add ~33%
                    totalBytes += texBytes;
                }
                else if (tex is RenderTexture rt)
                {
                    // rt.depth is the depth-buffer bit-depth (0/16/24/32), not a spatial dimension
                    // Color buffer: width * height * volumeDepth * 4 bytes (RGBA8 estimate)
                    int volumeDepth = rt.volumeDepth > 1 ? rt.volumeDepth : 1;
                    long colorBytes = (long)rt.width * rt.height * volumeDepth * 4;
                    // Depth buffer: width * height * (depthBits / 8)
                    long depthBytes = rt.depth > 0 ? (long)rt.width * rt.height * (rt.depth / 8) : 0;
                    totalBytes += colorBytes + depthBytes;
                }
            }
            return totalBytes / (1024 * 1024);
        }

        static int GetBitsPerPixel(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                    return 32;
                case TextureFormat.RGB24:
                    return 24;
                case TextureFormat.RGB565:
                case TextureFormat.RGBA4444:
                    return 16;
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                    return 8;
                case TextureFormat.RGBAHalf:
                    return 64;
                case TextureFormat.RGBAFloat:
                    return 128;
                case TextureFormat.DXT1:
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC2_RGB:
                    return 4;
                case TextureFormat.DXT5:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.BC7:
                    return 8;
                case TextureFormat.ASTC_4x4:
                    return 8;
                case TextureFormat.ASTC_6x6:
                    return 4;
                case TextureFormat.ASTC_8x8:
                    return 2;
                default:
                    return 32; // Conservative estimate
            }
        }
    }
}
