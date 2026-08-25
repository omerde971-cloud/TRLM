using System;
using UnityEngine;

namespace TRLM.Boat
{
    /// <summary>
    /// Beach-side trigger. When a RowboatController-carrying rowboat enters, ends its rowing
    /// state and fires OnLanded for the Objective Flow system to advance. No docking simulation.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LandingZone : MonoBehaviour
    {
        [SerializeField] private Transform landingExitMarker;

        public static event Action OnLanded;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var boat = other.GetComponentInParent<RowboatController>();
            if (boat == null) return;

            if (boat.IsRowing) boat.ExitBoatAt(landingExitMarker);
            OnLanded?.Invoke();
            TRLM.Progression.ObjectiveSystem.Instance?.AdvanceTo(TRLM.Progression.ObjectiveStep.ReachLandingZone);
            TRLM.UI.SimpleTutorialPrompt.ShowGlobal("Landing reached", 2.5f);
        }
    }
}
