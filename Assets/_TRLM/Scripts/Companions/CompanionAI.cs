using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TRLM.Core;
using TRLM.Survival;

namespace TRLM.Companions
{
    /// <summary>
    /// Companion state machine (Follow/Wait/MovingToCommandPoint) — Sprint 2 pass adds squad
    /// life on top of the Sprint 1 navigation core. While settled, companions run short
    /// "idle activities" (look around, inspect a nearby spot, stand with a squadmate, scout a
    /// few meters ahead, check on a hurt ally) instead of standing frozen or randomly
    /// wandering; when CompanionAwareness reports a menacing predator, they take up watch
    /// positions between the player and the threat and face it. The command states and the
    /// State enum order are unchanged (CompanionStatePersistence saves the enum index).
    /// Destination refresh stays on the Sprint 1 repath-interval throttle.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthSystem))]
    public class CompanionAI : MonoBehaviour, IDamageable
    {
        public enum State { Follow, Wait, MovingToCommandPoint }

        /// <summary>What a settled companion is currently doing with its idle time. Read by
        /// CompanionLookAt (gaze) and CompanionLocomotionAnimator (alert flag); never saved.</summary>
        public enum IdleActivity { None, Stroll, LookAround, Inspect, Buddy, Scout, CheckAlly, WatchThreat, WatchNoise }

        [Header("References")]
        [SerializeField] private Transform followTarget; // usually the player

        [Header("Follow")]
        [SerializeField] private float trailDistance = 3.4f;
        [SerializeField] private float repathInterval = 0.2f;
        [SerializeField] private float moveSpeed = 3f;
        [Tooltip("While settled, the companion ignores anchor drift up to this radius — it stands its ground " +
                 "instead of micro-following every step. Only when the player gets further than " +
                 "trailDistance + this slack does it start moving again.")]
        [SerializeField] private float followSlack = 3.0f;
        [Tooltip("If the player walks into this radius around the companion, it steps aside instead of blocking the path.")]
        [SerializeField] private float personalSpaceRadius = 1.4f;
        [Tooltip("Player distance beyond which the companion runs (full moveSpeed) instead of walking to catch up.")]
        [SerializeField] private float catchUpDistance = 9f;
        [Tooltip("Beyond this distance the companion abandons formation entirely and beelines to the player at full speed.")]
        [SerializeField] private float hardCatchUpDistance = 16f;
        [Tooltip("Fraction of moveSpeed used when the companion is only mildly out of position.")]
        [SerializeField] private float relaxedSpeedFraction = 0.55f;

        [Header("Idle Life")]
        [Tooltip("While settled, the companion occasionally strolls to a nearby point so the squad doesn't stand frozen.")]
        [SerializeField] private float idleWanderRadius = 2.5f;
        [SerializeField] private Vector2 idleActivityIntervalRange = new Vector2(6f, 14f);
        [Tooltip("Degrees offset from directly-behind the follow target, so multiple companions " +
                 "fan out instead of stacking on one trail point. E.g. Jonah=-45, Mira=45, Lena=-110, Noah=110.")]
        [SerializeField] private float formationAngle;
        [Tooltip("When the direct line from the follow target to this companion's full-width anchor " +
                 "is NavMesh-blocked (typical at a doorway), the angle is scaled by this factor instead " +
                 "— a cheap single-check-per-repath fallback toward single-file rather than jamming the " +
                 "squad against a doorframe.")]
        [SerializeField] private float narrowPassageAngleScale = 0.35f;

        [Header("Threat Response")]
        [Tooltip("How far from the player a companion stands while watching a threat.")]
        [SerializeField] private float threatWatchDistance = 2.2f;
        [Tooltip("If the threat itself gets closer than this to the companion, it backs toward the player instead of holding.")]
        [SerializeField] private float threatBackOffDistance = 6f;

        [Header("Come Here")]
        [Tooltip("Each companion arrives offset from the requested point by its own formationAngle, " +
                 "at this radius — reuses the Follow fan-out so multiple companions don't stack on the " +
                 "exact same spot when commanded together.")]
        [SerializeField] private float comeHereRadius = 1.4f;

        [Header("Navigation Recovery")]
        [Tooltip("Seconds a moving companion can sit with near-zero velocity before a recovery repath is attempted.")]
        [SerializeField] private float stuckTimeout = 2.5f;
        [Tooltip("Seconds off-NavMesh before attempting an emergency snap back onto it.")]
        [SerializeField] private float offMeshTimeout = 3f;

        // ---------------------------------------------------------------- squad registry
        private static readonly List<CompanionAI> all = new List<CompanionAI>();
        /// <summary>Live companions in the scene — replaces FindObjectsByType scans in hot paths
        /// (WolfPerception target search, squad threat sharing).</summary>
        public static IReadOnlyList<CompanionAI> All => all;

        private static CompanionAI activeScout; // at most one companion ranges ahead at a time

        private NavMeshAgent agent;
        private HealthSystem health;
        private CompanionAwareness awareness;
        private State state = State.Follow;
        private float repathTimer;
        private float stuckTimer;
        private float offMeshTimer;
        private Vector3 lastDestination;
        private Vector3 commandPoint;
        private bool settled;          // true = close enough, standing relaxed instead of chasing the anchor
        private float idleTimer;
        private float nextActivityAt;

        private IdleActivity activity = IdleActivity.None;
        private float activityTimer;
        private float activityDuration;
        private Vector3 activityPoint;      // walk/look destination for the current activity
        private CompanionAI buddy;          // squadmate we're standing with
        private bool hasDesiredFacing;
        private Vector3 desiredFacingPoint;

        private HealthSystem playerHealth;  // for hurt-teammate reactions that include the player

        public State CurrentState => state;
        public IdleActivity CurrentActivity => activity;
        public bool IsDead => health != null && health.IsDead;
        public bool IsSettled => settled;
        public Transform FollowTarget => followTarget;
        public CompanionAwareness Awareness => awareness;
        public float FormationAngle => formationAngle;

        /// <summary>Future-hook: injury/personality systems can scale this instead of touching moveSpeed directly. Default 1 = no change.</summary>
        public float MoveSpeedMultiplier { get; set; } = 1f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthSystem>();
            awareness = GetComponent<CompanionAwareness>();
            agent.speed = moveSpeed;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            // Spread priorities (lower = yields less) per-instance so agents don't tie when both want the same spot.
            agent.avoidancePriority = 30 + Mathf.Abs(GetInstanceID()) % 40;

            if (followTarget == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) followTarget = player.transform;
            }
            if (followTarget != null)
                playerHealth = followTarget.GetComponentInChildren<HealthSystem>();
        }

        private void OnEnable()
        {
            all.Add(this);
            if (health != null)
            {
                health.OnDeath += HandleDeath;
                health.OnDamaged += HandleSelfDamaged;
            }
            if (playerHealth != null) playerHealth.OnDamaged += HandlePlayerDamaged;
        }

        private void OnDisable()
        {
            all.Remove(this);
            if (activeScout == this) activeScout = null;
            if (health != null)
            {
                health.OnDeath -= HandleDeath;
                health.OnDamaged -= HandleSelfDamaged;
            }
            if (playerHealth != null) playerHealth.OnDamaged -= HandlePlayerDamaged;
        }

        private void Update()
        {
            if (IsDead) return;

            if (!agent.isOnNavMesh)
            {
                TickOffMeshRecovery();
                return;
            }
            offMeshTimer = 0f;

            TickStuckRecovery();
            TickFacing();

            repathTimer += Time.deltaTime;
            if (repathTimer < repathInterval) return;
            repathTimer = 0f;

            switch (state)
            {
                case State.Follow: TickFollow(); break;
                case State.Wait: TickWait(); break;
                case State.MovingToCommandPoint: TickMovingToCommandPoint(); break;
            }
        }

        // ---------------------------------------------------------------- follow core

        private void TickFollow()
        {
            if (followTarget == null) return;

            float playerDist = Vector3.Distance(transform.position, followTarget.position);

            // Personal space: the player walked into us — sidestep out of their way and stay settled.
            if (playerDist < personalSpaceRadius)
            {
                Vector3 away = transform.position - followTarget.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = -followTarget.forward;
                Vector3 side = Vector3.Cross(Vector3.up, away).normalized * (formationAngle >= 0f ? 1f : -1f);
                agent.isStopped = false;
                agent.speed = moveSpeed * MoveSpeedMultiplier * relaxedSpeedFraction;
                SetDestinationSafe(transform.position + (away.normalized + side * 0.6f).normalized * (personalSpaceRadius + 0.6f));
                CancelActivity();
                settled = false;
                return;
            }

            // Threat overrides idle life at any follow distance: take up watch positions.
            if (awareness != null && awareness.HasThreat && playerDist < catchUpDistance)
            {
                TickThreatWatch();
                return;
            }

            // Hysteresis: once settled, hold position until the player actually leaves us behind.
            if (settled && playerDist < trailDistance + followSlack)
            {
                TickIdleLife();
                return;
            }

            CancelActivity();

            // Hard catch-up: far behind (teleport-adjacent situations, sprinting player) — drop
            // formation and beeline, so stragglers read as urgently rejoining, not strolling.
            if (playerDist > hardCatchUpDistance)
            {
                settled = false;
                agent.isStopped = false;
                agent.speed = moveSpeed * MoveSpeedMultiplier;
                SetDestinationSafe(followTarget.position - followTarget.forward * 1.2f);
                return;
            }

            Vector3 anchor = FormationAnchor(formationAngle);

            // Narrow-passage compression: a NavMesh-only raycast (no physics cost) from the follow
            // target to the full-width anchor — if it's blocked, this companion is trying to fan out
            // through something like a doorway, so fall back to a much tighter angle instead of
            // jamming there. One check per repath tick (already throttled to repathInterval).
            if (NavMesh.Raycast(followTarget.position, anchor, out _, NavMesh.AllAreas))
                anchor = FormationAnchor(formationAngle * narrowPassageAngleScale);

            // Horizontal-plane distance: the player often stands slightly above/below the
            // companion's navmesh (beach ledges, stairs) and a Y-inclusive distance could
            // never reach the settle threshold, leaving companions permanently "catching up".
            Vector3 flatDelta = anchor - transform.position;
            flatDelta.y = 0f;
            float anchorDist = flatDelta.magnitude;
            bool reachedPathEnd = !agent.pathPending && agent.hasPath &&
                                  agent.remainingDistance <= agent.stoppingDistance + 0.25f;
            if (anchorDist <= 0.7f || (reachedPathEnd && anchorDist <= trailDistance))
            {
                // Arrived — or got as close as the NavMesh allows (anchor over water/off-mesh):
                // relax rather than orbit an unreachable point forever.
                settled = true;
                agent.isStopped = true;
                ScheduleNextActivity();
                return;
            }

            settled = false;
            agent.isStopped = false;
            // Distance-matched pace: stroll when only slightly out of position, run only on a real catch-up.
            float urgency = Mathf.InverseLerp(trailDistance + followSlack, catchUpDistance, playerDist);
            agent.speed = moveSpeed * MoveSpeedMultiplier * Mathf.Lerp(relaxedSpeedFraction, 1f, urgency);
            SetDestinationSafe(anchor);
        }

        private void TickWait()
        {
            // Waiting is a command, but waiting people still notice wolves and look around.
            if (awareness != null && awareness.HasThreat)
            {
                FaceActivityPoint(CurrentThreatPosition());
                agent.isStopped = true;
                return;
            }
            agent.isStopped = true;
            TickIdleLife();
        }

        // ---------------------------------------------------------------- threat watch

        private Vector3 CurrentThreatPosition()
            => awareness.ThreatTransform != null ? awareness.ThreatTransform.position : awareness.ThreatPosition;

        /// <summary>Squad under threat: stand between the player and the threat (fanned by
        /// formation angle so the squad forms a loose arc), face it, and back off if it closes in.
        /// This is also the combat-positioning behavior — while the player shoots, companions
        /// hold the arc instead of milling around behind them.</summary>
        private void TickThreatWatch()
        {
            activity = IdleActivity.WatchThreat;
            Vector3 threatPos = CurrentThreatPosition();
            Vector3 toThreat = threatPos - followTarget.position;
            toThreat.y = 0f;
            if (toThreat.sqrMagnitude < 0.04f) toThreat = transform.forward;

            float threatDist = Vector3.Distance(transform.position, threatPos);
            Vector3 anchor;
            if (threatDist < threatBackOffDistance)
            {
                // Too close — fall back to the player's far side, still facing the threat.
                anchor = followTarget.position - toThreat.normalized * (threatWatchDistance * 0.8f);
            }
            else
            {
                // Hold a fanned arc on the threat side of the player, well short of the threat itself.
                Vector3 arcDir = Quaternion.Euler(0f, formationAngle * 0.4f, 0f) * toThreat.normalized;
                anchor = followTarget.position + arcDir * threatWatchDistance;
            }

            FaceActivityPoint(threatPos);

            Vector3 flatToAnchor = anchor - transform.position;
            flatToAnchor.y = 0f;
            if (flatToAnchor.magnitude > 0.9f)
            {
                agent.isStopped = false;
                agent.speed = moveSpeed * MoveSpeedMultiplier * 0.8f;
                SetDestinationSafe(anchor);
            }
            else
            {
                agent.isStopped = true;
            }
            settled = true; // threat watch counts as being "in position"
        }

        // ---------------------------------------------------------------- idle life

        /// <summary>Settled squad life, Sprint 2 pass: instead of only random strolls, pick from a
        /// small set of readable activities. Runs on the repath tick. Order of checks matters:
        /// reactions (gunfire, hurt ally) preempt the cosmetic activities.</summary>
        private void TickIdleLife()
        {
            // Reaction: recent gunfire/loud noise that didn't come from the squad's own position.
            if (awareness != null && awareness.HasRecentLoudNoise && activity != IdleActivity.WatchNoise)
            {
                bool friendly = followTarget != null &&
                                (awareness.LastLoudNoisePosition - followTarget.position).sqrMagnitude < 9f;
                if (!friendly)
                {
                    StartActivity(IdleActivity.WatchNoise, 3.5f);
                    agent.isStopped = true;
                    FaceActivityPoint(awareness.LastLoudNoisePosition);
                    return;
                }
            }

            // Reaction: a squadmate (or the player) got hurt — nearest free companion walks over.
            if (awareness != null && awareness.HasInjuredAlly && activity == IdleActivity.None && IsClosestFreeCompanionTo(awareness.InjuredAlly))
            {
                StartActivity(IdleActivity.CheckAlly, 6f);
                activityPoint = awareness.InjuredAlly.position;
                agent.isStopped = false;
                agent.speed = moveSpeed * MoveSpeedMultiplier * relaxedSpeedFraction;
                SetDestinationSafe(ComputeStandNearPoint(awareness.InjuredAlly.position, 1.2f));
                return;
            }

            if (activity != IdleActivity.None)
            {
                TickCurrentActivity();
                return;
            }

            idleTimer += repathInterval;
            if (idleTimer < nextActivityAt) return;

            PickNewActivity();
        }

        private void PickNewActivity()
        {
            ScheduleNextActivity();

            // Weighted, context-aware pick. Not uniform-random: watching and glancing dominate,
            // ranging ahead is rare and only one companion does it at a time.
            float roll = Random.value;
            if (roll < 0.30f)
            {
                // Look around: face something meaningful — the direction the squad came from,
                // the darkest treeline, or a remembered noise. Approximated by a random bearing
                // biased away from the player's facing (they watch what the player isn't watching).
                StartActivity(IdleActivity.LookAround, Random.Range(2.5f, 5f));
                Vector3 baseDir = followTarget != null ? -followTarget.forward : transform.forward;
                Vector3 dir = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * baseDir;
                FaceActivityPoint(transform.position + dir * 8f);
                agent.isStopped = true;
            }
            else if (roll < 0.50f)
            {
                // Inspect: short walk to a nearby point, then stand facing it (reads as
                // checking a bush/rock/track rather than aimless drift).
                Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(1.5f, idleWanderRadius + 1f);
                Vector3 point = transform.position + new Vector3(circle.x, 0f, circle.y);
                if (followTarget != null && Vector3.Distance(point, followTarget.position) < personalSpaceRadius + 0.8f) return;
                StartActivity(IdleActivity.Inspect, Random.Range(4f, 7f));
                activityPoint = point;
                agent.isStopped = false;
                agent.speed = moveSpeed * MoveSpeedMultiplier * relaxedSpeedFraction * 0.8f;
                SetDestinationSafe(point);
            }
            else if (roll < 0.65f && TryPickBuddy(out buddy))
            {
                // Stand beside another settled companion for a while — squad reads as people
                // loosely pairing up rather than four isolated satellites.
                StartActivity(IdleActivity.Buddy, Random.Range(5f, 9f));
                Vector3 side = Vector3.Cross(Vector3.up, (transform.position - buddy.transform.position).normalized);
                activityPoint = buddy.transform.position + side * 1.1f;
                agent.isStopped = false;
                agent.speed = moveSpeed * MoveSpeedMultiplier * relaxedSpeedFraction * 0.8f;
                SetDestinationSafe(activityPoint);
            }
            else if (roll < 0.72f && activeScout == null && followTarget != null)
            {
                // Scout: drift a few meters ahead of the player, on their movement axis. Only
                // one companion at a time, only while the squad is calm.
                activeScout = this;
                StartActivity(IdleActivity.Scout, Random.Range(5f, 8f));
                Vector3 ahead = followTarget.position + followTarget.forward * (trailDistance + 2.5f);
                activityPoint = ahead;
                agent.isStopped = false;
                agent.speed = moveSpeed * MoveSpeedMultiplier * relaxedSpeedFraction;
                SetDestinationSafe(ahead);
            }
            else
            {
                // Plain short stroll (the Sprint 1 behavior) as the fallback filler.
                Vector2 circle = Random.insideUnitCircle * idleWanderRadius;
                Vector3 stroll = transform.position + new Vector3(circle.x, 0f, circle.y);
                if (followTarget != null && Vector3.Distance(stroll, followTarget.position) < personalSpaceRadius + 0.8f) return;
                StartActivity(IdleActivity.Stroll, 4f);
                agent.isStopped = false;
                agent.speed = moveSpeed * MoveSpeedMultiplier * relaxedSpeedFraction * 0.8f;
                SetDestinationSafe(stroll);
            }
        }

        private void TickCurrentActivity()
        {
            activityTimer += repathInterval;

            switch (activity)
            {
                case IdleActivity.Inspect:
                case IdleActivity.Stroll:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                    {
                        agent.isStopped = true;
                        if (activity == IdleActivity.Inspect) FaceActivityPoint(activityPoint + (activityPoint - transform.position));
                    }
                    break;

                case IdleActivity.Buddy:
                    if (buddy == null || buddy.IsDead) { CancelActivity(); return; }
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                    {
                        agent.isStopped = true;
                        // Face roughly the same way the buddy faces — two people looking out together.
                        FaceActivityPoint(transform.position + buddy.transform.forward * 6f);
                    }
                    break;

                case IdleActivity.Scout:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                    {
                        agent.isStopped = true;
                        if (followTarget != null) FaceActivityPoint(transform.position + followTarget.forward * 8f);
                    }
                    break;

                case IdleActivity.CheckAlly:
                    if (awareness == null || awareness.InjuredAlly == null) { CancelActivity(); return; }
                    FaceActivityPoint(awareness.InjuredAlly.position);
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                        agent.isStopped = true;
                    break;

                case IdleActivity.WatchNoise:
                case IdleActivity.LookAround:
                    // Pure facing activities — nothing to do until the timer ends.
                    break;
            }

            if (activityTimer >= activityDuration) CancelActivity();
        }

        private void StartActivity(IdleActivity newActivity, float duration)
        {
            if (activeScout == this && newActivity != IdleActivity.Scout) activeScout = null;
            activity = newActivity;
            activityTimer = 0f;
            activityDuration = duration;
        }

        private void CancelActivity()
        {
            if (activeScout == this) activeScout = null;
            activity = IdleActivity.None;
            buddy = null;
            hasDesiredFacing = false;
        }

        private void ScheduleNextActivity()
        {
            idleTimer = 0f;
            nextActivityAt = Random.Range(idleActivityIntervalRange.x, idleActivityIntervalRange.y);
        }

        private bool TryPickBuddy(out CompanionAI result)
        {
            result = null;
            float bestSqr = 8f * 8f;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == this || other == null || other.IsDead || !other.IsSettled) continue;
                float sqr = (other.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    result = other;
                }
            }
            return result != null;
        }

        private bool IsClosestFreeCompanionTo(Transform target)
        {
            if (target == null) return false;
            float mySqr = (target.position - transform.position).sqrMagnitude;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == this || other == null || other.IsDead) continue;
                if (other.transform == target) continue;
                if ((target.position - other.transform.position).sqrMagnitude < mySqr) return false;
            }
            return true;
        }

        private Vector3 ComputeStandNearPoint(Vector3 target, float radius)
        {
            Vector3 dir = (transform.position - target);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.04f) dir = transform.forward;
            return target + dir.normalized * radius;
        }

        // ---------------------------------------------------------------- facing

        /// <summary>Manual turn-toward while the agent is stopped; NavMeshAgent handles rotation
        /// while moving. Turn rate is deliberately human (~180°/s max via slerp) so heads/bodies
        /// don't snap.</summary>
        private void FaceActivityPoint(Vector3 point)
        {
            desiredFacingPoint = point;
            hasDesiredFacing = true;
        }

        private void TickFacing()
        {
            if (!hasDesiredFacing) return;
            if (agent.velocity.sqrMagnitude > 0.15f) return; // agent steering owns rotation while moving

            Vector3 dir = desiredFacingPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 4.5f);
        }

        // ---------------------------------------------------------------- formation / recovery

        private Vector3 FormationAnchor(float angle)
        {
            Vector3 formationDir = Quaternion.Euler(0f, 180f + angle, 0f) * followTarget.forward;
            return followTarget.position + formationDir.normalized * trailDistance;
        }

        /// <summary>Conservative stuck recovery: if the agent has a destination but isn't making progress, force a repath.</summary>
        private void TickStuckRecovery()
        {
            if (agent.isStopped || agent.pathPending) { stuckTimer = 0f; return; }
            if (agent.remainingDistance <= agent.stoppingDistance + 0.1f) { stuckTimer = 0f; return; }

            if (agent.velocity.sqrMagnitude < 0.02f)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;

            if (stuckTimer >= stuckTimeout)
            {
                stuckTimer = 0f;
                SetDestinationSafe(lastDestination);
            }
        }

        /// <summary>Conservative off-NavMesh recovery: only snaps back after a clear timeout, never mid-air teleports across the map.</summary>
        private void TickOffMeshRecovery()
        {
            offMeshTimer += Time.deltaTime;
            if (offMeshTimer < offMeshTimeout) return;
            offMeshTimer = 0f;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }

        private void TickMovingToCommandPoint()
        {
            agent.isStopped = false;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                state = State.Wait;
        }

        // ---------------------------------------------------------------- commands

        public void CommandFollow()
        {
            state = State.Follow;
            settled = false;
            CancelActivity();
        }

        public void CommandWait()
        {
            state = State.Wait;
            CancelActivity();
            if (agent.isOnNavMesh) agent.isStopped = true;
        }

        public void CommandComeHere(Vector3 point)
        {
            Vector3 offset = Quaternion.Euler(0f, formationAngle, 0f) * Vector3.back * comeHereRadius;
            commandPoint = point + offset;
            state = State.MovingToCommandPoint;
            settled = false;
            CancelActivity();
            agent.speed = moveSpeed * MoveSpeedMultiplier;
            SetDestinationSafe(commandPoint);
        }

        private void SetDestinationSafe(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                lastDestination = hit.position;
                agent.SetDestination(hit.position);
            }
        }

        // ---------------------------------------------------------------- damage plumbing

        private void HandleSelfDamaged(float amount, GameObject source)
        {
            // Tell squadmates so the nearest one reacts (CheckAlly / gaze).
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == null || other == this) continue;
                other.Awareness?.NotifyAllyHurt(transform);
            }
        }

        private void HandlePlayerDamaged(float amount, GameObject source)
        {
            awareness?.NotifyAllyHurt(followTarget);
        }

        private void HandleDeath()
        {
            state = State.Wait;
            CancelActivity();
            if (agent != null)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                agent.enabled = false;
            }
            enabled = false;
        }

        // ---------------------------------------------------------------- IDamageable passthrough

        public void TakeDamage(float amount, GameObject source = null) => health.TakeDamage(amount, source);
        public void Heal(float amount) => health.Heal(amount);
    }
}
