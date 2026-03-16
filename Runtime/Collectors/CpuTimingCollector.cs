using FrameAnalyzer.Runtime.Data;
using Unity.Profiling;

namespace FrameAnalyzer.Runtime.Collectors
{
    public class CpuTimingCollector : IFrameDataCollector
    {
        private ProfilerRecorder _playerLoop;
        private ProfilerRecorder _update;
        private ProfilerRecorder _lateUpdate;
        private ProfilerRecorder _fixedUpdate;
        private ProfilerRecorder _rendering;
        private ProfilerRecorder _physics;
        private ProfilerRecorder _scripts;
        private ProfilerRecorder _animation;
        private ProfilerRecorder _gcCollect;

        public void Begin()
        {
            _playerLoop = TryCreate(ProfilerCategory.Internal, "PlayerLoop");
            _update = TryCreate(ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate", "ScriptRunBehaviourUpdate");
            _lateUpdate = TryCreate(ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate", "ScriptRunBehaviourLateUpdate");
            _fixedUpdate = TryCreate(ProfilerCategory.Scripts, "FixedUpdate.ScriptRunBehaviourFixedUpdate", "ScriptRunBehaviourFixedUpdate");
            _rendering = TryCreate(ProfilerCategory.Render, "Camera.Render", "Render Camera");
            _physics = TryCreate(ProfilerCategory.Physics, "FixedUpdate.PhysicsFixedUpdate", "Physics.Simulate");
            _scripts = TryCreate(ProfilerCategory.Scripts, "BehaviourUpdate");
            _animation = TryCreate(ProfilerCategory.Animation, "Animators.Update", "Directors.Evaluate");
            _gcCollect = TryCreate(ProfilerCategory.Internal, "GC.Collect");
        }

        public void Collect(FrameSnapshot snapshot)
        {
            snapshot.Cpu = new CpuTimingData
            {
                WasCollected = true,
                PlayerLoopMs = NsToMs(_playerLoop),
                UpdateMs = NsToMs(_update),
                LateUpdateMs = NsToMs(_lateUpdate),
                FixedUpdateMs = NsToMs(_fixedUpdate),
                RenderingMs = NsToMs(_rendering),
                PhysicsMs = NsToMs(_physics),
                ScriptsMs = NsToMs(_scripts),
                AnimationMs = NsToMs(_animation),
                GcCollectMs = NsToMs(_gcCollect)
            };
        }

        public void End()
        {
            _playerLoop.Dispose();
            _update.Dispose();
            _lateUpdate.Dispose();
            _fixedUpdate.Dispose();
            _rendering.Dispose();
            _physics.Dispose();
            _scripts.Dispose();
            _animation.Dispose();
            _gcCollect.Dispose();
        }

        static ProfilerRecorder TryCreate(ProfilerCategory category, params string[] markerNames)
        {
            foreach (var name in markerNames)
            {
                var rec = ProfilerRecorder.StartNew(category, name);
                if (rec.Valid) return rec;
                rec.Dispose();
            }
            return default;
        }

        static double NsToMs(ProfilerRecorder rec)
        {
            if (!rec.Valid || rec.LastValue <= 0) return 0;
            return rec.LastValue / 1_000_000.0;
        }
    }
}
