using UnityEngine;

namespace TRLM.Interaction
{
    /// <summary>Proves IInteractable works for a one-shot "consume and disappear" object. Not real inventory.</summary>
    public class TestPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string itemName = "Test Item";

        public string InteractionPrompt => $"Pick Up {itemName}";

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[TestPickup] {interactor.name} picked up {itemName}.");
            gameObject.SetActive(false);
        }
    }
}
