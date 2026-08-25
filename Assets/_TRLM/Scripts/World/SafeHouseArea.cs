using System;
using UnityEngine;

namespace TRLM.World
{
    /// <summary>
    /// Trigger-volume companion to an existing SafeHouse-type WorldMarker. Tracks whether the
    /// player is currently inside so SleepInteraction can gate itself. WetnessSystem already
    /// scans for SafeHouse WorldMarkers directly (see WetnessSystem.IsInsideSafeHouse), so no
    /// extra "dry faster" hook is added here — that behavior already exists without this
    /// component's help; this class only owns presence detection.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SafeHouseArea : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";

        public event Action<bool> OnPlayerPresenceChanged;

        public bool PlayerInside { get; private set; }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            PlayerInside = true;
            OnPlayerPresenceChanged?.Invoke(true);
            TRLM.Progression.ObjectiveSystem.Instance?.AdvanceTo(TRLM.Progression.ObjectiveStep.ReachSafeHouse);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            PlayerInside = false;
            OnPlayerPresenceChanged?.Invoke(false);
        }
    }
}
