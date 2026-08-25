using System.Collections.Generic;
using UnityEngine;
using TRLM.Survival;

namespace TRLM.World
{
    /// <summary>
    /// Put on the same GameObject as a RockfallZone. Listens for OnRockImpact and damages the
    /// player when a pooled rock hits them hard enough. Debounced per-rock so continuous physics
    /// contact (resting on the player, multiple sub-steps) doesn't repeatedly deal damage.
    /// </summary>
    [RequireComponent(typeof(RockfallZone))]
    public class RockfallPlayerDamage : MonoBehaviour
    {
        [SerializeField] private float minImpactSpeed = 3f; // relativeVelocity.magnitude below this does nothing
        [SerializeField] private float damagePerImpactSpeedUnit = 4f;
        [SerializeField] private float perRockHitCooldownSeconds = 1f;

        private RockfallZone zone;
        private readonly Dictionary<int, float> lastHitTimeByRockId = new Dictionary<int, float>();

        private void Awake()
        {
            zone = GetComponent<RockfallZone>();
        }

        private void OnEnable()
        {
            zone.OnRockImpact += HandleRockImpact;
        }

        private void OnDisable()
        {
            zone.OnRockImpact -= HandleRockImpact;
        }

        private void HandleRockImpact(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            if (!collision.collider.transform.root.CompareTag("Player")) return;

            var health = collision.collider.GetComponentInParent<HealthSystem>();
            if (health == null) return;

            int rockId = collision.gameObject.GetInstanceID();
            if (lastHitTimeByRockId.TryGetValue(rockId, out float lastTime) && Time.time - lastTime < perRockHitCooldownSeconds)
                return;
            lastHitTimeByRockId[rockId] = Time.time;

            float applied = speed * damagePerImpactSpeedUnit * Mathf.Max(0f, TRLM.Progression.DifficultySettings.PlayerDamageMultiplier);
            health.TakeDamage(applied, gameObject);
        }
    }
}
