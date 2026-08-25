using UnityEngine;

namespace TRLM.Interaction
{
    /// <summary>
    /// Implemented by anything the player can press E on. Kept deliberately minimal —
    /// no loot/inventory concepts here, that belongs to future systems.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Short text shown in the "E — Interact" prompt, e.g. "Open Door".</summary>
        string InteractionPrompt { get; }

        void Interact(GameObject interactor);
    }
}
