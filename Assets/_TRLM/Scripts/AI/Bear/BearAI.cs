using System;
using UnityEngine;
using UnityEngine.AI;
using TRLM.AI.Perception;
using TRLM.AI.Wildlife;
using TRLM.Core;
using TRLM.Survival;

namespace TRLM.AI.Bear
{
    /// <summary>
    /// Solitary territorial bear — deliberately NOT WolfAI with a new model. A bear never
    /// stalks and has no pack: it forages inside its territory, and when an intruder presses
    /// in it escalates through a readable warning ladder — stand and face (Warn, growling),
    /// then a bluff charge that pulls up short with a roar, and only if the intruder still
    /// doesn't leave (or it gets hurt) a real charge-and-maul. It shrugs off gunfire noise
    /// that would scatter a lone wolf, barely retreats when wounded (it enrages instead),
    /// and only flees below a small health floor. Chases are short: leaving its territory
    /// radius ends the pursuit quickly.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthSystem))]
    public class BearAI : MonoBehaviour, IDamageable, IPredator, IWildlifeAgent
    {
        public enum State { Idle, Forage, Patrol, Investigate, Warn, BluffCharge, Charge, Attack, Pursue, Disengage, ReturnHome }

        [Header("Speeds")]
        [SerializeField] private float patrolSpeed = 1.1f;
        [SerializeField] private float chargeSpeed = 6.5f;
        [SerializeField] private float pursueSpeed = 4.6f;

        [Header("Territory")]
        [SerializeField] private float territoryRadius = 30f;
        [SerializeField] private float pursuitLeash = 45f;

        [Header("Escalation")]
        [Tooltip("Intruder inside this range puts the bear into Warn.")]
        [SerializeField] private float warnRadius = 16f;
        [Tooltip("Intruder inside this range (or lingering in warn range) triggers the bluff charge.")]
        [SerializeField] private float pressRadius = 9f;
        [SerializeField] private float warnPatienceSeconds = 6f;
        [Tooltip("Bluff charge pulls up this far from the target, roars, and reassesses.")]
        [SerializeField] private float bluffStopDistance = 3.5f;

        [Header("Combat")]
        [SerializeField] private float attackRange = 2.6f;
        [SerializeField] private float attackDamage = 30f;
        [SerializeField] private float attackWindupSeconds = 0.45f;
        [SerializeField] private float attackCooldownSeconds = 2.2f;
        [SerializeField] private float maxPursueSeconds = 10f;
        [Tooltip("Below this health fraction the bear finally breaks off and leaves.")]
        [SerializeField] private float fleeHealthFraction = 0.15f;

        [Header("Perception")]
        [SerializeField] private float sightRange = 20f;
        [SerializeField] private float senseInterval = 0.3f;

        private NavMeshAgent agent;
        private HealthSystem health;
        private WildlifeSpawnZone territoryZone;
        private WildlifeSpeciesProfile species;
        private Vector3 homePoint;

        private State state = State.Idle;
        private float stateTimer;
        private float senseTimer;
        private float attackCooldownTimer;
        private bool attackWindingUp;
        private float pursueTimer;
        private bool hasBluffed;   // first charge is a bluff; the next one is real
        private bool enraged;      // set when damaged — skips straight past warnings
        private Transform intruder;
        private Vector3 investigatePoint;
        private float warnPressure;

        public State CurrentState => state;
        public bool IsDead { get; private set; }
        public event Action<State> OnStateChanged;
        public event Action OnAttackCommitted;
        public event Action OnRoar;
        public event Action<float> OnDamaged;

        // ---------------------------------------------------------------- IPredator
        public Transform PredatorTransform => transform;
        public bool IsMenacing => state == State.Warn || state == State.BluffCharge || state == State.Charge
                                  || state == State.Attack || state == State.Pursue;
        public bool IsDeadPredator => IsDead;

        public void Initialize(WildlifeSpawnZone owningZone, WildlifeSpeciesProfile owningSpecies)
        {
            territoryZone = owningZone;
            species = owningSpecies;
            homePoint = owningZone != null ? owningZone.Center : transform.position;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthSystem>();
            homePoint = transform.position;
            agent.angularSpeed = 160f; // bears turn heavily, wolves whip around
            senseTimer = (Mathf.Abs(GetInstanceID()) % 30) * 0.01f;
        }

        private void OnEnable()
        {
            PredatorRegistry.Register(this);
            NoiseEvents.OnNoise += HandleNoise;
        }

        private void OnDisable()
        {
            PredatorRegistry.Unregister(this);
            NoiseEvents.OnNoise -= HandleNoise;
        }

        private void Start() => EnterState(State.Idle);

        private void Update()
        {
            if (IsDead || !agent.isOnNavMesh) return;

            if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;
            stateTimer += Time.deltaTime;

            senseTimer -= Time.deltaTime;
            if (senseTimer <= 0f)
            {
                senseTimer = senseInterval;
                SenseIntruder();
            }

            switch (state)
            {
                case State.Idle:
                    if (stateTimer >= 5f) EnterState(UnityEngine.Random.value < 0.55f ? State.Forage : State.Patrol);
                    break;

                case State.Forage:
                    if (stateTimer >= 8f) EnterState(State.Patrol);
                    break;

                case State.Patrol:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.4f)
                        EnterState(State.Idle);
                    break;

                case State.Investigate:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.6f || stateTimer > 10f)
                        EnterState(State.ReturnHome);
                    break;

                case State.Warn:
                    TickWarn();
                    break;

                case State.BluffCharge:
                    TickCharge(bluff: true);
                    break;

                case State.Charge:
                    TickCharge(bluff: false);
                    break;

                case State.Attack:
                    TickAttack();
                    break;

                case State.Pursue:
                    TickPursue();
                    break;

                case State.Disengage:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f || stateTimer > 8f)
                        EnterState(State.ReturnHome);
                    break;

                case State.ReturnHome:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                    {
                        warnPressure = 0f;
                        hasBluffed = false;
                        enraged = false;
                        EnterState(State.Idle);
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- sensing

        private void SenseIntruder()
        {
            var manager = WildlifeSpawnManager.Instance;
            Transform player = manager != null ? manager.Player : null;
            Transform nearest = null;
            float nearestDist = float.MaxValue;

            if (player != null)
            {
                float d = Vector3.Distance(transform.position, player.position);
                if (d < nearestDist) { nearestDist = d; nearest = player; }
            }
            var companions = TRLM.Companions.CompanionAI.All;
            for (int i = 0; i < companions.Count; i++)
            {
                var c = companions[i];
                if (c == null || c.IsDead) continue;
                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d < nearestDist) { nearestDist = d; nearest = c.transform; }
            }

            bool inSight = nearest != null && nearestDist <= sightRange && !LineBlocked(nearest);
            bool inTerritory = nearest != null &&
                               Vector3.Distance(nearest.position, homePoint) <= territoryRadius;

            if (inSight && inTerritory)
            {
                intruder = nearest;
                if (IsCalmState(state))
                {
                    if (enraged || nearestDist <= pressRadius * 0.7f)
                        EnterState(hasBluffed || enraged ? State.Charge : State.BluffCharge);
                    else if (nearestDist <= warnRadius)
                        EnterState(State.Warn);
                }
            }
            else if (IsCalmState(state))
            {
                intruder = null;
            }
        }

        private static bool IsCalmState(State s)
            => s == State.Idle || s == State.Forage || s == State.Patrol || s == State.Investigate || s == State.ReturnHome;

        private bool LineBlocked(Transform target)
        {
            Vector3 eye = transform.position + Vector3.up * 1.1f;
            if (Physics.Linecast(eye, target.position + Vector3.up * 0.9f, out RaycastHit hit))
                return !hit.transform.IsChildOf(target);
            return false;
        }

        private void HandleNoise(Vector3 position, float loudness)
        {
            if (IsDead || !IsCalmState(state)) return;
            float dist = Vector3.Distance(transform.position, position);
            if (dist > loudness) return;
            // Gunfire inside the territory annoys a bear rather than scaring it: investigate.
            if (Vector3.Distance(position, homePoint) <= territoryRadius * 1.3f)
            {
                investigatePoint = position;
                EnterState(State.Investigate);
            }
        }

        // ---------------------------------------------------------------- escalation states

        private void TickWarn()
        {
            if (intruder == null) { EnterState(State.ReturnHome); return; }
            FaceTowards(intruder.position);

            float dist = Vector3.Distance(transform.position, intruder.position);
            if (dist > warnRadius * 1.4f)
            {
                // Intruder backed off — bear won.
                EnterState(State.ReturnHome);
                return;
            }

            // Pressure builds while they linger, faster the closer they are.
            warnPressure += Time.deltaTime * (dist <= pressRadius ? 2.2f : 1f);
            if (warnPressure >= warnPatienceSeconds)
            {
                warnPressure = 0f;
                EnterState(hasBluffed || enraged ? State.Charge : State.BluffCharge);
            }
        }

        private void TickCharge(bool bluff)
        {
            if (intruder == null) { EnterState(State.ReturnHome); return; }

            float dist = Vector3.Distance(transform.position, intruder.position);
            agent.isStopped = false;
            agent.SetDestination(intruder.position);

            if (bluff && dist <= bluffStopDistance)
            {
                // Pull up short, roar, reassess from Warn — the classic bluff charge.
                hasBluffed = true;
                agent.isStopped = true;
                OnRoar?.Invoke();
                EnterState(State.Warn);
                return;
            }

            if (!bluff && dist <= attackRange)
            {
                EnterState(State.Attack);
                return;
            }

            if (stateTimer > 6f || Vector3.Distance(transform.position, homePoint) > pursuitLeash)
                EnterState(State.Disengage);
        }

        private void TickAttack()
        {
            if (intruder == null) { EnterState(State.Disengage); return; }
            FaceTowards(intruder.position);

            float dist = Vector3.Distance(transform.position, intruder.position);
            if (dist > attackRange * 1.4f) { EnterState(State.Pursue); return; }

            var damageable = intruder.GetComponentInChildren<IDamageable>() ?? intruder.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsDead) { EnterState(State.Disengage); return; }

            if (attackWindingUp)
            {
                if (stateTimer >= attackWindupSeconds)
                {
                    attackWindingUp = false;
                    stateTimer = 0f;
                    DealDamage(damageable);
                }
                return;
            }

            if (attackCooldownTimer <= 0f)
            {
                attackWindingUp = true;
                stateTimer = 0f;
            }
        }

        private void TickPursue()
        {
            pursueTimer += Time.deltaTime;
            if (intruder == null || pursueTimer > maxPursueSeconds ||
                Vector3.Distance(transform.position, homePoint) > pursuitLeash)
            {
                EnterState(State.Disengage);
                return;
            }

            agent.SetDestination(intruder.position);
            if (Vector3.Distance(transform.position, intruder.position) <= attackRange)
                EnterState(State.Attack);
        }

        private void DealDamage(IDamageable damageable)
        {
            if (attackCooldownTimer > 0f) return;
            attackCooldownTimer = attackCooldownSeconds;
            float applied = attackDamage * Mathf.Max(0f, TRLM.Progression.DifficultySettings.PlayerDamageMultiplier);
            OnAttackCommitted?.Invoke();
            damageable?.TakeDamage(applied, gameObject);
        }

        // ---------------------------------------------------------------- state entry

        private void EnterState(State next)
        {
            state = next;
            stateTimer = 0f;
            OnStateChanged?.Invoke(next);

            switch (next)
            {
                case State.Idle:
                    agent.isStopped = true;
                    break;
                case State.Forage:
                    agent.isStopped = true; // head-down eating animation carries this state
                    break;
                case State.Patrol:
                    agent.isStopped = false;
                    agent.speed = patrolSpeed;
                    SetDestinationSafe(territoryZone != null
                        ? territoryZone.GetRandomPointInZone()
                        : homePoint + RandomFlat(territoryRadius * 0.5f));
                    break;
                case State.Investigate:
                    agent.isStopped = false;
                    agent.speed = patrolSpeed * 1.5f;
                    SetDestinationSafe(investigatePoint);
                    break;
                case State.Warn:
                    agent.isStopped = true;
                    OnRoar?.Invoke(); // growl hook — AnimalAudio picks state-appropriate clip
                    break;
                case State.BluffCharge:
                case State.Charge:
                    agent.isStopped = false;
                    agent.speed = chargeSpeed;
                    break;
                case State.Attack:
                    agent.isStopped = true;
                    attackWindingUp = true;
                    break;
                case State.Pursue:
                    agent.isStopped = false;
                    agent.speed = pursueSpeed;
                    pursueTimer = 0f;
                    break;
                case State.Disengage:
                    agent.isStopped = false;
                    agent.speed = patrolSpeed * 1.6f;
                    SetDestinationSafe(homePoint + RandomFlat(6f));
                    break;
                case State.ReturnHome:
                    agent.isStopped = false;
                    agent.speed = patrolSpeed * 1.2f;
                    SetDestinationSafe(homePoint + RandomFlat(4f));
                    break;
            }
        }

        private Vector3 RandomFlat(float radius)
        {
            Vector2 c = UnityEngine.Random.insideUnitCircle * radius;
            return new Vector3(c.x, 0f, c.y);
        }

        private void FaceTowards(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            // Heavy, deliberate turn — half the wolf's snappiness.
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 2.5f);
        }

        private void SetDestinationSafe(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        // ---------------------------------------------------------------- IDamageable

        public void TakeDamage(float amount, GameObject source = null)
        {
            OnDamaged?.Invoke(amount);
            health.TakeDamage(amount, source);

            if (health.IsDead && !IsDead)
            {
                IsDead = true;
                agent.isStopped = true;
                PredatorRegistry.Unregister(this);
                enabled = false;
                return;
            }

            if (IsDead) return;

            if (health.Normalized <= fleeHealthFraction)
            {
                // Finally beaten — leave for good.
                enraged = false;
                EnterState(State.Disengage);
                return;
            }

            // Getting shot doesn't scare a bear — it commits.
            enraged = true;
            if (source != null) intruder = source.transform;
            if (state != State.Attack && state != State.Charge)
                EnterState(State.Charge);
        }

        public void Heal(float amount) => health.Heal(amount);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.35f, 0.1f);
            Gizmos.DrawWireSphere(Application.isPlaying ? homePoint : transform.position, territoryRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, warnRadius);
        }
#endif
    }
}
