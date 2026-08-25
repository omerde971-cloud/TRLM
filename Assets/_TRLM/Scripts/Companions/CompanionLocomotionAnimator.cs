using UnityEngine;
using UnityEngine.AI;

namespace TRLM.Companions
{
    /// <summary>
    /// Drives the Animator's locomotion parameters from the NavMeshAgent's actual velocity.
    /// Sprint 2: "Speed" is damped (SetFloat dampTime) so gait changes ease instead of snapping,
    /// and an "Alert" bool mirrors squad threat state for the alert idle pose. Kept separate from
    /// CompanionAI so the navigation state machine stays animation-agnostic (a companion with no
    /// Animator still works fine; this component just does nothing without one).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionLocomotionAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AlertHash = Animator.StringToHash("Alert");

        [Tooltip("Seconds of damping on the Speed float — acceleration/deceleration smoothing.")]
        [SerializeField] private float speedDampTime = 0.12f;

        private Animator animator;
        private NavMeshAgent agent;
        private CompanionAwareness awareness;
        private bool hasAlertParam;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            agent = GetComponent<NavMeshAgent>();
            awareness = GetComponent<CompanionAwareness>();
            if (animator != null)
            {
                foreach (var p in animator.parameters)
                    if (p.nameHash == AlertHash) { hasAlertParam = true; break; }
            }
        }

        private void Update()
        {
            if (animator == null) return;
            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedHash, speed, speedDampTime, Time.deltaTime);
            if (hasAlertParam)
                animator.SetBool(AlertHash, awareness != null && awareness.HasThreat);
        }

        // Walk_N/Run_N (StarterAssets) carry a baked "OnFootstep" AnimationEvent for footstep
        // audio. Forwarded to CompanionFootstepAudio when present; otherwise just absorbs the
        // event so Unity doesn't log a console error every step.
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight < 0.5f) return;
            footsteps ??= GetComponent<CompanionFootstepAudio>();
            footsteps?.PlayFootstep();
        }

        private void OnLand(AnimationEvent animationEvent) { }

        private CompanionFootstepAudio footsteps;
    }
}
