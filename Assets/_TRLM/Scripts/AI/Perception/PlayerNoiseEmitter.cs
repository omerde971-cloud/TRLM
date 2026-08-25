using UnityEngine;
using TRLM.Player;

namespace TRLM.AI.Perception
{
    /// <summary>
    /// Watches the player's existing, unmodified FirstPersonController and turns movement
    /// into NoiseEvents. Walking is quiet, sprinting is loud, landing a jump produces a
    /// brief pulse — matches the sprint's "walking: low noise, running: higher detection,
    /// jump/impact: brief event" spec without touching FirstPersonController itself.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerNoiseEmitter : MonoBehaviour
    {
        [SerializeField] private float walkNoiseRadius = 6f;
        [SerializeField] private float sprintNoiseRadius = 18f;
        [SerializeField] private float crouchNoiseMultiplier = 0.35f;
        [SerializeField] private float landingNoiseRadius = 12f;
        [SerializeField] private float noiseIntervalSeconds = 0.6f;

        private FirstPersonController controller;
        private float noiseTimer;
        private bool wasGrounded = true;
        private bool wasAirborne;

        private void Awake() => controller = GetComponent<FirstPersonController>();

        private void Update()
        {
            bool grounded = controller.IsGrounded;

            if (grounded && !wasGrounded && wasAirborne)
            {
                NoiseEvents.Raise(transform.position, landingNoiseRadius);
                wasAirborne = false;
            }
            if (!grounded) wasAirborne = true;
            wasGrounded = grounded;

            float speed = controller.CurrentSpeed;
            if (speed < 0.15f) { noiseTimer = 0f; return; }

            noiseTimer -= Time.deltaTime;
            if (noiseTimer > 0f) return;
            noiseTimer = noiseIntervalSeconds;

            float radius = controller.IsSprinting ? sprintNoiseRadius : walkNoiseRadius;
            if (controller.IsCrouching) radius *= crouchNoiseMultiplier;

            NoiseEvents.Raise(transform.position, radius);
        }
    }
}
