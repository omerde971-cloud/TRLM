using UnityEngine;
using TRLM.Interaction;

namespace TRLM.Inventory
{
    /// <summary>World pickup for a single ItemDefinition + count. Used both for hand-placed loot and for dropped items.</summary>
    public class PickupItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private int count = 1;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.45f;

        /// <summary>Fires once a pickup successfully transfers into a PlayerInventory. Added for
        /// ObjectiveSystem's SearchHouse step (Sprint 06) — any successful pickup counts as
        /// "searched", a deliberate simplification documented in ObjectiveSystem's remarks.</summary>
        public static event System.Action<PickupItem> OnAnyItemPickedUp;

        public string InteractionPrompt
        {
            get
            {
                if (item == null) return "Pick Up";
                var inventory = CachedInteractorInventory;
                if (inventory != null && !inventory.HasRoomFor(item, count))
                    return "Inventory Full";
                return $"Pick Up {item.displayName}";
            }
        }

        // Cached from the last Interact() call purely so InteractionPrompt (a getter with no
        // interactor parameter) can reflect "full" state without a scene-wide search.
        private PlayerInventory CachedInteractorInventory { get; set; }

        /// <summary>Used by PlayerInventory when spawning a dropped-item pickup, and by LootSpawnPoint.</summary>
        public void Configure(ItemDefinition newItem, int newCount)
        {
            item = newItem;
            count = Mathf.Max(1, newCount);
        }

        public void Interact(GameObject interactor)
        {
            if (item == null) return;

            var inventory = interactor.GetComponentInParent<PlayerInventory>();
            CachedInteractorInventory = inventory;
            if (inventory == null) return;

            if (inventory.TryAddItem(item, count))
            {
                if (pickupClip != null) AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);
                gameObject.SetActive(false);
                OnAnyItemPickedUp?.Invoke(this);
            }
            // Full inventory: leave the pickup in the world, do not delete it.
        }
    }
}
