using System.Collections.Generic;
using FrameAnalyzer.Runtime.Data;
using Unity.Profiling;

namespace FrameAnalyzer.Runtime.Collectors
{
    /// <summary>
    /// Captures per-URP-render-pass CPU timing using ProfilerRecorder.
    /// URPProfileId is internal to URP, so we use string marker names directly.
    /// GPU timing per-pass requires ProfilingSampler which needs active profiling scopes;
    /// ProfilerRecorder gives us CPU-side timing for each pass.
    /// </summary>
    public class UrpPassCollector : IFrameDataCollector
    {
        // URP marker names matching URPProfileId enum values
        static readonly string[] PassNames =
        {
            // Core render passes
            "UniversalRenderTotal",
            "UpdateVolumeFramework",
            "RenderCameraStack",

            // Geometry
            "DrawOpaqueObjects",
            "DrawTransparentObjects",
            "DrawScreenSpaceUI",
            "DrawSkybox",

            // Depth / Normals
            "DepthPrepass",
            "DrawDepthNormalPrepass",
            "CopyDepth",
            "CopyColor",

            // Shadows
            "MainLightShadow",
            "AdditionalLightsShadow",
            "ResolveShadows",

            // Lighting
            "LightCookies",

            // Post-processing
            "ColorGradingLUT",
            "StopNaNs",
            "SMAA",
            "GaussianDepthOfField",
            "BokehDepthOfField",
            "TemporalAA",
            "MotionBlur",
            "PaniniProjection",
            "UberPostProcess",
            "Bloom",
            "SSAO",

            // Lens Flares
            "LensFlareDataDriven",
            "LensFlareScreenSpace",

            // Motion Vectors
            "DrawMotionVectors",
            "DrawFullscreen",

            // Final
            "BlitFinalToBackBuffer",

            // RenderGraph variants
            "RG_SetupPostFX",
            "RG_TAA",
            "RG_MotionBlur",
            "RG_BloomSetup",
            "RG_BloomPrefilter",
            "RG_BloomDownsample",
            "RG_BloomUpsample",
            "RG_UberPost",
            "RG_FinalBlit",
        };

        private struct RecorderEntry
        {
            public string Name;
            public ProfilerRecorder Recorder;
        }

        private readonly List<RecorderEntry> _activeRecorders = new List<RecorderEntry>();

        public void Begin()
        {
            _activeRecorders.Clear();
            foreach (var name in PassNames)
            {
                var rec = ProfilerRecorder.StartNew(ProfilerCategory.Render, name);
                if (rec.Valid)
                    _activeRecorders.Add(new RecorderEntry { Name = name, Recorder = rec });
                else
                    rec.Dispose();
            }
        }

        public void Collect(FrameSnapshot snapshot)
        {
            // UrpPassTimingData is a struct — must mutate a local then assign back
            var urp = UrpPassTimingData.Create();
            urp.WasCollected = _activeRecorders.Count > 0;

            foreach (var entry in _activeRecorders)
            {
                long ns = entry.Recorder.LastValue;
                if (ns <= 0) continue;

                urp.Passes.Add(new UrpPassEntry
                {
                    PassName = entry.Name,
                    CpuMs = ns / 1_000_000.0,
                    GpuMs = 0 // GPU per-pass timing not available via ProfilerRecorder
                });
            }

            snapshot.UrpPasses = urp;
        }

        public void End()
        {
            foreach (var entry in _activeRecorders)
                entry.Recorder.Dispose();
            _activeRecorders.Clear();
        }
    }
}
