using System;
using UnityEngine;
using TRLM.Core;
using TRLM.Survival;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Lightweight, NavMesh-free brain for a coiled snake. It stays put where the WildlifeSpawner
    /// drops it (spawner samples a valid NavMesh point, but the snake itself never moves along one),
    /// slowly turns to face the player when they come close — a defensive coil — and dies when shot.
    /// Deliberately cheap: no NavMeshAgent, no pathfinding, interval-staggered sensing. Implements
    /// IWildlifeAgent so the existing zone spawner treats it like any other species, and IDamageable
    /// so weapons can kill it.
    /// </summary>
    public class SnakeBehavior : MonoBehaviour, IWildlifeAgent, IDamageable
    {
        [Header("Perception")]
        [SerializeField] private float noticeDistance = 8f;
        [SerializeField] private float faceTurnSpeed = 2.5f;
        [SerializeField] private float senseInterval = 0.4f;

        [Header("Idle (subtle life without animation)")]
        [SerializeField] private float bobAmplitude = 0.015f;
        [SerializeField] private float bobSpeed = 1.3f;

        private WildlifeSpeciesProfile species;
        private HealthSystem health;
        private float senseTimer;
        private Transform player;
        private bool alerted;
        private float baseY;
        private float phase;

        public bool IsDead { get; private set; }
        public event Action OnDied;

        public void Initialize(WildlifeSpawnZone owningZone, WildlifeSpeciesProfile owningSpecies)
        {
            species = owningSpecies;
        }

        private void Awake()
        {
            health = GetComponent<HealthSystem>(); // optional
            senseTimer = (Mathf.Abs(GetInstanceID()) % 40) * 0.01f;
            phase = (Mathf.Abs(GetInstanceID()) % 628) * 0.01f;
        }

        private void Start() => baseY = transform.position.y;

        private void Update()
        {
            if (IsDead) return;

            senseTimer -= Time.deltaTime;
            if (senseTimer <= 0f)
            {
                senseTimer = senseInterval;
                var mgr = WildlifeSpawnManager.Instance;
                player = mgr != null ? mgr.Player : null;
                alerted = player != null &&
                          (player.position - transform.position).sqrMagnitude < noticeDistance * noticeDistance;
            }

            if (alerted && player != null)
            {
                Vector3 dir = player.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(dir), Time.deltaTime * faceTurnSpeed);
            }

            // Tiny vertical bob so a static coil still reads as alive.
            if (bobAmplitude > 0f)
            {
                phase += Time.deltaTime * bobSpeed;
                var p = transform.position;
                p.y = baseY + Mathf.Sin(phase) * bobAmplitude;
                transform.position = p;
            }
        }

        // ---------------------------------------------------------------- IDamageable

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDead) return;
            if (health != null)
            {
                health.TakeDamage(amount, source);
                if (!health.IsDead) return;
            }

            IsDead = true;
            OnDied?.Invoke();
            enabled = false;
        }

        public void Heal(float amount) => health?.Heal(amount);
    }
}
