using UnityEngine;
using UnityEngine.AI;

namespace TRLM.AI.Human
{
    /// <summary>
    /// Cheap locomotion bridge for a NavMesh-driven humanoid that only owns a looping walk clip
    /// (e.g. the Meshy-rigged soldier): scale the Animator's playback speed by how fast the agent
    /// is actually moving, so a stopped guard doesn't march in place and a moving one strides at a
    /// natural cadence. Also drives an optional "Speed" float so a future blend tree can plug in.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class HumanoidLocomotion : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float referenceSpeed = 1.4f;   // agent speed at which the clip plays 1:1
        [SerializeField] private float minPlaybackSpeed = 0.06f; // gentle idle sway when stopped, not a freeze
        [SerializeField] private float maxPlaybackSpeed = 1.6f;
        [SerializeField] private float damp = 8f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private NavMeshAgent agent;
        private bool hasSpeedParam;
        private float current = 1f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
                foreach (var p in animator.parameters)
                    if (p.nameHash == SpeedParam) { hasSpeedParam = true; break; }
        }

        private void Update()
        {
            if (animator == null || agent == null) return;

            float vel = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            float norm = referenceSpeed > 0.01f ? vel / referenceSpeed : 0f;

            float target = Mathf.Clamp(norm, minPlaybackSpeed, maxPlaybackSpeed);
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-damp * Time.deltaTime));
            animator.speed = current;

            if (hasSpeedParam) animator.SetFloat(SpeedParam, vel, 0.12f, Time.deltaTime);
        }

        private void OnDisable()
        {
            if (animator != null) animator.speed = 1f;
        }
    }
}
