using UnityEngine;
using TRLM.Interaction;

namespace TRLM.Dialogue
{
    /// <summary>
    /// Placeable trigger for contextual/exploration lines ("arriving at island", "first wolf sign",
    /// etc. — Sprint 11 Phase 6). Either fires on player trigger-enter or via IInteractable, whichever
    /// a level designer wants for a given beat. Thin wrapper over DialogueSystem.Play — no branching,
    /// no conditions beyond the built-in DialogueLine.oneShot guard.
    /// </summary>
    public class DialogueTrigger : MonoBehaviour, IInteractable
    {
        [SerializeField] private DialogueLine line;
        [SerializeField] private bool triggerOnEnter = true;
        [SerializeField] private string playerTag = "Player";

        public string InteractionPrompt => "...";

        public void Interact(GameObject interactor) => Fire();

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnEnter) return;
            if (!other.CompareTag(playerTag)) return;
            Fire();
        }

        private void Fire()
        {
            if (line == null || DialogueSystem.Instance == null) return;
            DialogueSystem.Instance.Play(line);
        }
    }
}
