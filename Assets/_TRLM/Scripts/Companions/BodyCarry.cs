using UnityEngine;
using TRLM.Survival;
using TRLM.Player;

namespace TRLM.Companions
{
    /// <summary>
    /// E-to-carry a dead companion's corpse. Re-parents it under a CarryAnchor while held, disables
    /// its NavMeshAgent/Collider, and drops it back into world space on release.
    ///
    /// Movement penalty: Sprint 06 added a generic speed-modifier / sprint-block API to
    /// FirstPersonController (SetSpeedModifier/SetSprintBlocked), so carrying now applies a real
    /// walk-speed slowdown and blocks sprinting outright, in addition to the pre-existing
    /// stamina-regen penalty via StaminaRegenModifier (kept as-is — carrying is still tiring even
    /// while walking).
    /// </summary>
    public class BodyCarry : MonoBehaviour
    {
        private const string CarryPenaltyId = "CarryingBody";

        [SerializeField] private Transform carryAnchor; // created under camera if not assigned
        [SerializeField] private float carryStaminaRegenMultiplier = 0.5f;
        [SerializeField] private float carrySpeedMultiplier = 0.5f;

        private CarryableCorpse carried;
        private StaminaRegenModifier regenModifier;
        private FirstPersonController firstPersonController;

        public bool IsCarrying => carried != null;
        public CarryableCorpse Carried => carried;

        private void Awake()
        {
            regenModifier = GetComponentInChildren<StaminaRegenModifier>();
            firstPersonController = GetComponent<FirstPersonController>();
            if (firstPersonController == null) firstPersonController = GetComponentInParent<FirstPersonController>();

            if (carryAnchor == null)
            {
                Transform cam = transform.Find("CameraRoot/MainCamera");
                Transform parent = cam != null ? cam : transform;
                var go = new GameObject("CarryAnchor");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0f, -0.6f, 1.2f);
                carryAnchor = go.transform;
            }
        }

        public void PickUp(CarryableCorpse corpse)
        {
            if (corpse == null || IsCarrying) return;

            carried = corpse;
            var companion = corpse.GetComponent<CompanionAI>();
            var agent = corpse.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            var col = corpse.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            corpse.transform.SetParent(carryAnchor, false);
            corpse.transform.localPosition = Vector3.zero;
            corpse.transform.localRotation = Quaternion.identity;

            regenModifier?.SetPenalty(CarryPenaltyId, carryStaminaRegenMultiplier);
            firstPersonController?.SetSpeedModifier(CarryPenaltyId, carrySpeedMultiplier);
            firstPersonController?.SetSprintBlocked(CarryPenaltyId, true);
        }

        public void Drop()
        {
            if (!IsCarrying) return;

            carried.transform.SetParent(null, true);
            carried.transform.position = transform.position + transform.forward * 1f;

            var col = carried.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            // NavMeshAgent stays disabled — it's a corpse, it shouldn't repath.

            regenModifier?.ClearPenalty(CarryPenaltyId);
            firstPersonController?.ClearSpeedModifier(CarryPenaltyId);
            firstPersonController?.SetSprintBlocked(CarryPenaltyId, false);
            carried = null;
        }

        /// <summary>Used by BurialZone to remove the corpse from play without a normal Drop.</summary>
        public void ClearCarriedReference()
        {
            regenModifier?.ClearPenalty(CarryPenaltyId);
            firstPersonController?.ClearSpeedModifier(CarryPenaltyId);
            firstPersonController?.SetSprintBlocked(CarryPenaltyId, false);
            carried = null;
        }
    }
}
