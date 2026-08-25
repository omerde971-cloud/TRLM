using UnityEngine;
using TRLM.Equipment;
using TRLM.Inventory;
using TRLM.Interaction;
using TRLM.Story;
using TRLM.Dialogue;

namespace TRLM.World
{
    /// <summary>
    /// Narrative weapon-discovery in the cave. This is NOT a new weapon system — it drives the
    /// EXISTING <see cref="PlayerEquipment"/> / <see cref="PlayerInventory"/> flow as a one-shot
    /// story beat ("a survival cache left by the predecessors"). Interacting equips the cached
    /// weapon, draws it, hands over starter ammo, sets a StoryFlag so the cache does not respawn
    /// on reload, and self-deactivates. Persistence rides entirely on existing systems:
    /// the equipped weapon + magazine persist via EquipmentData, and the "already taken" state
    /// persists via <see cref="StoryFlags"/> (see <c>takenFlag</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public class CaveWeaponCache : MonoBehaviour, IInteractable
    {
        [Header("Weapon granted (uses existing PlayerEquipment.TryEquip)")]
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private EquipmentSlotType drawSlot = EquipmentSlotType.LongGunA;
        [SerializeField] private bool drawOnTake = true;

        [Header("Starter ammo (existing PlayerInventory.TryAddItem)")]
        [SerializeField] private ItemDefinition ammoItem;
        [SerializeField] private int ammoCount = 12;

        [Header("Persistence / prompt")]
        [Tooltip("StoryFlag id set on take; if already set at Start the cache is hidden (prevents respawn on reload).")]
        [SerializeField] private string takenFlag = "cave_weapon_taken";
        [SerializeField] private string promptText = "Take the shotgun";

        [Header("Optional discovery line (played once on take)")]
        [SerializeField] private DialogueLine discoveryLine;

        [Header("Visual to hide on take (defaults to this GameObject)")]
        [SerializeField] private GameObject visualToHide;

        public string InteractionPrompt => promptText;

        private void Start()
        {
            if (!string.IsNullOrEmpty(takenFlag) && StoryFlags.Instance != null && StoryFlags.Instance.Has(takenFlag))
                gameObject.SetActive(false);
        }

        public void Interact(GameObject interactor)
        {
            if (weapon == null || interactor == null) return;

            var equipment = interactor.GetComponentInParent<PlayerEquipment>();
            if (equipment == null) return;

            if (!equipment.TryEquip(weapon)) return;
            if (drawOnTake) equipment.SetActive(drawSlot);

            if (ammoItem != null && ammoCount > 0)
            {
                var inventory = interactor.GetComponentInParent<PlayerInventory>();
                if (inventory != null) inventory.TryAddItem(ammoItem, ammoCount);
            }

            if (discoveryLine != null && !string.IsNullOrEmpty(discoveryLine.id) && DialogueSystem.Instance != null)
                DialogueSystem.Instance.Play(discoveryLine);

            if (!string.IsNullOrEmpty(takenFlag) && StoryFlags.Instance != null)
                StoryFlags.Instance.Set(takenFlag);

            (visualToHide != null ? visualToHide : gameObject).SetActive(false);
        }
    }
}
