using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.AI;
using TRLM.Core;
using TRLM.Survival;
using TRLM.AI.Wildlife;
using TRLM.AI.Perception;

namespace TRLM.AI.Wolf
{
    /// <summary>
    /// First-pass wolf state machine. Movement/perception/attack are fully functional;
    /// there is deliberately no Animator here — see class remarks in WildlifeSystem.md.
    /// The wolf model (Assets/ThirdParty/Animals/Wolf_CC0) has no rig or animation clips
    /// at all (confirmed in the P0 asset audit), so this drives a plain transform via
    /// NavMeshAgent. Visually it will slide rather than walk/run/attack-animate until a
    /// rigged wolf asset replaces it or this one is rigged from scratch — reported per
    /// sprint instructions rather than silently faked or allowed to block the sprint.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(WolfPerception))]
    [RequireComponent(typeof(HealthSystem))]
    public class WolfAI : MonoBehaviour, IDamageable, IPredator, IWildlifeAgent
    {
        public enum State { Idle, Roam, Investigate, Alert, Stalk, Chase, Attack, Retreat, ReturnToTerritory }

        [Header("Speeds")]
        [SerializeField] private float roamSpeed = 1.4f;
        [SerializeField] private float stalkSpeed = 1.8f;
        [SerializeField] private float chaseSpeed = 5.5f;

        [Header("Timers")]
        [SerializeField] private float idleDuration = 4f;
        [SerializeField] private float investigateTimeout = 8f;
        [SerializeField] private float alertDuration = 2f;
        [SerializeField] private float stalkMinDuration = 3f;
        [SerializeField] private float maxChaseSeconds = 20f;
        [SerializeField] private float retreatDuration = 6f;

        [Header("Ranges")]
        [SerializeField] private float stalkDistance = 12f;
        [SerializeField] private float attackRange = 2.2f;
        [SerializeField] private float leashDistanceFromTerritory = 70f;

        [Header("Attack")]
        [SerializeField] private float attackWindupSeconds = 0.5f;
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float attackCooldownSeconds = 1.6f;
        [SerializeField] private float targetMemorySeconds = 4f;

        [Header("Pack")]
        [SerializeField] private float packAlertRadius = 30f;
        [SerializeField] private int maxSimultaneousAttackers = 2;

        [Header("Pathing")]
        [Tooltip("How often Stalk/Chase recompute their NavMesh destination, matching CompanionAI's " +
                 "repath-throttle pattern instead of calling SetDestination every frame.")]
        [SerializeField] private float stalkRepathInterval = 0.2f;
        [SerializeField] private float chaseRepathInterval = 0.1f;

        private static readonly List<WolfAI> allWolves = new List<WolfAI>();
        private static readonly HashSet<WolfAI> currentAttackers = new HashSet<WolfAI>();

        private NavMeshAgent agent;
        private WolfPerception perception;
        private HealthSystem health;
        private WildlifeSpawnZone territory;
        private WildlifeSpeciesProfile species;

        private State state = State.Idle;
        private float stateTimer;
        private float chaseTimer;
        private float attackCooldownTimer;
        private bool attackWindingUp;
        private Vector3 investigateTarget;
        private Vector3 territoryCenter;
        private float repathTimer;
        private Transform committedTarget;
        private Vector3 committedTargetPosition;
        private float targetMemoryTimer;

        public State CurrentState => state;
        public bool IsDead { get; private set; }
        public event Action<State> OnStateChanged;
        public event Action OnAttackCommitted;
        public event Action<float> OnDamaged;

        /// <summary>Live wolves in the scene, for pack queries and passive-wildlife predator checks.</summary>
        public static IReadOnlyList<WolfAI> All => allWolves;

        // ---------------------------------------------------------------- IPredator (companion squad awareness)
        public Transform PredatorTransform => transform;
        public bool IsMenacing => state == State.Alert || state == State.Stalk || state == State.Chase || state == State.Attack;
        public bool IsDeadPredator => IsDead;

        /// <summary>Fraction of max health below which the wolf fights shy: it disengages, limps,
        /// and won't re-commit to a stalk for woundedWarySeconds.</summary>
        [Header("Injury")]
        [SerializeField] private float woundedHealthFraction = 0.35f;
        [SerializeField] private float woundedWarySeconds = 14f;
        [SerializeField] private float woundedSpeedFactor = 0.7f;

        private float woundedWaryTimer;
        private bool IsWounded => health != null && health.CurrentHealth <= health.MaxHealth * woundedHealthFraction;

        /// <summary>Night multiplier from the species profile — wolves commit to chases faster
        /// and range harder after dark. 1 when no profile/manager present.</summary>
        private float NightAggression
        {
            get
            {
                var mgr = WildlifeSpawnManager.Instance;
                if (mgr == null || species == null) return 1f;
                return mgr.IsNight ? Mathf.Max(0.1f, species.nightActivityMultiplier) : Mathf.Max(0.1f, species.dayActivityMultiplier);
            }
        }

        public void Initialize(WildlifeSpawnZone owningZone, WildlifeSpeciesProfile owningSpecies)
        {
            territory = owningZone;
            species = owningSpecies;
            territoryCenter = owningZone != null ? owningZone.Center : transform.position;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            perception = GetComponent<WolfPerception>();
            health = GetComponent<HealthSystem>();
            territoryCenter = transform.position;
        }

        private void OnEnable()
        {
            allWolves.Add(this);
            PredatorRegistry.Register(this);
        }

        private void OnDisable()
        {
            allWolves.Remove(this);
            currentAttackers.Remove(this);
            PredatorRegistry.Unregister(this);
        }

        private void Start() => EnterState(State.Idle);

        private void Update()
        {
            if (IsDead) return;
            if (!agent.isOnNavMesh) return;

            if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;
            if (repathTimer > 0f) repathTimer -= Time.deltaTime;
            if (woundedWaryTimer > 0f) woundedWaryTimer -= Time.deltaTime;

            switch (state)
            {
                case State.Idle: TickIdle(); break;
                case State.Roam: TickRoam(); break;
                case State.Investigate: TickInvestigate(); break;
                case State.Alert: TickAlert(); break;
                case State.Stalk: TickStalk(); break;
                case State.Chase: TickChase(); break;
                case State.Attack: TickAttack(); break;
                case State.Retreat: TickRetreat(); break;
                case State.ReturnToTerritory: TickReturnToTerritory(); break;
            }
        }

        // ---------------------------------------------------------------- state entry

        private void EnterState(State next)
        {
            state = next;
            stateTimer = 0f;
            OnStateChanged?.Invoke(state);

            switch (next)
            {
                case State.Idle:
                    agent.isStopped = true;
                    ClearCommittedTarget();
                    break;
                case State.Roam:
                    agent.isStopped = false;
                    agent.speed = roamSpeed;
                    ClearCommittedTarget();
                    SetDestinationSafe(territory != null ? territory.GetRandomPointInZone() : RandomPointAround(territoryCenter, 15f));
                    break;
                case State.Investigate:
                    agent.isStopped = false;
                    agent.speed = roamSpeed * 1.2f;
                    investigateTarget = perception.LastHeardNoisePosition;
                    SetDestinationSafe(investigateTarget);
                    break;
                case State.Alert:
                    agent.isStopped = true;
                    break;
                case State.Stalk:
                    agent.isStopped = false;
                    agent.speed = stalkSpeed;
                    repathTimer = 0f; // repath immediately on entry, then throttle
                    break;
                case State.Chase:
                    agent.isStopped = false;
                    agent.speed = chaseSpeed;
                    chaseTimer = 0f;
                    repathTimer = 0f; // repath immediately on entry, then throttle
                    AlertNearbyPack();
                    break;
                case State.Attack:
                    agent.isStopped = true;
                    attackWindingUp = true;
                    stateTimer = 0f;
                    currentAttackers.Add(this);
                    break;
                case State.Retreat:
                    agent.isStopped = false;
                    agent.speed = roamSpeed * 1.3f;
                    currentAttackers.Remove(this);
                    SetDestinationSafe(RandomPointAround(transform.position, 10f, awayFrom: perception.VisibleTarget));
                    break;
                case State.ReturnToTerritory:
                    agent.isStopped = false;
                    agent.speed = roamSpeed;
                    SetDestinationSafe(territoryCenter);
                    break;
            }
        }

        // ---------------------------------------------------------------- per-state ticks

        private void TickIdle()
        {
            stateTimer += Time.deltaTime;
            if (!IsWary && AcquireTarget()) { EnterState(State.Alert); return; }
            if (perception.HasRecentNoise) { ReactToNoise(); return; }
            if (stateTimer >= idleDuration) EnterState(State.Roam);
        }

        private void TickRoam()
        {
            if (!IsWary && AcquireTarget()) { EnterState(State.Alert); return; }
            if (perception.HasRecentNoise) { ReactToNoise(); return; }
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) EnterState(State.Idle);
        }

        /// <summary>Wounded/wary wolves stop hunting for a while — they still flee gunfire.</summary>
        private bool IsWary => woundedWaryTimer > 0f;

        /// <summary>Sprint 2: a wolf that hears gunfire-loud noise flees by day when alone, but a
        /// pack — or the night activity bonus — makes it bold enough to investigate instead.</summary>
        private void ReactToNoise()
        {
            bool gunshot = perception.LastHeardNoiseLoudness >= 25f;
            bool bold = NearbyAllyCount(packAlertRadius) > 0 || NightAggression > 1f;
            if (gunshot && (!bold || IsWary))
            {
                EnterState(State.Retreat);
                return;
            }
            if (!IsWary) EnterState(State.Investigate);
        }

        private void TickInvestigate()
        {
            stateTimer += Time.deltaTime;
            if (AcquireTarget()) { EnterState(State.Alert); return; }
            if (stateTimer >= investigateTimeout) { EnterState(State.Roam); return; }
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f) EnterState(State.Roam);
        }

        private void TickAlert()
        {
            stateTimer += Time.deltaTime;
            if (TryGetCommittedTargetPosition(out Vector3 targetPos))
            {
                FaceTowards(targetPos);
                if (stateTimer >= alertDuration) EnterState(State.Stalk);
            }
            else if (stateTimer >= alertDuration * 1.5f)
            {
                EnterState(State.Roam);
            }
        }

        private void TickStalk()
        {
            stateTimer += Time.deltaTime;
            if (!TryGetCommittedTargetPosition(out Vector3 targetPos))
            {
                if (stateTimer >= stalkMinDuration) EnterState(State.Retreat);
                return;
            }

            float dist = Vector3.Distance(transform.position, targetPos);
            if (repathTimer <= 0f)
            {
                repathTimer = stalkRepathInterval;
                SetDestinationSafe(targetPos + (transform.position - targetPos).normalized * stalkDistance * 0.5f);
            }

            // A wounded/wary wolf breaks off the stalk instead of committing.
            if (IsWounded || IsWary) { EnterState(State.Retreat); return; }

            // Night aggression: the species' night multiplier scales the commit roll, so wolves
            // that stalk cautiously by day close in decisively after dark.
            bool aggressive = species != null && UnityEngine.Random.value < species.aggressionModifier * NightAggression * Time.deltaTime;
            bool packBonus = NearbyAllyCount(packAlertRadius) > 0; // packs commit faster than lone wolves
            float commitDistance = stalkDistance * (NightAggression > 1f ? 1.25f : 1f);
            if (stateTimer >= stalkMinDuration && (dist < commitDistance || aggressive || packBonus))
                EnterState(State.Chase);
        }

        private void TickChase()
        {
            chaseTimer += Time.deltaTime;

            if (!TryGetCommittedTargetPosition(out Vector3 targetPos))
            {
                targetPos = committedTargetPosition;
                if (Vector3.Distance(transform.position, targetPos) < 1f) { EnterState(State.Retreat); return; }
            }

            float distFromTerritory = Vector3.Distance(targetPos, territoryCenter);
            if (distFromTerritory > leashDistanceFromTerritory) { EnterState(State.Retreat); return; }
            if (chaseTimer > maxChaseSeconds) { EnterState(State.Retreat); return; }

            if (repathTimer <= 0f)
            {
                repathTimer = chaseRepathInterval;
                SetDestinationSafe(PackFlankOffset(targetPos));
            }

            float dist = Vector3.Distance(transform.position, targetPos);
            if (dist <= attackRange && currentAttackers.Count < maxSimultaneousAttackers)
                EnterState(State.Attack);
        }

        private void TickAttack()
        {
            stateTimer += Time.deltaTime;

            if (!TryGetCommittedTargetPosition(out Vector3 targetPos)) { EnterState(State.Chase); return; }
            FaceTowards(targetPos);

            float dist = Vector3.Distance(transform.position, targetPos);
            if (dist > attackRange * 1.3f) { EnterState(State.Chase); return; }

            if (attackWindingUp)
            {
                if (stateTimer >= attackWindupSeconds)
                {
                    attackWindingUp = false;
                    stateTimer = 0f;
                    TryDealDamage(committedTarget, targetPos);
                }
                return;
            }

            if (attackCooldownTimer <= 0f)
            {
                attackWindingUp = true;
                stateTimer = 0f;
            }
        }

        private void TickRetreat()
        {
            stateTimer += Time.deltaTime;
            if (TryGetCommittedTargetPosition(out _) && stateTimer < retreatDuration * 0.3f) return; // keep distance briefly even if still visible
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) { EnterState(State.ReturnToTerritory); return; }
            if (stateTimer >= retreatDuration) EnterState(State.ReturnToTerritory);
        }

        private void TickReturnToTerritory()
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) EnterState(State.Idle);
        }

        // ---------------------------------------------------------------- attack / damage

        private bool AcquireTarget()
        {
            if (TryGetCommittedTargetPosition(out _)) return true;
            if (!perception.CanSeeTarget(out Transform target, out Vector3 targetPosition)) return false;

            committedTarget = target;
            committedTargetPosition = targetPosition;
            targetMemoryTimer = targetMemorySeconds;
            return true;
        }

        private bool TryGetCommittedTargetPosition(out Vector3 targetPosition)
        {
            targetPosition = committedTargetPosition;
            if (committedTarget == null) return AcquireFreshTarget(out targetPosition);

            var damageable = committedTarget.GetComponentInChildren<IDamageable>() ?? committedTarget.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsDead)
            {
                ClearCommittedTarget();
                return AcquireFreshTarget(out targetPosition);
            }

            if (perception.CanSeeTargetTransform(committedTarget, out targetPosition))
            {
                committedTargetPosition = targetPosition;
                targetMemoryTimer = targetMemorySeconds;
                return true;
            }

            targetMemoryTimer -= Time.deltaTime;
            if (targetMemoryTimer > 0f)
            {
                targetPosition = committedTargetPosition;
                return true;
            }

            ClearCommittedTarget();
            return AcquireFreshTarget(out targetPosition);
        }

        private bool AcquireFreshTarget(out Vector3 targetPosition)
        {
            targetPosition = default;
            if (!perception.CanSeeTarget(out Transform target, out targetPosition)) return false;

            committedTarget = target;
            committedTargetPosition = targetPosition;
            targetMemoryTimer = targetMemorySeconds;
            return true;
        }

        private void ClearCommittedTarget()
        {
            committedTarget = null;
            targetMemoryTimer = 0f;
        }

        private void TryDealDamage(Transform target, Vector3 targetPos)
        {
            if (attackCooldownTimer > 0f) return;
            attackCooldownTimer = attackCooldownSeconds;

            float dist = Vector3.Distance(transform.position, targetPos);
            if (dist > attackRange * 1.3f) return; // re-check at the moment of impact, not just at windup start

            // Line check so a wall between the wolf and the target blocks the hit.
            if (Physics.Linecast(transform.position + Vector3.up * 0.5f, targetPos + Vector3.up * 0.9f, out RaycastHit hit))
            {
                if (target != null && !hit.transform.IsChildOf(target)) return;
            }

            // Player health can live under Systems; companions implement IDamageable on their root.
            var damageable = target != null
                ? target.GetComponentInChildren<IDamageable>() ?? target.GetComponentInParent<IDamageable>()
                : null;
            float applied = attackDamage * Mathf.Max(0f, TRLM.Progression.DifficultySettings.PlayerDamageMultiplier);
            OnAttackCommitted?.Invoke();
            damageable?.TakeDamage(applied, gameObject);
        }

        // ---------------------------------------------------------------- pack helpers

        private void AlertNearbyPack()
        {
            foreach (var other in allWolves)
            {
                if (other == this || other == null) continue;
                if (other.state == State.Chase || other.state == State.Attack) continue;
                if (Vector3.Distance(other.transform.position, transform.position) <= packAlertRadius)
                    other.ReceiveAlert(perception.LastKnownTargetPosition);
            }
        }

        private void ReceiveAlert(Vector3 position)
        {
            if (state == State.Idle || state == State.Roam)
            {
                investigateTarget = position;
                EnterState(State.Investigate);
            }
        }

        private int NearbyAllyCount(float radius)
        {
            int count = 0;
            foreach (var other in allWolves)
                if (other != this && other != null && Vector3.Distance(other.transform.position, transform.position) <= radius)
                    count++;
            return count;
        }

        private Vector3 PackFlankOffset(Vector3 playerPos)
        {
            // Deterministic per-wolf angle offset so a pack doesn't stack on one point.
            int index = allWolves.IndexOf(this);
            float angle = index * 47f; // arbitrary spread, not a full circle multiple to avoid symmetric clumping
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (attackRange * 0.8f);
            return playerPos + offset;
        }

        // ---------------------------------------------------------------- utility

        private void SetDestinationSafe(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        private Vector3 RandomPointAround(Vector3 center, float radius, Transform awayFrom = null)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 point = center + new Vector3(offset.x, 0f, offset.y);
            if (awayFrom != null)
            {
                Vector3 dir = (center - awayFrom.position).normalized;
                point = center + dir * radius;
            }
            return point;
        }

        private void FaceTowards(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
        }

        // ---------------------------------------------------------------- IDamageable (the wolf itself can be hurt — rockfall, future weapons)

        public void TakeDamage(float amount, GameObject source = null)
        {
            OnDamaged?.Invoke(amount);
            health.TakeDamage(amount, source);
            if (health.IsDead && !IsDead)
            {
                IsDead = true;
                agent.isStopped = true;
                currentAttackers.Remove(this);
                PredatorRegistry.Unregister(this);
                enabled = false;
                return;
            }

            // Wounded response: badly hurt wolves disengage, limp, and stay wary instead of
            // trading hits to the death.
            if (!IsDead && IsWounded)
            {
                woundedWaryTimer = woundedWarySeconds;
                if (state != State.Retreat && state != State.ReturnToTerritory)
                    EnterState(State.Retreat);
            }
        }

        /// <summary>Wounded wolves move at a visible limp — a stable clamp on top of whatever
        /// per-state speed EnterState assigned. Retreat is exempt (adrenaline flight).</summary>
        private void LateUpdate()
        {
            if (IsDead || !agent.enabled) return;
            if (IsWounded && state != State.Retreat)
                agent.speed = Mathf.Min(agent.speed, chaseSpeed * woundedSpeedFactor);
        }

        public void Heal(float amount) => health.Heal(amount);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 22f); // approximate sight range, see WolfPerception.sightRange
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(territoryCenter, leashDistanceFromTerritory);
            if (agent != null && agent.hasPath)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, agent.destination);
            }
        }
#endif
    }
}
