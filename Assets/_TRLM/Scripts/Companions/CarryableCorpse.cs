using UnityEngine;
using TRLM.Interaction;

namespace TRLM.Companions
{
    /// <summary>
    /// Makes a dead CompanionAI pickable via E. Only responds once HealthSystem.IsDead is true;
    /// while alive this component simply reports no interaction. Delegates the actual pickup
    /// logic to BodyCarry on the player.
    /// </summary>
    [RequireComponent(typeof(CompanionAI))]
    public class CarryableCorpse : MonoBehaviour, IInteractable
    {
        private CompanionAI companion;

        private CompanionAI Companion => companion != null ? companion : (companion = GetComponent<CompanionAI>());

        public string InteractionPrompt => Companion.IsDead ? "Carry Body" : null;

        public void Interact(GameObject interactor)
        {
            if (!Companion.IsDead) return;

            var carry = interactor.GetComponent<BodyCarry>();
            carry?.PickUp(this);
        }
    }
}
