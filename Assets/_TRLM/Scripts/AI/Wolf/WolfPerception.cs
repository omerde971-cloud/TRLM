using UnityEngine;
using TRLM.AI.Perception;
using TRLM.AI.Human;
using TRLM.Companions;
using TRLM.Core;

namespace TRLM.AI.Wolf
{
    /// <summary>
    /// Sight + sound perception for one wolf. Sight uses distance, field-of-view, and a
    /// line-of-sight raycast (so a wolf can't see through a wall/rock). Sound subscribes to
    /// the shared NoiseEvents bus. Crouching reduces both detection range and how far
    /// footstep noise travels (handled on the emitter side via PlayerNoiseEmitter), so its
    /// effect here is just "the player is harder to spot," not a separate code path.
    /// </summary>
    public class WolfPerception : MonoBehaviour
    {
        [SerializeField] private float sightRange = 22f;
        [SerializeField] private float sightAngleDegrees = 140f;
        [SerializeField] private float hearingRadiusMultiplier = 1.1f;
        [SerializeField] private LayerMask sightBlockingMask = ~0;
        [SerializeField] private float noiseMemorySeconds = 8f;

        /// <summary>Weather hook (Sprint 09): heavy rain/storm dulls hearing a little. WeatherSystem
        /// sets this once on weather change, not per-frame; every wolf reads the same value instead
        /// of each wolf polling weather state itself. 1 = unaffected.</summary>
        public static float WeatherHearingMultiplier = 1f;

        private Transform player;
        private Transform visibleTarget;
        private float noiseMemoryTimer;

        public Vector3 LastHeardNoisePosition { get; private set; }
        public float LastHeardNoiseLoudness { get; private set; }
        public bool HasRecentNoise => noiseMemoryTimer > 0f;
        public Vector3 LastKnownPlayerPosition { get; private set; }
        public Vector3 LastKnownTargetPosition { get; private set; }
        public Transform VisibleTarget => visibleTarget;

        private void OnEnable() => NoiseEvents.OnNoise += HandleNoise;
        private void OnDisable() => NoiseEvents.OnNoise -= HandleNoise;

        private void Awake()
        {
            var manager = TRLM.AI.Wildlife.WildlifeSpawnManager.Instance;
            player = manager != null ? manager.Player : null;
        }

        private void Update()
        {
            if (noiseMemoryTimer > 0f) noiseMemoryTimer -= Time.deltaTime;
        }

        private void HandleNoise(Vector3 position, float loudness)
        {
            float dist = Vector3.Distance(transform.position, position);
            float aggression = Mathf.Max(0f, TRLM.Progression.DifficultySettings.WildlifeAggressionMultiplier);
            if (dist <= loudness * hearingRadiusMultiplier * WeatherHearingMultiplier * aggression)
            {
                LastHeardNoisePosition = position;
                LastHeardNoiseLoudness = loudness;
                noiseMemoryTimer = noiseMemorySeconds;
            }
        }

        public bool CanSeePlayer(out Vector3 playerPosition)
        {
            bool canSee = CanSeeTarget(out Transform target, out Vector3 targetPosition);
            if (canSee && target == player)
            {
                playerPosition = targetPosition;
                return true;
            }

            playerPosition = default;
            return false;
        }

        public bool CanSeeTarget(out Transform target, out Vector3 targetPosition)
        {
            target = null;
            targetPosition = default;
            visibleTarget = null;

            if (TrySeeTransform(player, out targetPosition))
            {
                target = player;
                visibleTarget = target;
                LastKnownPlayerPosition = targetPosition;
                LastKnownTargetPosition = targetPosition;
                return true;
            }

            // Sprint 2 perf: companions come from the static registry instead of a
            // FindObjectsByType scene scan on every perception query.
            var companions = CompanionAI.All;
            Transform best = null;
            Vector3 bestPosition = default;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < companions.Count; i++)
            {
                var companion = companions[i];
                if (companion == null || companion.IsDead) continue;
                Transform candidate = companion.transform;
                if (!TrySeeTransform(candidate, out Vector3 candidatePosition)) continue;

                float sqrDistance = (candidatePosition - transform.position).sqrMagnitude;
                if (sqrDistance >= bestDistance) continue;
                bestDistance = sqrDistance;
                best = candidate;
                bestPosition = candidatePosition;
            }

            // Island-security soldiers are prey too, so predators and armed guards actually fight.
            var soldiers = SoldierAI.All;
            for (int i = 0; i < soldiers.Count; i++)
            {
                var soldier = soldiers[i];
                if (soldier == null || soldier.IsDead) continue;
                Transform candidate = soldier.transform;
                if (!TrySeeTransform(candidate, out Vector3 candidatePosition)) continue;

                float sqrDistance = (candidatePosition - transform.position).sqrMagnitude;
                if (sqrDistance >= bestDistance) continue;
                bestDistance = sqrDistance;
                best = candidate;
                bestPosition = candidatePosition;
            }

            if (best == null) return false;

            target = best;
            targetPosition = bestPosition;
            visibleTarget = best;
            LastKnownTargetPosition = bestPosition;
            return true;
        }

        public bool CanSeeTargetTransform(Transform target, out Vector3 targetPosition)
        {
            if (TrySeeTransform(target, out targetPosition))
            {
                visibleTarget = target;
                if (target == player) LastKnownPlayerPosition = targetPosition;
                LastKnownTargetPosition = targetPosition;
                return true;
            }

            return false;
        }

        private bool TrySeeTransform(Transform candidate, out Vector3 candidatePosition)
        {
            candidatePosition = default;
            if (candidate == null) return false;

            var damageable = candidate.GetComponentInChildren<IDamageable>() ?? candidate.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsDead) return false;

            Vector3 toTarget = candidate.position - transform.position;
            float dist = toTarget.magnitude;
            if (dist > sightRange) return false;

            float angle = Vector3.Angle(transform.forward, toTarget);
            if (angle > sightAngleDegrees * 0.5f) return false;

            Vector3 eyeHeight = Vector3.up * 0.6f;
            if (Physics.Linecast(transform.position + eyeHeight, candidate.position + Vector3.up * 0.9f, out RaycastHit hit, sightBlockingMask))
            {
                if (!hit.transform.IsChildOf(candidate)) return false; // something solid is in the way
            }

            candidatePosition = candidate.position;
            return true;
        }
    }
}
