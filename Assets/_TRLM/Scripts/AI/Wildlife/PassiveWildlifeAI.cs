using System;
using UnityEngine;
using UnityEngine.AI;
using TRLM.AI.Perception;
using TRLM.Core;
using TRLM.Survival;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Prey-animal brain for deer/stag/fox: graze, wander, become alert, flee. No combat.
    /// Perception is deliberately cheap — distance ticks on an interval (staggered per
    /// instance), player from WildlifeSpawnManager, predators from PredatorRegistry, loud
    /// noises from the NoiseEvents bus. Night behavior comes from the species profile's
    /// activity multipliers at the spawner level; here, predator proximity makes prey relocate
    /// away entirely, so passive animals get scarce while wolves are hunting nearby.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PassiveWildlifeAI : MonoBehaviour, IDamageable, IWildlifeAgent
    {
        public enum State { Graze, Wander, Alert, Flee, Relocate }

        [Header("Speeds")]
        [SerializeField] private float wanderSpeed = 1.2f;
        [SerializeField] private float fleeSpeed = 6f;

        [Header("Perception")]
        [SerializeField] private float playerAlertDistance = 18f;
        [SerializeField] private float playerFleeDistance = 11f;
        [SerializeField] private float predatorFleeDistance = 26f;
        [SerializeField] private float senseInterval = 0.35f;

        [Header("Timers")]
        [SerializeField] private Vector2 grazeDurationRange = new Vector2(4f, 11f);
        [SerializeField] private float alertDuration = 2.2f;
        [SerializeField] private float fleeDistance = 30f;
        [SerializeField] private float relocateDistance = 45f;

        private NavMeshAgent agent;
        private HealthSystem health;
        private WildlifeSpawnZone territory;
        private WildlifeSpeciesProfile species;

        private State state = State.Graze;
        private float stateTimer;
        private float stateDuration;
        private float senseTimer;
        private Vector3 threatPoint;
        private bool threatIsPredator;

        public State CurrentState => state;
        public bool IsDead { get; private set; }
        public event Action<State> OnStateChanged;
        public event Action OnDied;

        public void Initialize(WildlifeSpawnZone owningZone, WildlifeSpeciesProfile owningSpecies)
        {
            territory = owningZone;
            species = owningSpecies;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthSystem>(); // optional
            senseTimer = (Mathf.Abs(GetInstanceID()) % 35) * 0.01f; // stagger sensor ticks
        }

        private void OnEnable() => NoiseEvents.OnNoise += HandleNoise;
        private void OnDisable() => NoiseEvents.OnNoise -= HandleNoise;

        private void Start() => EnterState(State.Graze);

        private void Update()
        {
            if (IsDead || !agent.isOnNavMesh) return;

            senseTimer -= Time.deltaTime;
            if (senseTimer <= 0f)
            {
                senseTimer = senseInterval;
                SenseThreats();
            }

            stateTimer += Time.deltaTime;
            switch (state)
            {
                case State.Graze:
                    if (stateTimer >= stateDuration) EnterState(State.Wander);
                    break;
                case State.Wander:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
                        EnterState(State.Graze);
                    break;
                case State.Alert:
                    FaceThreat();
                    if (stateTimer >= alertDuration)
                        EnterState(threatIsPredator ? State.Flee : State.Graze);
                    break;
                case State.Flee:
                case State.Relocate:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                        EnterState(State.Graze);
                    break;
            }
        }

        private void SenseThreats()
        {
            // Predators trump the player: prey clears out of an active hunt's whole area.
            var predator = PredatorRegistry.FindNearest(transform.position, predatorFleeDistance, menacingOnly: false);
            if (predator != null)
            {
                threatPoint = predator.PredatorTransform.position;
                threatIsPredator = true;
                if (state != State.Flee && state != State.Relocate) EnterState(State.Relocate);
                return;
            }

            var manager = WildlifeSpawnManager.Instance;
            Transform player = manager != null ? manager.Player : null;
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist < playerFleeDistance)
            {
                threatPoint = player.position;
                threatIsPredator = false;
                if (state != State.Flee) EnterState(State.Flee);
            }
            else if (dist < playerAlertDistance && (state == State.Graze || state == State.Wander))
            {
                threatPoint = player.position;
                threatIsPredator = false;
                EnterState(State.Alert);
            }
        }

        private void HandleNoise(Vector3 position, float loudness)
        {
            if (IsDead) return;
            float dist = Vector3.Distance(transform.position, position);
            if (dist > loudness * 1.4f) return; // prey hears further than it needs to
            threatPoint = position;
            threatIsPredator = loudness >= 25f; // gunfire scatters prey hard
            if (state != State.Flee && state != State.Relocate)
                EnterState(threatIsPredator ? State.Flee : State.Alert);
        }

        private void EnterState(State next)
        {
            state = next;
            stateTimer = 0f;
            OnStateChanged?.Invoke(next);

            switch (next)
            {
                case State.Graze:
                    agent.isStopped = true;
                    stateDuration = UnityEngine.Random.Range(grazeDurationRange.x, grazeDurationRange.y);
                    break;
                case State.Wander:
                    agent.isStopped = false;
                    agent.speed = wanderSpeed;
                    SetDestinationSafe(territory != null
                        ? territory.GetRandomPointInZone()
                        : transform.position + RandomFlat(8f));
                    break;
                case State.Alert:
                    agent.isStopped = true;
                    break;
                case State.Flee:
                    agent.isStopped = false;
                    agent.speed = fleeSpeed;
                    SetDestinationSafe(AwayFromThreat(fleeDistance));
                    break;
                case State.Relocate:
                    agent.isStopped = false;
                    agent.speed = fleeSpeed * 0.85f;
                    SetDestinationSafe(AwayFromThreat(relocateDistance));
                    break;
            }
        }

        private Vector3 AwayFromThreat(float distance)
        {
            Vector3 dir = transform.position - threatPoint;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.04f) dir = RandomFlat(1f);
            // Small random spread so a fleeing herd fans out instead of stacking on one line.
            dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-25f, 25f), 0f) * dir.normalized;
            return transform.position + dir * distance;
        }

        private static Vector3 RandomFlat(float radius)
        {
            Vector2 c = UnityEngine.Random.insideUnitCircle.normalized * radius;
            return new Vector3(c.x, 0f, c.y);
        }

        private void FaceThreat()
        {
            Vector3 dir = threatPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);
        }

        private void SetDestinationSafe(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        // ---------------------------------------------------------------- IDamageable

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDead) return;
            if (health != null)
            {
                health.TakeDamage(amount, source);
                if (!health.IsDead)
                {
                    // Shot at and survived: bolt immediately.
                    if (source != null) threatPoint = source.transform.position;
                    threatIsPredator = true;
                    EnterState(State.Flee);
                    return;
                }
            }

            IsDead = true;
            agent.isStopped = true;
            OnDied?.Invoke();
            enabled = false;
        }

        public void Heal(float amount) => health?.Heal(amount);
    }
}
