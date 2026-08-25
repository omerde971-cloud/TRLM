using System;
using UnityEngine;
using TRLM.AI.Perception;
using TRLM.Survival;

namespace TRLM.Companions
{
    /// <summary>
    /// One companion's situational awareness: nearby menacing predators, loud noises
    /// (gunfire), and hurt teammates. Deliberately sensor-only — CompanionAI reads the
    /// results to decide movement, CompanionLookAt reads them to decide gaze. All sensing
    /// runs on a staggered low-rate tick (not per-frame), and predators come from
    /// PredatorRegistry rather than scene scans.
    /// </summary>
    [RequireComponent(typeof(CompanionAI))]
    public class CompanionAwareness : MonoBehaviour
    {
        [Header("Threat Sensing")]
        [SerializeField] private float threatSenseRadius = 30f;
        [SerializeField] private float senseInterval = 0.4f;
        [Tooltip("Seconds the squad stays wary after the last menacing predator disappears from range.")]
        [SerializeField] private float threatLingerSeconds = 5f;

        [Header("Noise Reaction")]
        [Tooltip("NoiseEvents loudness at/above this reads as gunfire/explosion rather than footsteps.")]
        [SerializeField] private float gunshotLoudnessThreshold = 25f;
        [SerializeField] private float noiseMemorySeconds = 6f;

        [Header("Hurt Teammates")]
        [SerializeField] private float allyConcernSeconds = 8f;

        private CompanionAI self;
        private float senseTimer;
        private float threatMemoryTimer;
        private float noiseMemoryTimer;
        private float allyConcernTimer;

        public Transform ThreatTransform { get; private set; }
        public Vector3 ThreatPosition { get; private set; }
        public bool HasThreat => threatMemoryTimer > 0f;

        public Vector3 LastLoudNoisePosition { get; private set; }
        public bool HasRecentLoudNoise => noiseMemoryTimer > 0f;
        /// <summary>True if the most recent loud noise was gunfire-loud (vs. a heavy landing).</summary>
        public bool LastLoudNoiseWasGunfire { get; private set; }

        public Transform InjuredAlly { get; private set; }
        public bool HasInjuredAlly => allyConcernTimer > 0f && InjuredAlly != null;

        /// <summary>Fired when the squad member first notices a new threat (for audio/animation hooks).</summary>
        public event Action OnThreatNoticed;

        private void Awake()
        {
            self = GetComponent<CompanionAI>();
            // Stagger sensor ticks so four companions never scan on the same frame.
            senseTimer = (Mathf.Abs(GetInstanceID()) % 40) * 0.01f;
        }

        private void OnEnable() => NoiseEvents.OnNoise += HandleNoise;
        private void OnDisable() => NoiseEvents.OnNoise -= HandleNoise;

        private void Update()
        {
            if (threatMemoryTimer > 0f) threatMemoryTimer -= Time.deltaTime;
            if (noiseMemoryTimer > 0f) noiseMemoryTimer -= Time.deltaTime;
            if (allyConcernTimer > 0f) allyConcernTimer -= Time.deltaTime;

            senseTimer -= Time.deltaTime;
            if (senseTimer > 0f) return;
            senseTimer = senseInterval;

            SenseThreats();
        }

        private void SenseThreats()
        {
            if (self.IsDead) return;

            var predator = PredatorRegistry.FindNearest(transform.position, threatSenseRadius, menacingOnly: true);
            if (predator != null)
            {
                bool isNew = !HasThreat;
                ThreatTransform = predator.PredatorTransform;
                ThreatPosition = predator.PredatorTransform.position;
                threatMemoryTimer = threatLingerSeconds;
                if (isNew)
                {
                    OnThreatNoticed?.Invoke();
                    ShareThreatWithSquad(ThreatTransform, ThreatPosition);
                }
            }
            else if (ThreatTransform != null && threatMemoryTimer > 0f)
            {
                // Threat still remembered but no longer sensed: keep last known position, drop the live transform ref once it expires.
            }
            else
            {
                ThreatTransform = null;
            }
        }

        /// <summary>One companion spotting a wolf alerts the others (they turn to watch even if
        /// the wolf is outside their own sense radius) — squad-level "alert others to threats".</summary>
        private void ShareThreatWithSquad(Transform threat, Vector3 position)
        {
            var all = CompanionAI.All;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == null || other == self || other.IsDead) continue;
                var awareness = other.Awareness;
                if (awareness == null) continue;
                awareness.ReceiveSharedThreat(threat, position);
            }
        }

        public void ReceiveSharedThreat(Transform threat, Vector3 position)
        {
            if (HasThreat) return; // already tracking something
            ThreatTransform = threat;
            ThreatPosition = position;
            threatMemoryTimer = threatLingerSeconds;
            OnThreatNoticed?.Invoke();
        }

        private void HandleNoise(Vector3 position, float loudness)
        {
            if (loudness < gunshotLoudnessThreshold) return;
            // Ignore noise raised on top of us (our own squad's feet are not alarming).
            if ((position - transform.position).sqrMagnitude < 4f) return;

            LastLoudNoisePosition = position;
            LastLoudNoiseWasGunfire = loudness >= gunshotLoudnessThreshold;
            noiseMemoryTimer = noiseMemorySeconds;
        }

        /// <summary>Called by squadmates' HealthSystem hooks (wired in CompanionAI) when an ally takes damage.</summary>
        public void NotifyAllyHurt(Transform ally)
        {
            if (ally == null || ally == transform) return;
            InjuredAlly = ally;
            allyConcernTimer = allyConcernSeconds;
        }
    }
}
