using System;
using UnityEngine;

namespace TRLM.Survival
{
    /// <summary>
    /// Foundation for later false-perception content (distant whisper, branch crack, uncertain
    /// silhouette). Deliberately a separate, tiny static bus — NOT routed through NoiseEvents —
    /// so it is structurally impossible for a perception event to be picked up by WolfAI/wildlife
    /// as a real noise, and impossible for it to deal damage. Only two example kinds are wired
    /// (see PsychologicalState's occasional trigger); this is not a hallucination content library.
    /// </summary>
    public static class PerceptionEventSystem
    {
        public const string DistantBranchCrack = "DistantBranchCrack";
        public const string DistantWhisper = "DistantWhisper";

        /// <summary>(position, kind) — a future audio/VFX layer subscribes here. Never implies real threat.</summary>
        public static event Action<Vector3, string> OnPerceptionEvent;

        public static void Trigger(Vector3 position, string kind)
        {
            OnPerceptionEvent?.Invoke(position, kind);
        }
    }
}
