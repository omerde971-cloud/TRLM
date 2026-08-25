using UnityEngine;

namespace TRLM.Save
{
    /// <summary>
    /// Marks an authored volume as an approved manual-save location (safe house, protected camp).
    /// Same trigger-volume pattern as SafeHouseArea, deliberately separate from it — not every
    /// SafeHouseArea is necessarily meant to allow saving, and not every save zone need be a full
    /// safe house (e.g. a story-authored camp). Place one of these where manual save should work;
    /// SaveOrchestrator.CanManualSave checks whether the player is standing inside any of them.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ManualSaveZone : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";

        public static readonly System.Collections.Generic.List<ManualSaveZone> ActiveZonesPlayerIsIn = new System.Collections.Generic.List<ManualSaveZone>();

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (!ActiveZonesPlayerIsIn.Contains(this)) ActiveZonesPlayerIsIn.Add(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            ActiveZonesPlayerIsIn.Remove(this);
        }

        private void OnDisable() => ActiveZonesPlayerIsIn.Remove(this);

        public static bool PlayerInAnyZone => ActiveZonesPlayerIsIn.Count > 0;
    }
}
