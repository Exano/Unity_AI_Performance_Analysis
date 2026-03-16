using System.Text;
using FrameAnalyzer.Runtime.Data;
using FrameAnalyzer.Runtime.Serialization;

namespace FrameAnalyzer.Editor.Claude
{
    /// <summary>
    /// Builds a prompt that compares two capture sessions and asks Claude
    /// to explain what improved, what regressed, and what's unchanged.
    /// </summary>
    public static class ComparisonPromptBuilder
    {
        public static string Build(CaptureSession baseline, CaptureSession current,
            string baselineLabel, string currentLabel, string userNotes = null)
        {
            var sb = new StringBuilder();

            // Agent expertise (same as single analysis)
            string agentContent = AnalysisPromptBuilder.LoadAgentPromptPublic();
            if (!string.IsNullOrEmpty(agentContent))
            {
                sb.AppendLine(agentContent);
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Performance Comparison");
            sb.AppendLine();
            sb.AppendLine($"You are comparing two capture sessions to determine whether performance has improved or regressed.");
            sb.AppendLine();

            // Baseline session
            sb.AppendLine($"## BASELINE: {baselineLabel}");
            sb.AppendLine();
            sb.Append(SessionSerializer.ToAnalysisPrompt(baseline));
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();

            // Current session
            sb.AppendLine($"## CURRENT: {currentLabel}");
            sb.AppendLine();
            sb.Append(SessionSerializer.ToAnalysisPrompt(current));
            sb.AppendLine();

            // Comparison instructions
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Your Task");
            sb.AppendLine();
            sb.AppendLine("Compare BASELINE vs CURRENT and produce a clear, non-technical progress report. Structure your response as:");
            sb.AppendLine();
            sb.AppendLine("1. **Overall Verdict** — One sentence: did performance improve, regress, or stay the same? Include the FPS change.");
            sb.AppendLine("2. **What Improved** — List each metric that got better, with before/after numbers and the percentage change. Explain what this means in plain language.");
            sb.AppendLine("3. **What Regressed** — List each metric that got worse, same format. Flag anything that should be investigated.");
            sb.AppendLine("4. **Unchanged** — Briefly note areas with no significant change.");
            sb.AppendLine("5. **Script Changes** — If profiler hierarchy data is available for both sessions, compare the top methods. Did any hotspots disappear? Did new ones appear? Did GC allocation patterns change?");
            sb.AppendLine("6. **Recommended Next Steps** — Based on the remaining bottlenecks in CURRENT, what should the developer focus on next?");
            sb.AppendLine();
            sb.AppendLine("Use simple language. The audience may not be deeply technical. Use phrases like:");
            sb.AppendLine("- \"Frame rate improved from 45 to 62 FPS (38% faster)\"");
            sb.AppendLine("- \"Draw calls dropped from 800 to 350, meaning the GPU has much less work to do\"");
            sb.AppendLine("- \"Memory allocations per frame went up — this will eventually cause stutters\"");
            sb.AppendLine();
            sb.AppendLine("Always include specific numbers. Avoid jargon without a brief explanation.");
            sb.AppendLine();
            sb.AppendLine("**Important context:** Both captures were taken in the Unity Editor, not on a target device. Absolute FPS numbers will differ from real hardware. Focus on *relative changes* between the two sessions — did things get better or worse, and by how much?");

            if (!string.IsNullOrEmpty(userNotes))
            {
                sb.AppendLine();
                sb.AppendLine("**Developer notes — READ CAREFULLY:** The developer has provided the following guidance. Respect these notes: skip topics they say to ignore, and do not flag regressions in areas they have explicitly accepted.");
                sb.AppendLine();
                sb.AppendLine(userNotes);
            }

            return sb.ToString();
        }
    }
}
