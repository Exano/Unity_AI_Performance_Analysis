using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using UnityEngine;

namespace FrameAnalyzer.Editor.Claude
{
    /// <summary>
    /// Checks for and optionally registers the Unity MCP server with Claude CLI.
    /// </summary>
    public static class McpHelper
    {
        public static string GetMcpUrl()
        {
            const string prefKey = "MCPForUnity.HttpUrl";
            const string defaultBase = "http://127.0.0.1:8080";
            var baseUrl = UnityEditor.EditorPrefs.GetString(prefKey, "");
            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = defaultBase;
            baseUrl = baseUrl.TrimEnd('/');
            if (baseUrl.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
                return baseUrl;
            return baseUrl + "/mcp";
        }

        /// <summary>
        /// Returns true if the MCP server appears to be running.
        /// Uses a short timeout to avoid blocking the editor.
        /// </summary>
        public static bool IsMcpAvailable()
        {
            try
            {
                var mcpUrl = GetMcpUrl();
                // Strip only the trailing /mcp segment
                var baseUrl = mcpUrl.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase)
                    ? mcpUrl.Substring(0, mcpUrl.Length - 4)
                    : mcpUrl;

                var request = WebRequest.Create(baseUrl) as HttpWebRequest;
                if (request == null) return false;
                request.Method = "HEAD";
                request.Timeout = 2000; // 2 second timeout
                using (var response = request.GetResponse())
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures the MCP server is registered with Claude CLI.
        /// Returns a status message or null if already registered.
        /// </summary>
        public static string EnsureMcpRegistered(string workingDirectory)
        {
            var mcpUrl = GetMcpUrl();
            try
            {
                var listOutput = RunClaudeCommand("claude mcp list", workingDirectory);
                if (listOutput != null && listOutput.Contains(mcpUrl))
                    return null; // Already registered

                // Sanitize the URL for shell safety
                if (!Uri.IsWellFormedUriString(mcpUrl, UriKind.Absolute))
                    return $"Invalid MCP URL: {mcpUrl}";

                var addCmd = $"claude mcp add --scope local --transport http unity {mcpUrl}";
                var result = RunClaudeCommand(addCmd, workingDirectory);
                return result != null
                    ? $"Registered Unity MCP server at {mcpUrl}"
                    : "Failed to register MCP server";
            }
            catch (Exception e)
            {
                return $"MCP registration check failed: {e.Message}";
            }
        }

        static string RunClaudeCommand(string command, string workingDirectory)
        {
            var psi = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            if (Application.platform == RuntimePlatform.WindowsEditor)
            { psi.FileName = "cmd.exe"; psi.Arguments = $"/c {command}"; }
            else
            { psi.FileName = "/bin/bash"; psi.Arguments = $"-l -c '{command}'"; }

            psi.Environment.Remove("CLAUDECODE");
            psi.Environment.Remove("CLAUDE_CODE_ENTRYPOINT");

            using (var proc = Process.Start(psi))
            {
                // Read both streams before WaitForExit to prevent pipe buffer deadlock
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(10000);
                if (!proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                    return null;
                }
                return proc.ExitCode == 0 ? stdout : null;
            }
        }
    }
}
