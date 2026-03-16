using System;
using System.IO;
using Unity.Profiling.Memory;
using UnityEngine;

namespace FrameAnalyzer.Editor.Capture
{
    /// <summary>
    /// Takes a memory snapshot using Unity's Memory Profiler API.
    /// The snapshot can be opened in Window > Analysis > Memory Profiler for detailed inspection.
    /// </summary>
    public static class MemorySnapshotCapture
    {
        /// <summary>
        /// Takes a memory snapshot and saves it to the project's MemoryCaptures folder.
        /// Returns the file path, or null on failure.
        /// </summary>
        public static string TakeSnapshot(Action<string> onComplete = null)
        {
            try
            {
                var dir = Path.Combine(Application.dataPath, "..", "MemoryCaptures");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var path = Path.Combine(dir, $"frame-analyzer-{timestamp}.snap");

                bool finished = false;
                string resultPath = null;

                MemoryProfiler.TakeSnapshot(path, (filePath, success) =>
                {
                    if (success)
                    {
                        resultPath = filePath;
                        Debug.Log($"[FrameAnalyzer] Memory snapshot saved: {filePath}");
                    }
                    else
                    {
                        Debug.LogWarning("[FrameAnalyzer] Memory snapshot capture failed.");
                    }
                    finished = true;
                    onComplete?.Invoke(resultPath);
                });

                // The snapshot is async — return the expected path.
                // The actual file may not exist yet; the callback confirms completion.
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FrameAnalyzer] Failed to take memory snapshot: {e.Message}");
                return null;
            }
        }
    }
}
