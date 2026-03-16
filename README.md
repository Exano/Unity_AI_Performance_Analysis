# Unity AI Performance Analysis

**1-Click Play Mode profiler for Unity 6 + URP.**

Captures CPU, GPU, memory, per-URP-pass timing, and scene structure, then streams an AI-powered performance report via Claude. Includes per-method hotspot detection, loaded asset memory breakdown, session history, and side-by-side comparison. All displayed in a formatted editor window and exportable as a Markdown file.

## Features

- **Per-frame profiler capture**: CPU timing, GPU timing, memory, rendering stats, GC allocations
- **URP render pass breakdown**: Per-pass CPU timing for DrawOpaqueObjects, Bloom, Shadows, SSAO, etc.
- **Bottleneck classification**: Automatic CPU/GPU/PresentLimited/Balanced detection per frame
- **Memory breakdown**: Loaded asset memory analysis by type (textures, meshes, audio, etc.)
- **Per-method hotspot detection**: Identify the most expensive methods in your frame
- **Scene structure analysis**: Hierarchy depth, component counts, material/shader inspection, LOD coverage, static flags
- **AI-powered analysis**: Sends captured data to Claude Code CLI for expert performance report
- **Session history**: Save and revisit past profiling sessions
- **Side-by-side comparison**: Compare two sessions to track optimization progress
- **Optional MCP integration**: Claude can query the scene via MCP for deeper investigation
- **Streaming markdown output**: Real-time response display with Catppuccin Mocha theme
- **Markdown export**: Export formatted reports as `.md` files

## Requirements

- Unity 6 (6000.0+)
- Universal Render Pipeline (URP)
- [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) installed (`npm install -g @anthropic-ai/claude-code`)

## Installation

1. Open the Unity Package Manager (`Window > Package Manager`)
2. Click **+** > **Add package from git URL...**
3. Paste:
   ```
   https://github.com/Exano/Unity_AI_Performance_Analysis.git
   ```

## Usage

1. Open **Window > Frame Analyzer** (or press `Ctrl+Shift+J`)
2. Enter Play Mode
3. Configure frame count (30-600) and options
4. Click **Analyze**
5. Wait for frame capture + AI analysis
6. Read the performance report

## Configuration

| Option | Description |
|--------|-------------|
| Frames | Number of frames to capture (default: 120) |
| Scene Analysis | Capture scene structure (hierarchy, components, materials) |
| MCP | Use Unity MCP server for deeper scene queries during analysis |
| Skip Permissions | Run Claude with `--dangerously-skip-permissions` |

## Data Captured

### Per Frame
- CPU: PlayerLoop, Update, LateUpdate, FixedUpdate, Rendering, Physics, Scripts, Animation, GC.Collect
- Memory: Managed heap, GC allocations (bytes + count)
- GPU: CPU frame time, GPU frame time, main thread, render thread
- URP Passes: ~40 render pass markers with CPU timing
- Rendering: Batches, draw calls, set-pass calls, triangles, vertices
- Bottleneck: CPU/GPU/PresentLimited/Balanced classification

### Scene Snapshot (once)
- Total GameObjects, hierarchy depth
- Component breakdown (Renderers, Colliders, Rigidbodies, Animators, Canvas, etc.)
- Material/shader analysis with SRP Batcher compatibility check
- Static flags distribution, LOD coverage, texture memory estimate

## Optional: MCP Integration

If [unity-mcp](https://github.com/nicoreed/unity-mcp) (`com.coplaydev.unity-mcp`) is installed, Claude can use MCP tools during analysis to query specific GameObjects, inspect materials, and capture screenshots. This enables follow-up investigation of issues found in the profiler data.

Without MCP, the tool works fully — Claude analyzes with `--max-turns 1`. With MCP, Claude gets `--max-turns 3` for deeper investigation.

## Export

Click **Export** to save the raw capture session as JSON for later analysis.

## License

This project is licensed under the [Business Source License 1.1](LICENSE).
Non-production, personal, and educational use is permitted.
On **2030-03-15** the license converts to **GPL v2.0 or later**.

## Author

**Tony The Dev** — [One Mechanic Studios](https://github.com/Exano) | [tonythedev.com](https://tonythedev.com)
