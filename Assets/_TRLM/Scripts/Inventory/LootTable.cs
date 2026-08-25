using System;
using System.Collections.Generic;
using UnityEngine;

namespace TRLM.Inventory
{
    /// <summary>Weighted loot pool + a guaranteed-item list for critical-path reliability (e.g. the first house always has water+food).</summary>
    [CreateAssetMenu(menuName = "TRLM/Inventory/Loot Table", fileName = "NewLootTable")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ItemDefinition item;
            public float weight;
            public int minCount;
            public int maxCount;
        }

        public List<Entry> entries = new List<Entry>();

        /// <summary>Always granted regardless of the weighted roll — used to guarantee minimum survival items.</summary>
        public List<ItemDefinition> guaranteedItems = new List<ItemDefinition>();

        /// <summary>Rolls one weighted entry. Returns false if the table has no usable entries.</summary>
        public bool RollOne(out ItemDefinition item, out int count)
        {
            item = null;
            count = 0;

            float totalWeight = 0f;
            foreach (var e in entries)
                if (e.item != null && e.weight > 0f) totalWeight += e.weight;

            if (totalWeight <= 0f) return false;

            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;
            foreach (var e in entries)
            {
                if (e.item == null || e.weight <= 0f) continue;
                cumulative += e.weight;
                if (roll > cumulative) continue;

                item = e.item;
                count = UnityEngine.Random.Range(Mathf.Max(1, e.minCount), Mathf.Max(e.minCount, e.maxCount) + 1);
                if (item.category == ItemCategory.Ammo)
                    count = Mathf.Max(1, Mathf.RoundToInt(count * Mathf.Max(0f, TRLM.Progression.DifficultySettings.LootAmmoMultiplier)));
                return true;
            }

            return false;
        }
    }
}
