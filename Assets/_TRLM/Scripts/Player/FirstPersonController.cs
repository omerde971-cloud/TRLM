using System.Collections.Generic;
using UnityEngine;
using TRLM.Survival;

namespace TRLM.Player
{
    /// <summary>
    /// Slow, atmospheric survival-style first-person movement (not an arcade FPS).
    /// Uses CharacterController for grounding/collision and applies smoothed
    /// acceleration/deceleration rather than instant velocity snapping.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private StaminaSystem stamina;

        [Header("Speeds (m/s)")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float sprintSpeed = 4.2f;
        [SerializeField] private float crouchSpeed = 1.2f;

        [Header("Acceleration")]
        [SerializeField] private float accelerationTime = 0.18f;
        [SerializeField] private float decelerationTime = 0.12f;

        [Header("Jump / Gravity")]
        [SerializeField] private float jumpHeight = 0.9f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Crouch")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchingHeight = 1.1f;
        [SerializeField] private float crouchTransitionSpeed = 8f;

        [Header("Slope Handling")]
        [SerializeField] private float slopeLimit = 45f;
        [SerializeField] private float slideFriction = 0.15f;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private bool isCrouching;
        private float targetHeight;

        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching => isCrouching;
        public float CurrentSpeed => horizontalVelocity.magnitude;

        // Generic, additive movement-modifier API (Sprint 06) so other systems (BodyCarry, future
        // status effects) can penalize speed / block sprint without this file knowing about them.
        // Same worst-of pattern as StaminaRegenModifier.
        public float SpeedMultiplier { get; private set; } = 1f;
        public bool SprintAllowed { get; private set; } = true;
        private readonly Dictionary<string, float> speedPenalties = new Dictionary<string, float>();
        private readonly HashSet<string> sprintBlockers = new HashSet<string>();

        public void SetSpeedModifier(string sourceId, float multiplier)
        {
            speedPenalties[sourceId] = multiplier;
            RecomputeModifiers();
        }

        public void ClearSpeedModifier(string sourceId)
        {
            speedPenalties.Remove(sourceId);
            RecomputeModifiers();
        }

        public void SetSprintBlocked(string sourceId, bool blocked)
        {
            if (blocked) sprintBlockers.Add(sourceId);
            else sprintBlockers.Remove(sourceId);
            SprintAllowed = sprintBlockers.Count == 0;
        }

        private void RecomputeModifiers()
        {
            SpeedMultiplier = 1f;
            foreach (var v in speedPenalties.Values) SpeedMultiplier = Mathf.Min(SpeedMultiplier, v);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.slopeLimit = slopeLimit;
            targetHeight = standingHeight;
            ApplyHeight(standingHeight);
        }

        private void OnEnable()
        {
            if (input != null)
                input.JumpPressed += HandleJumpPressed;
        }

        private void OnDisable()
        {
            if (input != null)
                input.JumpPressed -= HandleJumpPressed;
        }

        private void Update()
        {
            IsGrounded = controller.isGrounded;

            UpdateCrouch();
            UpdateHorizontalMovement();
            UpdateVerticalMovement();

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void UpdateCrouch()
        {
            if (input != null)
                isCrouching = input.CrouchHeld;

            targetHeight = isCrouching ? crouchingHeight : standingHeight;
            float newHeight = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            ApplyHeight(newHeight);
        }

        private void ApplyHeight(float height)
        {
            controller.height = height;
            Vector3 center = controller.center;
            center.y = height * 0.5f;
            controller.center = center;
        }

        private void UpdateHorizontalMovement()
        {
            Vector2 raw = input != null ? input.MoveInput : Vector2.zero;
            bool wantsSprint = input != null && input.SprintHeld && raw.y > 0.1f && !isCrouching;

            IsSprinting = false;
            float targetSpeed;
            if (isCrouching)
            {
                targetSpeed = crouchSpeed;
            }
            else if (wantsSprint && SprintAllowed && (stamina == null || !stamina.IsExhausted))
            {
                targetSpeed = sprintSpeed;
                IsSprinting = stamina == null || stamina.ConsumeSprint(Time.deltaTime);
            }
            else
            {
                targetSpeed = walkSpeed;
            }

            targetSpeed *= SpeedMultiplier;

            Vector3 wishDir = (transform.right * raw.x + transform.forward * raw.y);
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
            Vector3 targetVelocity = wishDir * targetSpeed;

            float smoothTime = targetVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude
                ? accelerationTime
                : decelerationTime;
            float t = smoothTime <= 0f ? 1f : Time.deltaTime / smoothTime;
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVelocity, Mathf.Clamp01(t));

            if (IsGrounded && OnSteepSlope(out Vector3 slideDirection))
            {
                horizontalVelocity += slideDirection * (Mathf.Abs(gravity) * slideFriction * Time.deltaTime);
            }
        }

        private bool OnSteepSlope(out Vector3 slideDirection)
        {
            slideDirection = Vector3.zero;
            if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, controller.height * 0.6f + 0.3f))
                return false;

            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle <= controller.slopeLimit) return false;

            slideDirection = Vector3.ProjectOnPlane(Vector3.down, hit.normal).normalized;
            return true;
        }

        private void UpdateVerticalMovement()
        {
            if (IsGrounded && verticalVelocity < 0f)
                verticalVelocity = groundedStickForce;

            verticalVelocity += gravity * Time.deltaTime;
        }

        private void HandleJumpPressed()
        {
            if (!IsGrounded || isCrouching) return;
            if (stamina != null && !stamina.ConsumeJump()) return;

            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
}
