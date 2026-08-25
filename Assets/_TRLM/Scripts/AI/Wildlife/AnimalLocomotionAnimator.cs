using UnityEngine;
using UnityEngine.AI;
using TRLM.AI.Bear;
using TRLM.Survival;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Generic animal animator bridge for bear + passive wildlife (wolves keep their own
    /// WolfLocomotionAnimator). Drives a damped "Speed" float from agent velocity, an "Eat"
    /// bool for graze/forage states, and Attack/Hit/Dead via events. Works with either a
    /// BearAI or a PassiveWildlifeAI on the same GameObject — whichever is present.
    /// </summary>
    public class AnimalLocomotionAnimator : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int EatParam = Animator.StringToHash("Eat");
        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeadParam = Animator.StringToHash("Dead");

        [SerializeField] private Animator animator;
        [SerializeField] private float speedDampTime = 0.15f;

        private NavMeshAgent agent;
        private BearAI bear;
        private PassiveWildlifeAI passive;
        private HealthSystem health;
        private bool hasEatParam;

        private void Awake()
        {
            agent = GetComponentInParent<NavMeshAgent>();
            bear = GetComponentInParent<BearAI>();
            passive = GetComponentInParent<PassiveWildlifeAI>();
            health = GetComponentInParent<HealthSystem>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                foreach (var p in animator.parameters)
                    if (p.nameHash == EatParam) { hasEatParam = true; break; }
            }
        }

        private void OnEnable()
        {
            if (bear != null)
            {
                bear.OnAttackCommitted += HandleAttack;
                bear.OnDamaged += HandleDamaged;
            }
            if (passive != null) passive.OnDied += HandleDeath;
            if (health != null) health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (bear != null)
            {
                bear.OnAttackCommitted -= HandleAttack;
                bear.OnDamaged -= HandleDamaged;
            }
            if (passive != null) passive.OnDied -= HandleDeath;
            if (health != null) health.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            if (animator == null || agent == null) return;
            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedParam, speed, speedDampTime, Time.deltaTime);

            if (hasEatParam)
            {
                bool eating = (bear != null && bear.CurrentState == BearAI.State.Forage)
                              || (passive != null && passive.CurrentState == PassiveWildlifeAI.State.Graze);
                animator.SetBool(EatParam, eating);
            }
        }

        private void HandleAttack()
        {
            if (animator != null) animator.SetTrigger(AttackParam);
        }

        private void HandleDamaged(float _)
        {
            if (animator != null && (health == null || !health.IsDead)) animator.SetTrigger(HitParam);
        }

        private void HandleDeath()
        {
            if (animator != null) animator.SetBool(DeadParam, true);
        }
    }
}
