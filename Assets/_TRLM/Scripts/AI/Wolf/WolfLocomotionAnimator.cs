using UnityEngine;
using UnityEngine.AI;
using TRLM.Survival;

namespace TRLM.AI.Wolf
{
    /// <summary>
    /// Bridges WolfAI/NavMeshAgent state into the wolf's Animator, mirroring the pattern
    /// CompanionLocomotionAnimator uses for humans: locomotion is a Speed float driven from agent
    /// velocity (blend tree Idle -> Walk -> Gallop), while discrete events (attack commit, damage,
    /// death) fire triggers. Keeps WolfAI itself animation-agnostic.
    /// </summary>
    public class WolfLocomotionAnimator : MonoBehaviour
    {
        [SerializeField] private WolfAI wolf;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [Tooltip("Seconds of damping applied to the Speed parameter so gait changes ease instead of snapping.")]
        [SerializeField] private float speedDampTime = 0.15f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeadParam = Animator.StringToHash("Dead");

        private HealthSystem health;

        private void Awake()
        {
            if (wolf == null) wolf = GetComponentInParent<WolfAI>();
            if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            health = GetComponentInParent<HealthSystem>();
        }

        private void OnEnable()
        {
            if (wolf != null)
            {
                wolf.OnAttackCommitted += HandleAttack;
                wolf.OnDamaged += HandleDamaged;
            }
            if (health != null) health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (wolf != null)
            {
                wolf.OnAttackCommitted -= HandleAttack;
                wolf.OnDamaged -= HandleDamaged;
            }
            if (health != null) health.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            if (animator == null || agent == null) return;
            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedParam, speed, speedDampTime, Time.deltaTime);
        }

        private void HandleAttack()
        {
            if (animator != null) animator.SetTrigger(AttackParam);
        }

        private void HandleDamaged(float amount)
        {
            if (animator != null && (health == null || !health.IsDead)) animator.SetTrigger(HitParam);
        }

        private void HandleDeath()
        {
            if (animator != null) animator.SetBool(DeadParam, true);
        }
    }
}
