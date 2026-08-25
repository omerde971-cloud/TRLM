using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TRLM.Interaction;
using TRLM.Player;

namespace TRLM.Boat
{
    /// <summary>
    /// Player-rowable boat for the production opening. The player controls progress with discrete
    /// SPACE strokes while the boat follows an authored approach line to the island.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RowboatController : MonoBehaviour, IInteractable
    {
        [Header("Seating")]
        [SerializeField] private Transform seatAnchor;
        [SerializeField] private Transform exitAnchor;
        [SerializeField] private bool startOccupiedOnSceneStart;
        [SerializeField] private string playerTag = "Player";

        [Header("Passengers")]
        [SerializeField] private Transform[] passengerRoots;
        [SerializeField] private Transform[] passengerAnchors;

        [Header("Rowing")]
        [SerializeField] private float strokeImpulse = 2.1f;
        [SerializeField] private float strokeCooldown = 0.7f;
        [SerializeField] private float acceleration = 7f;
        [SerializeField] private float drag = 1.25f;
        [SerializeField] private float maxForwardSpeed = 5f;

        [Header("Authored Route")]
        [SerializeField] private Transform routeTarget;
        [SerializeField] private float routeCorrectionStrength = 2.5f;
        [SerializeField] private float turnStabilization = 7f;
        [SerializeField] private float routeCompletionDistance = 1.5f;

        [Header("Sea Motion")]
        [Tooltip("Gentle vertical bob of the hull at sea. Metres.")]
        [SerializeField] private float bobAmplitude = 0.055f;
        [SerializeField] private float bobFrequency = 0.32f;
        [Tooltip("Idle roll sway in degrees; reads as the sea working under the hull.")]
        [SerializeField] private float rollAmplitude = 1.7f;
        [SerializeField] private float rollFrequency = 0.24f;
        [Tooltip("Extra forward pitch dip per stroke, degrees — sells the surge of each pull.")]
        [SerializeField] private float strokePitchDip = 1.6f;

        [Header("Presentation")]
        [SerializeField] private Transform[] oarVisuals;
        [SerializeField] private Vector3 oarStrokeEuler = new Vector3(-28f, 0f, 0f);
        [SerializeField] private AnimationCurve oarStrokeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AudioSource rowingAudioSource;
        [SerializeField] private AudioSource creakLoopSource;
        [SerializeField] private AudioClip oarWaterEnterClip;
        [SerializeField] private AudioClip oarPullClip;
        [SerializeField] private float oarWaterDelay = 0.08f;
        [SerializeField] private float oarPullDelay = 0.22f;
        [SerializeField, Range(0f, 1f)] private float oarWaterVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float oarPullVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float creakMovingVolume = 0.18f;

        private Rigidbody rb;
        private PlayerInputHandler input;
        private FirstPersonController playerController;
        private CharacterController characterController;
        private GameObject playerRoot;
        private float lastStrokeTime = -999f;
        private bool tutorialShown;
        private float currentSpeed;
        private float targetSpeed;
        private Vector3 routeStart;
        private Vector3 routeDirection;
        private readonly List<(Transform root, Transform previousParent)> passengerParents = new List<(Transform, Transform)>();
        private Quaternion[] oarRestRotations;
        private float oarStrokeTimer = 999f;
        private Coroutine strokeAudioRoutine;
        private const float OarStrokeDuration = 0.55f;

        public bool IsRowing { get; private set; }
        public GameObject CurrentPlayer => playerRoot;
        public float StrokeCooldown => strokeCooldown;
        public float CurrentSpeed => currentSpeed;

        public string InteractionPrompt => IsRowing ? "Exit Boat" : "Row Boat";

        private Rigidbody Rb => rb != null ? rb : (rb = GetComponent<Rigidbody>());

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            if (rowingAudioSource == null) rowingAudioSource = GetComponent<AudioSource>();
            CacheOarRestRotations();
        }

        private IEnumerator Start()
        {
            if (!startOccupiedOnSceneStart) yield break;

            // One frame late on purpose: SaveOrchestrator.Start consumes PendingLoad and restores
            // progression in the same frame the scene loads. Auto-seating must lose that race —
            // a continued save that is already past the landing would otherwise be dragged back
            // into the opening rowboat (or bounced to the exit anchor) after its position restore.
            yield return null;

            var objectives = TRLM.Progression.ObjectiveSystem.Instance;
            if (objectives != null && objectives.Current >= TRLM.Progression.ObjectiveStep.ReachLandingZone)
                yield break;

            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
                EnterBoat(player);
        }

        public void Interact(GameObject interactor)
        {
            if (IsRowing) ExitBoat();
            else EnterBoat(interactor);
        }

        private void EnterBoat(GameObject interactor)
        {
            playerRoot = ResolvePlayerRoot(interactor);
            if (playerRoot == null) return;

            input = playerRoot.GetComponent<PlayerInputHandler>();
            playerController = playerRoot.GetComponent<FirstPersonController>();
            characterController = playerRoot.GetComponent<CharacterController>();

            if (playerController != null) playerController.enabled = false;
            if (characterController != null) characterController.enabled = false;

            if (seatAnchor != null)
            {
                playerRoot.transform.SetParent(seatAnchor, false);
                playerRoot.transform.localPosition = Vector3.zero;
                playerRoot.transform.localRotation = Quaternion.identity;
            }

            if (input != null) input.JumpPressed += HandleStroke;

            ConfigureRoute();
            AttachPassengers();
            currentSpeed = 0f;
            targetSpeed = 0f;
            IsRowing = true;
            if (creakLoopSource != null && creakLoopSource.clip != null && !creakLoopSource.isPlaying)
            {
                creakLoopSource.volume = 0f;
                creakLoopSource.Play();
            }
            TRLM.Progression.ObjectiveSystem.Instance?.AdvanceTo(TRLM.Progression.ObjectiveStep.RowToIsland);

            if (!tutorialShown)
            {
                tutorialShown = true;
                TRLM.UI.SimpleTutorialPrompt.ShowGlobal("SPACE - Row toward the island", 3f);
            }
        }

        private void ExitBoat()
        {
            ExitBoatAt(exitAnchor);
        }

        public void ExitBoatAt(Transform targetAnchor)
        {
            if (input != null) input.JumpPressed -= HandleStroke;

            if (playerRoot != null)
            {
                playerRoot.transform.SetParent(null, true);
                if (targetAnchor != null)
                {
                    playerRoot.transform.SetPositionAndRotation(targetAnchor.position, targetAnchor.rotation);
                }

                if (characterController != null) characterController.enabled = true;
                if (playerController != null) playerController.enabled = true;
            }

            DetachPassengers();
            if (creakLoopSource != null && creakLoopSource.isPlaying)
                creakLoopSource.Stop();
            if (strokeAudioRoutine != null)
            {
                StopCoroutine(strokeAudioRoutine);
                strokeAudioRoutine = null;
            }

            IsRowing = false;
            currentSpeed = 0f;
            targetSpeed = 0f;
            playerRoot = null;
            input = null;
            playerController = null;
            characterController = null;
        }

        /// <summary>Called by LandingZone when the boat reaches shore — ends rowing without an interact.</summary>
        public void ForceExit() => ExitBoat();

        public bool RequestStroke()
        {
            if (!IsRowing) return false;
            if (Time.time - lastStrokeTime < strokeCooldown) return false;

            lastStrokeTime = Time.time;
            targetSpeed = Mathf.Min(maxForwardSpeed, targetSpeed + strokeImpulse);
            oarStrokeTimer = 0f;

            if (strokeAudioRoutine != null) StopCoroutine(strokeAudioRoutine);
            strokeAudioRoutine = StartCoroutine(PlayStrokeAudio());

            return true;
        }

        private void HandleStroke() => RequestStroke();

        private void FixedUpdate()
        {
            if (!IsRowing) return;

            targetSpeed = Mathf.MoveTowards(targetSpeed, 0f, drag * Time.fixedDeltaTime);
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
            if (creakLoopSource != null)
                creakLoopSource.volume = Mathf.Lerp(0f, creakMovingVolume, Mathf.InverseLerp(0.1f, maxForwardSpeed, currentSpeed));

            Vector3 moveDirection = routeDirection.sqrMagnitude > 0.001f ? routeDirection : transform.forward;
            Vector3 nextPosition = Rb.position + moveDirection * currentSpeed * Time.fixedDeltaTime;
            if (routeTarget != null)
            {
                float progress = Vector3.Dot(nextPosition - routeStart, routeDirection);
                Vector3 onRoute = routeStart + routeDirection * Mathf.Max(0f, progress);
                Vector3 correction = Vector3.ProjectOnPlane(onRoute - nextPosition, Vector3.up);
                nextPosition += Vector3.ClampMagnitude(correction * routeCorrectionStrength * Time.fixedDeltaTime, 0.35f);

                if (Vector3.Distance(nextPosition, routeTarget.position) <= routeCompletionDistance)
                    targetSpeed = Mathf.Min(targetSpeed, 1.5f);
            }

            // Procedural sea motion: two offset sines for a non-repeating bob, slow roll sway, and
            // a small pitch dip synced to each oar stroke. The rigidbody is kinematic, so this is
            // authored motion — cheap, deterministic, and inherited by the seated player camera.
            float t = Time.time;
            float bob = Mathf.Sin(t * Mathf.PI * 2f * bobFrequency) * bobAmplitude
                      + Mathf.Sin(t * Mathf.PI * 2f * bobFrequency * 0.63f + 1.7f) * bobAmplitude * 0.5f;
            nextPosition.y = routeStart.y + bob;

            float roll = Mathf.Sin(t * Mathf.PI * 2f * rollFrequency) * rollAmplitude
                       + Mathf.Sin(t * Mathf.PI * 2f * rollFrequency * 0.71f + 0.9f) * rollAmplitude * 0.4f;
            float strokeNorm = Mathf.Clamp01(oarStrokeTimer / OarStrokeDuration);
            float strokePitch = strokeNorm < 1f ? Mathf.Sin(strokeNorm * Mathf.PI) * strokePitchDip : 0f;
            float speedPitch = Mathf.InverseLerp(0f, maxForwardSpeed, currentSpeed) * 0.8f;

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up)
                                      * Quaternion.Euler(strokePitch + speedPitch, 0f, roll);
            Quaternion nextRotation = Quaternion.Slerp(Rb.rotation, targetRotation, turnStabilization * Time.fixedDeltaTime);
            Rb.MovePosition(nextPosition);
            Rb.MoveRotation(nextRotation);
        }

        private void Update()
        {
            AnimateOars();
        }

        private void ConfigureRoute()
        {
            routeStart = transform.position;
            Vector3 target = routeTarget != null ? routeTarget.position : transform.position + transform.forward * 25f;
            routeDirection = Vector3.ProjectOnPlane(target - routeStart, Vector3.up).normalized;
            if (routeDirection.sqrMagnitude < 0.001f)
                routeDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        }

        private void AttachPassengers()
        {
            passengerParents.Clear();
            int count = Mathf.Min(passengerRoots != null ? passengerRoots.Length : 0, passengerAnchors != null ? passengerAnchors.Length : 0);
            for (int i = 0; i < count; i++)
            {
                Transform passenger = passengerRoots[i];
                Transform anchor = passengerAnchors[i];
                if (passenger == null || anchor == null) continue;

                passengerParents.Add((passenger, passenger.parent));
                var nav = passenger.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null) nav.enabled = false;
                passenger.SetParent(anchor, false);
                passenger.localPosition = Vector3.zero;
                passenger.localRotation = Quaternion.identity;
            }
        }

        private void DetachPassengers()
        {
            foreach (var passenger in passengerParents)
            {
                if (passenger.root == null) continue;
                passenger.root.SetParent(passenger.previousParent, true);
                var nav = passenger.root.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null) nav.enabled = true;
            }
            passengerParents.Clear();
        }

        private void CacheOarRestRotations()
        {
            if (oarVisuals == null) return;
            oarRestRotations = new Quaternion[oarVisuals.Length];
            for (int i = 0; i < oarVisuals.Length; i++)
                oarRestRotations[i] = oarVisuals[i] != null ? oarVisuals[i].localRotation : Quaternion.identity;
        }

        private void AnimateOars()
        {
            if (oarVisuals == null || oarRestRotations == null) return;
            oarStrokeTimer += Time.deltaTime;
            float normalized = Mathf.Clamp01(oarStrokeTimer / OarStrokeDuration);
            float stroke = normalized < 1f ? Mathf.Sin(oarStrokeCurve.Evaluate(normalized) * Mathf.PI) : 0f;

            for (int i = 0; i < oarVisuals.Length; i++)
            {
                if (oarVisuals[i] == null) continue;
                Quaternion offset = Quaternion.Euler(oarStrokeEuler * stroke);
                oarVisuals[i].localRotation = oarRestRotations[i] * offset;
            }
        }

        private IEnumerator PlayStrokeAudio()
        {
            if (rowingAudioSource == null) yield break;

            if (oarWaterEnterClip != null)
            {
                if (oarWaterDelay > 0f) yield return new WaitForSeconds(oarWaterDelay);
                rowingAudioSource.PlayOneShot(oarWaterEnterClip, oarWaterVolume);
            }

            if (oarPullClip != null)
            {
                float remainingDelay = Mathf.Max(0f, oarPullDelay - oarWaterDelay);
                if (remainingDelay > 0f) yield return new WaitForSeconds(remainingDelay);
                rowingAudioSource.PlayOneShot(oarPullClip, oarPullVolume);
            }

            strokeAudioRoutine = null;
        }

        private static GameObject ResolvePlayerRoot(GameObject interactor)
        {
            if (interactor == null) return null;
            var inputOnSelf = interactor.GetComponent<PlayerInputHandler>();
            if (inputOnSelf != null) return inputOnSelf.gameObject;

            var inputInParent = interactor.GetComponentInParent<PlayerInputHandler>();
            if (inputInParent != null) return inputInParent.gameObject;

            var controller = interactor.GetComponentInParent<FirstPersonController>();
            if (controller != null) return controller.gameObject;

            return interactor.CompareTag("Player") ? interactor : null;
        }
    }
}
