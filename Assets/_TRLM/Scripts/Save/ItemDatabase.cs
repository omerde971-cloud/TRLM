using UnityEngine;
using TRLM.Inventory;
using TRLM.Equipment;

namespace TRLM.Save
{
    /// <summary>
    /// Save persistence never serializes a direct ItemDefinition/WeaponDefinition (Unity Object)
    /// reference — it stores the definition's stable string id and looks it up here on restore.
    /// One authored asset (Assets/_TRLM/ScriptableObjects/ItemDatabase.asset) lists every
    /// ItemDefinition/WeaponDefinition in the project; SaveOrchestrator holds the one reference to it.
    /// </summary>
    [CreateAssetMenu(menuName = "TRLM/Save/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        public ItemDefinition[] items = System.Array.Empty<ItemDefinition>();
        public WeaponDefinition[] weapons = System.Array.Empty<WeaponDefinition>();

        public ItemDefinition FindItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            foreach (var i in items)
                if (i != null && i.itemId == itemId) return i;
            return null;
        }

        public WeaponDefinition FindWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            foreach (var w in weapons)
                if (w != null && w.weaponId == weaponId) return w;
            return null;
        }
    }
}
