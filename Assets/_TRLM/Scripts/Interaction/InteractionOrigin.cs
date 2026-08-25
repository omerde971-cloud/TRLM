using UnityEngine;

namespace TRLM.Interaction
{
    /// <summary>
    /// Raycasts forward from the camera every frame looking for an IInteractable.
    /// Fires E via PlayerInputHandler's InteractPressed event. UI (InteractionPromptUI)
    /// reads CurrentPrompt to show the "E — Interact" hint.
    /// </summary>
    public class InteractionOrigin : MonoBehaviour
    {
        [SerializeField] private TRLM.Player.PlayerInputHandler input;
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private LayerMask interactableMask = ~0;

        private IInteractable currentTarget;
        private Collider lastHitCollider;

        public string CurrentPrompt => currentTarget?.InteractionPrompt;
        public bool HasTarget => currentTarget != null;

        private void OnEnable()
        {
            if (input != null)
                input.InteractPressed += HandleInteractPressed;
        }

        private void OnDisable()
        {
            if (input != null)
                input.InteractPressed -= HandleInteractPressed;
        }

        private void Update()
        {
            RefreshTarget();
        }

        private void RefreshTarget()
        {
            if (raycastCamera == null) { currentTarget = null; lastHitCollider = null; return; }

            Ray ray = new Ray(raycastCamera.transform.position, raycastCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactableMask, QueryTriggerInteraction.Collide))
            {
                // GetComponentInParent walks the hierarchy — only worth paying for when the
                // raycast lands on a different collider than last frame, not on every hit frame.
                if (hit.collider != lastHitCollider)
                {
                    lastHitCollider = hit.collider;
                    currentTarget = hit.collider.GetComponentInParent<IInteractable>();
                }
                return;
            }

            lastHitCollider = null;
            currentTarget = null;
        }

        private void HandleInteractPressed()
        {
            currentTarget?.Interact(gameObject);
        }
    }
}
