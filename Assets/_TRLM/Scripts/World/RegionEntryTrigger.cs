using UnityEngine;
using TRLM.Progression;

namespace TRLM.World
{
    /// <summary>
    /// Reusable trigger volume that advances the objective flow to a configured step when the
    /// player enters it. Not single-purpose — place one per region boundary / area-of-interest
    /// that needs an automatic objective trigger (Sprint 06: EnterCoastalForest,
    /// ReachAbandonedHouse). Uses AdvanceTo, which is idempotent/order-tolerant (a no-op if the
    /// objective is already at or past this step), so overlapping triggers or re-entering an area
    /// is always safe.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RegionEntryTrigger : MonoBehaviour
    {
        [SerializeField] private ObjectiveStep step;
        [SerializeField] private string playerTag = "Player";

        [Header("Region (Sprint 10 — optional, save metadata only)")]
        [Tooltip("Leave empty for a trigger that's only about the objective step. Set it on the " +
                 "volume marking a named region boundary (e.g. \"Coastal Forest\") so save metadata " +
                 "can report where the player is without guessing from nearby object names.")]
        [SerializeField] private string regionName;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            ObjectiveSystem.Instance?.AdvanceTo(step);
            if (!string.IsNullOrEmpty(regionName))
                TRLM.Save.RegionTracker.CurrentRegionName = regionName;
        }
    }
}
