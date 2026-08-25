using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TRLM.AI.Perception;
using TRLM.Core;
using TRLM.Progression;
using TRLM.Survival;

namespace TRLM.AI.Human
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthSystem))]
    public class SoldierAI : MonoBehaviour, IDamageable
    {
        public enum State { Patrol, Suspicious, Investigate, Alert, Combat, Search, Return }

        /// <summary>Live soldiers, so predators (WolfPerception) can treat them as prey without a
        /// scene scan. Maintained in OnEnable/OnDisable, mirroring CompanionAI.All.</summary>
        public static readonly List<SoldierAI> All = new List<SoldierAI>();

        [Header("Route")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float patrolWaitSeconds = 2f;

        [Header("Perception")]
        [SerializeField] private Transform eye;
        [SerializeField] private float sightRange = 24f;
        [SerializeField] private float sightAngleDegrees = 95f;
        [SerializeField] private float hearingMemorySeconds = 5f;
        [SerializeField] private LayerMask sightBlockingMask = ~0;

        [Header("Combat")]
        [SerializeField] private float combatRange = 18f;
        [SerializeField] private float attackCooldownSeconds = 1.2f;
        [SerializeField] private float damage = 8f;
        [SerializeField] private float searchDurationSeconds = 7f;

        private NavMeshAgent agent;
        private HealthSystem health;
        private Transform target;
        private Vector3 startPosition;
        private Vector3 startForward;
        private Vector3 lastKnownTargetPosition;
        private Vector3 heardPosition;
        private float heardTimer;
        private float stateTimer;
        private float attackTimer;
        private int patrolIndex;
        private Transform playerTransform;
        private float retargetTimer;

        public State CurrentState { get; private set; } = State.Patrol;
        public bool IsDead => health != null && health.IsDead;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthSystem>();
            startPosition = transform.position;
            startForward = transform.forward;
            if (eye == null) eye = transform;
        }

        private void OnEnable()
        {
            NoiseEvents.OnNoise += HandleNoise;
            if (health != null) health.OnDeath += HandleDeath;
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            NoiseEvents.OnNoise -= HandleNoise;
            if (health != null) health.OnDeath -= HandleDeath;
            All.Remove(this);
        }

        private void Start()
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            target = playerTransform;
            EnterState(State.Patrol);
        }

        /// <summary>Island security is hostile to intruders (the player) AND to predators. Pick the
        /// closest hostile so a soldier engages a charging wolf/bear as readily as the player.</summary>
        private void RefreshHostileTarget()
        {
            var predator = PredatorRegistry.FindNearest(transform.position, sightRange, menacingOnly: false);
            Transform predatorT = predator != null ? predator.PredatorTransform : null;

            if (predatorT == null) { if (target == null) target = playerTransform; return; }
            if (playerTransform == null) { target = predatorT; return; }

            float dPred = (predatorT.position - transform.position).sqrMagnitude;
            float dPlayer = (playerTransform.position - transform.position).sqrMagnitude;
            target = dPred < dPlayer ? predatorT : playerTransform;
        }

        private void Update()
        {
            if (IsDead || agent == null || !agent.isOnNavMesh) return;

            if (heardTimer > 0f) heardTimer -= Time.deltaTime;
            if (attackTimer > 0f) attackTimer -= Time.deltaTime;
            stateTimer += Time.deltaTime;

            // Re-pick the closest hostile (player or nearest predator) a few times a second so a
            // soldier will break off to fight an approaching wolf/bear.
            retargetTimer -= Time.deltaTime;
            if (retargetTimer <= 0f) { retargetTimer = 0.35f; RefreshHostileTarget(); }

            if (CanSeeTarget(out Vector3 seenPosition))
            {
                lastKnownTargetPosition = seenPosition;
                if (CurrentState != State.Combat && CurrentState != State.Alert)
                    EnterState(State.Alert);
            }

            switch (CurrentState)
            {
                case State.Patrol: TickPatrol(); break;
                case State.Suspicious: TickSuspicious(); break;
                case State.Investigate: TickInvestigate(); break;
                case State.Alert: TickAlert(); break;
                case State.Combat: TickCombat(); break;
                case State.Search: TickSearch(); break;
                case State.Return: TickReturn(); break;
            }
        }

        private void EnterState(State next)
        {
            CurrentState = next;
            stateTimer = 0f;

            switch (next)
            {
                case State.Patrol:
                    agent.isStopped = false;
                    SetNextPatrolDestination();
                    break;
                case State.Suspicious:
                    agent.isStopped = true;
                    break;
                case State.Investigate:
                    agent.isStopped = false;
                    SetDestinationSafe(heardPosition);
                    break;
                case State.Alert:
                    agent.isStopped = true;
                    break;
                case State.Combat:
                    agent.isStopped = false;
                    break;
                case State.Search:
                    agent.isStopped = false;
                    SetDestinationSafe(lastKnownTargetPosition);
                    break;
                case State.Return:
                    agent.isStopped = false;
                    SetDestinationSafe(startPosition);
                    break;
            }
        }

        private void TickPatrol()
        {
            if (heardTimer > 0f) { EnterState(State.Suspicious); return; }
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f && stateTimer >= patrolWaitSeconds)
                SetNextPatrolDestination();
        }

        private void TickSuspicious()
        {
            FaceTowards(heardPosition);
            if (stateTimer >= 1.25f) EnterState(State.Investigate);
        }

        private void TickInvestigate()
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.4f)
                EnterState(State.Search);
        }

        private void TickAlert()
        {
            FaceTowards(lastKnownTargetPosition);
            if (stateTimer >= 0.8f) EnterState(State.Combat);
        }

        private void TickCombat()
        {
            if (!CanSeeTarget(out Vector3 seenPosition))
            {
                EnterState(State.Search);
                return;
            }

            lastKnownTargetPosition = seenPosition;
            float distance = Vector3.Distance(transform.position, seenPosition);
            if (distance > combatRange * 0.85f)
                SetDestinationSafe(seenPosition);
            else
                agent.isStopped = true;

            FaceTowards(seenPosition);
            if (distance <= combatRange && attackTimer <= 0f)
            {
                attackTimer = attackCooldownSeconds;
                var damageable = target != null ? target.GetComponentInChildren<IDamageable>() : null;
                damageable?.TakeDamage(damage * Mathf.Max(0f, DifficultySettings.EnemyDamageMultiplier), gameObject);
            }
        }

        private void TickSearch()
        {
            if (heardTimer > 0f) { EnterState(State.Investigate); return; }
            if (stateTimer >= searchDurationSeconds) EnterState(State.Return);
        }

        private void TickReturn()
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
            {
                transform.rotation = Quaternion.LookRotation(startForward, Vector3.up);
                EnterState(State.Patrol);
            }
        }

        private void SetNextPatrolDestination()
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            patrolIndex = Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1);
            SetDestinationSafe(patrolPoints[patrolIndex].position);
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }

        private bool CanSeeTarget(out Vector3 targetPosition)
        {
            targetPosition = default;
            if (target == null) return false;

            Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 1.65f;
            Vector3 toTarget = target.position - origin;
            if (toTarget.magnitude > sightRange) return false;
            if (Vector3.Angle(transform.forward, toTarget) > sightAngleDegrees * 0.5f) return false;

            Vector3 aimPoint = target.position + Vector3.up * 0.9f;
            if (Physics.Linecast(origin, aimPoint, out RaycastHit hit, sightBlockingMask))
            {
                if (!hit.transform.IsChildOf(target)) return false;
            }

            targetPosition = target.position;
            return true;
        }

        private void HandleNoise(Vector3 position, float loudness)
        {
            if (IsDead) return;
            float distance = Vector3.Distance(transform.position, position);
            if (distance > loudness) return;

            heardPosition = position;
            heardTimer = hearingMemorySeconds;
            if (CurrentState == State.Patrol || CurrentState == State.Return)
                EnterState(State.Suspicious);
        }

        private void SetDestinationSafe(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        private void FaceTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 6f);
        }

        private void HandleDeath()
        {
            if (agent != null && agent.enabled)
                agent.isStopped = true;
            enabled = false;
        }

        public void TakeDamage(float amount, GameObject source = null)
        {
            health.TakeDamage(amount, source);
            if (source != null)
            {
                target = source.transform;
                lastKnownTargetPosition = source.transform.position;
                if (!IsDead) EnterState(State.Alert);
            }
        }

        public void Heal(float amount) => health.Heal(amount);
    }
}
