# Unity AI Performance Analysis

**1-Click Play Mode profiler for Unity 6 + URP.**

Captures CPU, GPU, memory, per-URP-pass timing, and scene structure, then streams an AI-powered performance report via Claude. Includes per-method hotspot detection, loaded asset memory breakdown, session history, and side-by-side comparison. All displayed in a formatted editor window and exportable as a Markdown file.

## Features

- **One-Click Profiling** — Enter Play Mode, click Capture, get a full performance snapshot
- **CPU & GPU Analysis** — Per-method hotspot detection with frame time breakdowns
- **URP Pass Timing** — Per-render-pass timing for the Universal Render Pipeline
- **Memory Breakdown** — Loaded asset memory analysis by type (textures, meshes, audio, etc.)
- **Scene Structure** — Hierarchy analysis including object counts, nesting depth, and component distribution
- **AI-Powered Reports** — Streams actionable performance insights via Claude
- **Session History** — Save and revisit past profiling sessions
- **Side-by-Side Comparison** — Compare two sessions to track optimization progress
- **Markdown Export** — Export formatted reports as `.md` files

## Requirements

- Unity 6 (6000.x)
- Universal Render Pipeline (URP)
- Claude API key (for AI analysis)

## Installation

1. Open the Unity Package Manager (`Window > Package Manager`)
2. Click **+** > **Add package from git URL...**
3. Paste:
   ```
   https://github.com/Exano/Unity_AI_Performance_Analysis.git
   ```

## Quick Start

1. Open the profiler window: **Window > Analysis > AI Performance Analysis**
2. Enter Play Mode
3. Click **Capture**
4. Review the AI-generated performance report
5. Export as Markdown if needed

## License

This project is licensed under the [Business Source License 1.1](LICENSE).
Non-production, personal, and educational use is permitted.
On **2030-03-15** the license converts to **GPL v2.0 or later**.

## Author

**Tony The Dev** — [One Mechanic Studios](https://github.com/Exano) | [tonythedev.com](https://tonythedev.com)
