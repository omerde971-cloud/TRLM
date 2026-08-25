using UnityEngine;

namespace TRLM.Player
{
    /// <summary>
    /// First-person mouse look. Deliberately decoupled from Health/Stamina — it only
    /// knows about PlayerInputHandler. Yaw is applied to the player body (so movement
    /// stays aligned with the view) and pitch is applied to this camera transform only,
    /// which keeps the door open for a separate visible-body root later and for future
    /// third-person cinematic transitions (swap what drives this transform, not the body).
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private Transform bodyRoot;

        [Header("Sensitivity")]
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Smoothing")]
        // Quality Pass #1: 0.03s read as a one-frame-plus lag behind the mouse during aiming/
        // scanning, disconnected from the player's snappier movement. Lowered to steady raw jitter
        // without reintroducing that lag — Game Director to confirm feel.
        [SerializeField] private float rotationSmoothTime = 0.01f;

        [Header("Recoil (Sprint 07 — additive kick, not a rewrite)")]
        [SerializeField] private float recoilRecoverySpeed = 6f;

        private float yaw;
        private float pitch;
        private Vector2 smoothedLookVelocity;
        private Vector2 currentLook;

        // Additive recoil offsets, separate from the player-driven pitch/yaw above, that decay
        // back to zero over time. Kept as pure offsets rather than folded into `pitch` itself so
        // recoil recovery never fights with — or gets permanently baked into — the player's own
        // look input.
        private float recoilOffsetPitch;
        private float recoilOffsetYaw;

        private void Awake()
        {
            if (input == null)
                input = GetComponentInParent<PlayerInputHandler>();

            if (bodyRoot == null)
                bodyRoot = input != null ? input.transform : GetComponentInParent<PlayerInputHandler>()?.transform;

            if (bodyRoot != null)
                yaw = bodyRoot.eulerAngles.y;
            pitch = transform.localEulerAngles.x;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (input == null) return;

            Vector2 target = input.LookInput * sensitivity;
            currentLook = Vector2.SmoothDamp(currentLook, target, ref smoothedLookVelocity, rotationSmoothTime);

            yaw += currentLook.x;
            pitch = Mathf.Clamp(pitch - currentLook.y, minPitch, maxPitch);

            if (bodyRoot != null)
                bodyRoot.rotation = Quaternion.Euler(0f, yaw, 0f);

            recoilOffsetPitch = Mathf.Lerp(recoilOffsetPitch, 0f, Time.deltaTime * recoilRecoverySpeed);
            recoilOffsetYaw = Mathf.Lerp(recoilOffsetYaw, 0f, Time.deltaTime * recoilRecoverySpeed);

            transform.localRotation = Quaternion.Euler(pitch + recoilOffsetPitch, recoilOffsetYaw, 0f);
        }

        /// <summary>
        /// Additive weapon-fire recoil kick (Sprint 07). WeaponController calls this per shot
        /// with a weapon's WeaponDefinition.recoilPitch/recoilYawRandom — it does not touch
        /// `pitch`/`yaw` (the player's own look state) directly, only the separate recoil
        /// offsets above, which recover smoothly on their own each frame.
        /// </summary>
        public void AddRecoilKick(float pitchDegrees, float yawDegrees)
        {
            recoilOffsetPitch -= pitchDegrees; // negative pitch = looking up in this component's convention
            recoilOffsetYaw += yawDegrees;
        }
    }
}
