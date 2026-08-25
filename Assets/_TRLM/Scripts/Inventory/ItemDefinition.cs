using UnityEngine;

namespace TRLM.Inventory
{
    public enum ItemCategory
    {
        Medicine,
        Battery,
        Tool,
        SurvivalResource,
        SpecialObject,
        Wood,
        Ammo, // Sprint 07 (A1) — firearm ammunition. Not "useable" via PlayerInventory.UseSelectedItem's
              // generic switch (falls through to its default/false case); consumed only by
              // WeaponController.TryReload via TryRemoveItem.
        Bandage // Sprint 07 (A2) — stops/reduces active bleeding on use (Section 23). Separate from
                // Medicine since a bandage doesn't heal HP, it only treats the Bleeding status effect.
    }

    /// <summary>
    /// Data-only description of an item. Instances live as assets under
    /// Assets/_TRLM/ScriptableObjects/Items/ — PlayerInventory/PickupItem/LootTable all
    /// reference these rather than duplicating item data per-instance.
    /// </summary>
    [CreateAssetMenu(menuName = "TRLM/Inventory/Item Definition", fileName = "NewItem")]
    public class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        public Sprite icon; // may be null — UI falls back to a placeholder
        [TextArea] public string description;
        public ItemCategory category;
        public bool stackable = true;
        public int maxStack = 10;
    }
}
