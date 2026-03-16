using System;
using System.Collections.Generic;
using FrameAnalyzer.Runtime.Collectors;
using FrameAnalyzer.Runtime.Data;
using UnityEngine;

namespace FrameAnalyzer.Editor.Capture
{
    public class CaptureOrchestrator
    {
        public enum CaptureState { Idle, Capturing, Complete, Cancelled }

        public CaptureState State { get; private set; } = CaptureState.Idle;
        public int CurrentFrame { get; private set; }
        public int TotalFrames { get; private set; }
        public CaptureSession Session { get; private set; }
        public float Progress => TotalFrames > 0 ? (float)CurrentFrame / TotalFrames : 0f;

        private readonly List<IFrameDataCollector> _collectors;

        public CaptureOrchestrator(List<IFrameDataCollector> collectors)
        {
            _collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
        }

        public void StartCapture(int frameCount)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Frame capture requires Play Mode.");

            if (State == CaptureState.Capturing)
                throw new InvalidOperationException("Capture already in progress.");

            TotalFrames = frameCount;
            CurrentFrame = 0;
            State = CaptureState.Capturing;

            Session = new CaptureSession
            {
                RequestedFrameCount = frameCount
            };
            Session.PopulateSystemInfo();

            foreach (var collector in _collectors)
                collector.Begin();
        }

        /// <summary>
        /// Call once per frame during capture. Returns true when capture is complete.
        /// </summary>
        public bool CaptureFrame()
        {
            if (State != CaptureState.Capturing) return true;

            var snapshot = new FrameSnapshot { FrameIndex = CurrentFrame };
            foreach (var collector in _collectors)
            {
                try
                {
                    collector.Collect(snapshot);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[FrameAnalyzer] Collector {collector.GetType().Name} failed: {e.Message}");
                }
            }
            Session.Frames.Add(snapshot);

            CurrentFrame++;
            if (CurrentFrame >= TotalFrames)
            {
                EndCapture();
                return true;
            }
            return false;
        }

        public void Cancel()
        {
            if (State != CaptureState.Capturing) return;
            foreach (var collector in _collectors)
                collector.End();
            State = CaptureState.Cancelled;
        }

        private void EndCapture()
        {
            foreach (var collector in _collectors)
                collector.End();
            Session.ComputeSummary();
            State = CaptureState.Complete;
        }
    }
}
