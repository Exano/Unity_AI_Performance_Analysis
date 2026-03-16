using System;
using System.Collections.Generic;
using System.Linq;
using FrameAnalyzer.Runtime.Data;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace FrameAnalyzer.Editor.Capture
{
    /// <summary>
    /// Enumerates all loaded Unity objects, measures their runtime memory via
    /// Profiler.GetRuntimeMemorySizeLong(), and produces a categorized breakdown
    /// with top-N largest assets. This is the data the Memory Profiler window shows
    /// but extracted programmatically for the AI report.
    /// </summary>
    public static class MemoryBreakdownAnalyzer
    {
        private const int TopN = 30;

        // Types we care about, in the order we want them reported
        static readonly Type[] TrackedTypes =
        {
            typeof(Texture2D),
            typeof(RenderTexture),
            typeof(Cubemap),
            typeof(Mesh),
            typeof(AudioClip),
            typeof(AnimationClip),
            typeof(Shader),
            typeof(Material),
            typeof(Font),
            typeof(ComputeShader),
            typeof(RuntimeAnimatorController),
        };

        public static MemoryBreakdownData Analyze()
        {
            var result = new MemoryBreakdownData();

            try
            {
                var categoryMap = new Dictionary<string, MemoryCategorySummary>();
                var allEntries = new List<MemoryAssetEntry>();

                // Scan each tracked type
                foreach (var type in TrackedTypes)
                {
                    var objects = Resources.FindObjectsOfTypeAll(type);
                    string category = type.Name;
                    long categoryTotal = 0;
                    int categoryCount = 0;

                    foreach (var obj in objects)
                    {
                        // Skip built-in resources (editor internals) by checking hideFlags
                        if (obj.hideFlags.HasFlag(HideFlags.HideAndDontSave) && string.IsNullOrEmpty(obj.name))
                            continue;

                        long size = Profiler.GetRuntimeMemorySizeLong(obj);
                        if (size <= 0) continue;

                        categoryTotal += size;
                        categoryCount++;

                        string assetName = string.IsNullOrEmpty(obj.name) ? $"(unnamed {category})" : obj.name;

                        // Add detail for textures, meshes, and audio — the big memory consumers
                        if (obj is Texture2D tex)
                            assetName = $"{tex.name} ({tex.width}x{tex.height} {tex.format})";
                        else if (obj is RenderTexture rt)
                            assetName = $"{rt.name} ({rt.width}x{rt.height} depth:{rt.depth})";
                        else if (obj is Mesh mesh)
                        {
                            // Use GetIndexCount instead of mesh.triangles which copies the entire index buffer
                            int triCount = 0;
                            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                                triCount += (int)mesh.GetIndexCount(sub);
                            triCount /= 3;
                            assetName = $"{mesh.name} ({mesh.vertexCount} verts, {triCount} tris)";
                        }
                        else if (obj is AudioClip clip)
                            assetName = $"{clip.name} ({clip.length:F1}s, {clip.channels}ch)";

                        allEntries.Add(new MemoryAssetEntry
                        {
                            Name = assetName,
                            TypeName = category,
                            SizeBytes = size
                        });
                    }

                    if (categoryCount > 0)
                    {
                        categoryMap[category] = new MemoryCategorySummary
                        {
                            Category = category,
                            Count = categoryCount,
                            TotalBytes = categoryTotal
                        };
                    }
                }

                // Also scan for ScriptableObjects, GameObjects, and Components as a catch-all
                ScanCatchAll<ScriptableObject>("ScriptableObject", allEntries, categoryMap);
                ScanCatchAll<MonoBehaviour>("MonoBehaviour", allEntries, categoryMap);

                // Build result
                result.WasCollected = true;
                result.TotalTrackedBytes = categoryMap.Values.Sum(c => c.TotalBytes);

                // Sort categories by total size descending
                result.ByCategory = categoryMap.Values
                    .OrderByDescending(c => c.TotalBytes)
                    .ToList();

                // Top N assets by size
                result.TopAssets = allEntries
                    .OrderByDescending(e => e.SizeBytes)
                    .Take(TopN)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FrameAnalyzer] Memory breakdown analysis failed: {e.Message}");
            }

            return result;
        }

        static void ScanCatchAll<T>(string category, List<MemoryAssetEntry> entries,
            Dictionary<string, MemoryCategorySummary> categoryMap) where T : Object
        {
            var objects = Resources.FindObjectsOfTypeAll<T>();
            long total = 0;
            int count = 0;

            foreach (var obj in objects)
            {
                if (obj.hideFlags.HasFlag(HideFlags.HideAndDontSave) && string.IsNullOrEmpty(obj.name))
                    continue;

                long size = Profiler.GetRuntimeMemorySizeLong(obj);
                if (size <= 1024) continue; // Skip tiny objects for catch-all

                total += size;
                count++;

                entries.Add(new MemoryAssetEntry
                {
                    Name = string.IsNullOrEmpty(obj.name) ? $"(unnamed {category})" : obj.name,
                    TypeName = category,
                    SizeBytes = size
                });
            }

            if (count > 0 && !categoryMap.ContainsKey(category))
            {
                categoryMap[category] = new MemoryCategorySummary
                {
                    Category = category,
                    Count = count,
                    TotalBytes = total
                };
            }
        }
    }
}
