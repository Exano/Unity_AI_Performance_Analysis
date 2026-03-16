using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FrameAnalyzer.Runtime.Data;
using FrameAnalyzer.Runtime.Serialization;
using UnityEngine;

namespace FrameAnalyzer.Editor.History
{
    /// <summary>
    /// Persists completed capture sessions to Library/FrameAnalyzerHistory/ for comparison.
    /// </summary>
    public static class SessionHistory
    {
        static readonly string HistoryDir = Path.Combine(Application.dataPath, "..", "Library", "FrameAnalyzerHistory");

        public struct HistoryEntry
        {
            public string FilePath;
            public string Label;      // Human-readable: "Mar 15 14:32 — 62 FPS, High, 120 frames"
            public string Timestamp;   // Sortable ISO string
        }

        /// <summary>
        /// Save a completed session. Returns the file path.
        /// </summary>
        public static string Save(CaptureSession session)
        {
            if (!Directory.Exists(HistoryDir))
                Directory.CreateDirectory(HistoryDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            double fps = session.Summary?.AvgFps ?? 0;
            string fpsStr = fps > 0 ? $"{fps:F0}fps" : "nofps";
            string fileName = $"{timestamp}_{fpsStr}_{session.QualityLevel ?? "unknown"}.json";
            string path = Path.Combine(HistoryDir, fileName);

            var json = SessionSerializer.ToJson(session);
            File.WriteAllText(path, json);
            return path;
        }

        /// <summary>
        /// Load a session from a history file.
        /// </summary>
        public static CaptureSession Load(string path)
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return SessionSerializer.FromJson(json);
        }

        /// <summary>
        /// List all saved sessions, newest first.
        /// </summary>
        public static List<HistoryEntry> ListSessions()
        {
            var entries = new List<HistoryEntry>();
            if (!Directory.Exists(HistoryDir))
                return entries;

            var files = Directory.GetFiles(HistoryDir, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToArray();

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                // Parse: "2026-03-15_14-32-00_62fps_High"
                var parts = name.Split('_');
                string label = name;
                string timestamp = "";
                if (parts.Length >= 4)
                {
                    var date = parts[0]; // 2026-03-15
                    var time = parts[1]; // 14-32-00
                    var fps = parts[2];  // 62fps
                    var quality = parts[3]; // High
                    try
                    {
                        var dt = DateTime.ParseExact($"{date}_{time}", "yyyy-MM-dd_HH-mm-ss", null);
                        label = $"{dt:MMM dd HH:mm} — {fps}, {quality}";
                        timestamp = dt.ToString("o");
                    }
                    catch
                    {
                        label = name;
                    }
                }
                entries.Add(new HistoryEntry { FilePath = file, Label = label, Timestamp = timestamp });
            }

            return entries;
        }

        /// <summary>
        /// Delete old sessions, keeping the most recent N.
        /// </summary>
        public static void Prune(int keepCount = 20)
        {
            if (!Directory.Exists(HistoryDir)) return;
            var files = Directory.GetFiles(HistoryDir, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .Skip(keepCount)
                .ToArray();
            foreach (var file in files)
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}
