using UnityEngine;
using TRLM.Core;
using TRLM.Save;

namespace TRLM.Notebook
{
    /// <summary>
    /// Captures/restores the Kehanet Defteri — collected page ids (the notebook's own state) plus
    /// the PersistentObjectIds of already-taken world pickups (inactive-as-collected, the exact
    /// PickupItem/WorldStatePersistence convention) so a taken page never respawns on load.
    /// Static Capture()/Restore() adapter shape matching WorldStatePersistence/
    /// CompanionStatePersistence; SaveOrchestrator is the only caller.
    /// </summary>
    public static class NotebookStatePersistence
    {
        public static NotebookData Capture()
        {
            var d = new NotebookData();

            var notebook = ProphecyNotebook.Instance;
            if (notebook != null)
            {
                foreach (var id in notebook.CollectedIds)
                    d.collectedPageIds.Add(id);
            }

            // Include inactive: a collected ProphecyPagePickup is SetActive(false) — that IS the
            // state being captured (see WorldStatePersistence.Capture's identical remark).
            foreach (var pickup in Object.FindObjectsByType<ProphecyPagePickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pickup.gameObject.activeSelf) continue; // still in the world = not collected
                var pid = pickup.GetComponent<PersistentObjectId>();
                if (pid == null || string.IsNullOrEmpty(pid.Id)) continue;
                d.collectedPickupIds.Add(pid.Id);
            }

            return d;
        }

        public static void Restore(NotebookData d)
        {
            if (d == null) return;

            // Seed silently — no discovery lines/objective hooks re-fire for pages already found
            // last session (mirrors DialogueSystem.SeedPlayedOneShots).
            var notebook = ProphecyNotebook.Instance;
            if (notebook != null)
            {
                notebook.ClearCollected();
                notebook.SeedCollected(d.collectedPageIds);
            }

            foreach (var pickupId in d.collectedPickupIds)
            {
                var pid = PersistentObjectId.Find(pickupId);
                if (pid != null) pid.gameObject.SetActive(false);
            }
        }
    }
}
