using UnityEngine;
using TRLM.Interaction;

namespace TRLM.Equipment
{
    /// <summary>
    /// World pickup that equips a WeaponDefinition directly into PlayerEquipment rather than the
    /// 10-slot carried-item PlayerInventory — firearms are physical equipment, not inventory
    /// items (see PlayerEquipment remarks). Used by the Sprint 07 combat test scene for the
    /// pistol/long-gun pickups; mirrors PickupItem's IInteractable shape.
    /// </summary>
    public class WeaponPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private WeaponDefinition weapon;

        public string InteractionPrompt => weapon != null ? $"Equip {weapon.displayName}" : "Equip";

        public void Interact(GameObject interactor)
        {
            if (weapon == null) return;

            var equipment = interactor.GetComponentInParent<PlayerEquipment>();
            if (equipment == null) return;

            if (equipment.TryEquip(weapon))
                gameObject.SetActive(false);
        }
    }
}
