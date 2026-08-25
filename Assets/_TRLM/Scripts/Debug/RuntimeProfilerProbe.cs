using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace TRLM.Debugging
{
    /// <summary>
    /// Temporary play-mode measurement probe: samples key profiler counters for a fixed
    /// number of frames, then logs one summary line and destroys itself. Spawned on demand
    /// from editor tooling (never present in scenes/builds).
    /// </summary>
    public class RuntimeProfilerProbe : MonoBehaviour
    {
        public int sampleFrames = 150;

        private ProfilerRecorder mainThread;
        private ProfilerRecorder behaviourUpdate;
        private ProfilerRecorder gcAlloc;
        private ProfilerRecorder physics;
        private ProfilerRecorder animation;

        private int frames;
        private double mainSum, updateSum, physicsSum, animSum;
        private long gcSum, gcMax;

        private void OnEnable()
        {
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            behaviourUpdate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate", 1);
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            physics = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "FixedUpdate.PhysicsFixedUpdate", 1);
            animation = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Update.DirectorUpdateAnimationBegin", 1);
        }

        private void Update()
        {
            frames++;
            if (mainThread.Valid) mainSum += mainThread.LastValue / 1e6;       // ns -> ms
            if (behaviourUpdate.Valid) updateSum += behaviourUpdate.LastValue / 1e6;
            if (physics.Valid) physicsSum += physics.LastValue / 1e6;
            if (animation.Valid) animSum += animation.LastValue / 1e6;
            if (gcAlloc.Valid)
            {
                gcSum += gcAlloc.LastValue;
                if (gcAlloc.LastValue > gcMax) gcMax = gcAlloc.LastValue;
            }

            if (frames >= sampleFrames)
            {
                var sb = new StringBuilder("[RuntimeProfilerProbe] ");
                sb.Append("frames=").Append(frames);
                sb.Append(" mainThreadAvgMs=").Append((mainSum / frames).ToString("F2"));
                sb.Append(" scriptUpdateAvgMs=").Append((updateSum / frames).ToString("F3"));
                sb.Append(" physicsAvgMs=").Append((physicsSum / frames).ToString("F3"));
                sb.Append(" animBeginAvgMs=").Append((animSum / frames).ToString("F3"));
                sb.Append(" gcPerFrameAvgB=").Append((gcSum / frames));
                sb.Append(" gcPerFrameMaxB=").Append(gcMax);
                Debug.Log(sb.ToString());
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            mainThread.Dispose();
            behaviourUpdate.Dispose();
            gcAlloc.Dispose();
            physics.Dispose();
            animation.Dispose();
        }
    }
}
