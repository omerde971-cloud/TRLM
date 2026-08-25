using System.Collections.Generic;
using UnityEngine;
using TRLM.Core;
using TRLM.World;
using TRLM.Inventory;
using TRLM.Companions;

namespace TRLM.Save
{
    /// <summary>
    /// Captures/restores authored world state identified by PersistentObjectId — collected loot,
    /// lit fires, discovered safe houses, used burial zones. Deliberately does not touch
    /// procedural/transient state (LootSpawnPoint's runtime-rolled loot, wildlife, rockfall) — see
    /// WorldStateData's class remarks for why those are excluded, not forgotten.
    /// </summary>
    public static class WorldStatePersistence
    {
        public static WorldStateData Capture()
        {
            var d = new WorldStateData();

            // Include inactive: a collected PickupItem is SetActive(false) — that's exactly the
            // state this loop is looking for, so excluding inactive objects (FindObjectsByType's
            // default) would miss every already-collected pickup.
            foreach (var pickup in Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pickup.gameObject.activeSelf) continue; // still in the world = not collected
                var pid = pickup.GetComponent<PersistentObjectId>();
                if (pid == null || string.IsNullOrEmpty(pid.Id)) continue;
                d.collectedLoot.Add(new PersistentFlagEntry { persistentId = pid.Id });
            }

            foreach (var fire in FirePoint.ActiveLitFires)
            {
                var pid = fire.GetComponent<PersistentObjectId>();
                if (pid == null || string.IsNullOrEmpty(pid.Id)) continue;
                d.litFires.Add(new PersistentFlagEntry { persistentId = pid.Id });
            }

            foreach (var safeHouse in Object.FindObjectsByType<SafeHouseArea>(FindObjectsSortMode.None))
            {
                var pid = safeHouse.GetComponent<PersistentObjectId>();
                // "Discovered" has no dedicated tracked flag on SafeHouseArea — PlayerInside only
                // reflects the current moment, not history. A safe, honest simplification: if the
                // player is standing in one right now it's obviously discovered; broader "ever
                // visited" tracking would need a new field on SafeHouseArea, out of scope here.
                if (pid == null || string.IsNullOrEmpty(pid.Id) || !safeHouse.PlayerInside) continue;
                d.discoveredSafeHouses.Add(new PersistentFlagEntry { persistentId = pid.Id });
            }

            foreach (var zone in Object.FindObjectsByType<BurialZone>(FindObjectsSortMode.None))
            {
                var pid = zone.GetComponent<PersistentObjectId>();
                if (pid == null || string.IsNullOrEmpty(pid.Id) || !zone.HasBuried) continue;
                d.usedBurialZones.Add(new BurialZoneEntry { persistentId = pid.Id, companionId = zone.BuriedCompanionId });
            }

            return d;
        }

        public static void Restore(WorldStateData d)
        {
            if (d == null) return;

            foreach (var entry in d.collectedLoot)
            {
                var pid = PersistentObjectId.Find(entry.persistentId);
                if (pid != null) pid.gameObject.SetActive(false);
            }

            foreach (var entry in d.litFires)
            {
                var pid = PersistentObjectId.Find(entry.persistentId);
                var fire = pid != null ? pid.GetComponent<FirePoint>() : null;
                fire?.RestoreLit();
            }

            // Discovered safe houses: no persistent visual/gameplay effect exists yet to reapply
            // (PlayerInside is presence-only, recomputed live by the trigger) — the list is
            // captured/carried for future UI (map reveal, fast travel) but intentionally has
            // nothing to restore against right now.

            foreach (var entry in d.usedBurialZones)
            {
                var pid = PersistentObjectId.Find(entry.persistentId);
                var zone = pid != null ? pid.GetComponent<BurialZone>() : null;
                zone?.RestoreBuried(entry.companionId);
            }
        }

        public static bool AnyBurialZoneUsed(WorldStateData d) => d != null && d.usedBurialZones.Count > 0;
    }
}
