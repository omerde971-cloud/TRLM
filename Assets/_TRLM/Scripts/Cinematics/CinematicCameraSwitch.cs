using UnityEngine;

namespace TRLM.Cinematics
{
    /// <summary>
    /// Bridges a <see cref="CinematicDirector"/> to a dedicated cinematic Camera without putting a
    /// CinemachineBrain on the first-person gameplay camera. TRLM's gameplay camera is parented under
    /// a moving player rig and driven directly by the controller, so a Brain on it writes a
    /// world-space transform that fights the rig's fixed local offset (it visibly corrupts the FP
    /// view). Instead the Brain + CinemachineCamera(s) live on a SEPARATE camera object; while the
    /// beat plays this switch makes that the ONLY active camera — enabling the cinematic camera and
    /// disabling the gameplay camera's Camera component (a single active render target, no fragile
    /// depth-stacked compositing) — then restores exactly the reverse afterwards.
    ///
    /// Poll-based on the director's IsPlaying so there is no UnityEvent wiring, and every director
    /// exit path (complete/skip/abort) flips IsPlaying false, restoring the gameplay camera the same
    /// frame — guaranteeing no duplicate active camera lingers. Only the Camera COMPONENT is toggled,
    /// so the gameplay camera's AudioListener and transform-following keep running underneath.
    /// </summary>
    public class CinematicCameraSwitch : MonoBehaviour
    {
        [SerializeField] private CinematicDirector director;
        [Tooltip("Dedicated cinematic camera (own CinemachineBrain). Kept disabled except while the cinematic plays.")]
        [SerializeField] private Camera cinematicCamera;
        [Tooltip("The first-person gameplay camera. Auto-found (Camera.main) when left null.")]
        [SerializeField] private Camera gameplayCamera;

        private bool lastState;

        private void Awake()
        {
            if (director == null) director = GetComponent<CinematicDirector>();
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (cinematicCamera != null) cinematicCamera.enabled = false;
        }

        private void LateUpdate()
        {
            if (director == null || cinematicCamera == null) return;
            bool want = director.IsPlaying;
            if (want == lastState) return;
            lastState = want;

            cinematicCamera.enabled = want;
            if (gameplayCamera != null) gameplayCamera.enabled = !want;
        }

        private void OnDisable()
        {
            // Never leave the view on the cinematic camera if this component/scene is torn down.
            if (cinematicCamera != null) cinematicCamera.enabled = false;
            if (gameplayCamera != null) gameplayCamera.enabled = true;
            lastState = false;
        }
    }
}
