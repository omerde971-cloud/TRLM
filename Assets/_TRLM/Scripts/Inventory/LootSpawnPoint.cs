using UnityEngine;

namespace TRLM.Inventory
{
    /// <summary>
    /// Rolls a LootTable on Start and spawns PickupItem instances nearby. Designed to sit
    /// alongside an existing WorldMarker (LootPoint/SafeHouse) placed in an earlier sprint —
    /// it reads nothing from WorldMarker, it's just meant to be added as a sibling component.
    /// </summary>
    public class LootSpawnPoint : MonoBehaviour
    {
        [SerializeField] private LootTable table;
        [SerializeField] private int rollCount = 3;
        [SerializeField] private float scatterRadius = 1.25f;
        [SerializeField] private GameObject pickupVisualPrefab; // placeholder visual, see note below

        private void Start()
        {
            if (table == null)
            {
                Debug.LogWarning($"[LootSpawnPoint] {name} has no LootTable assigned — nothing spawned.");
                return;
            }

            foreach (var guaranteed in table.guaranteedItems)
            {
                if (guaranteed == null) continue;
                SpawnPickup(guaranteed, 1);
            }

            for (int i = 0; i < rollCount; i++)
            {
                if (table.RollOne(out ItemDefinition item, out int count))
                    SpawnPickup(item, count);
            }
        }

        private void SpawnPickup(ItemDefinition item, int count)
        {
            Vector2 offset = Random.insideUnitCircle * scatterRadius;
            Vector3 pos = transform.position + new Vector3(offset.x, 0f, offset.y);

            // No real pickup meshes yet (future art pass) — a plain primitive stands in so the
            // loot loop is fully testable now.
            GameObject go = pickupVisualPrefab != null
                ? Instantiate(pickupVisualPrefab, pos, Quaternion.identity, transform)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            if (pickupVisualPrefab == null)
            {
                go.transform.SetParent(transform);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.3f;
            }
            go.name = $"Loot_{item.itemId}";

            var pickup = go.GetComponent<PickupItem>();
            if (pickup == null) pickup = go.AddComponent<PickupItem>();
            pickup.Configure(item, count);
        }
    }
}
