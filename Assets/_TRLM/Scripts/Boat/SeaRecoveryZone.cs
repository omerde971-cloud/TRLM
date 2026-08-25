using UnityEngine;
using TRLM.Player;

namespace TRLM.Boat
{
    /// <summary>
    /// Production safety net for Sprint 11: if the player or rowboat drops into the unsafe sea
    /// volume, snap them back to an authored recovery marker instead of allowing an infinite fall.
    /// This is deliberately not a swimming system.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SeaRecoveryZone : MonoBehaviour
    {
        [SerializeField] private Transform playerRecoveryMarker;
        [SerializeField] private Transform boatRecoveryMarker;
        [SerializeField] private RowboatController rowboat;
        [SerializeField] private string playerTag = "Player";

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var playerInput = other.GetComponentInParent<PlayerInputHandler>();
            if (playerInput != null || other.CompareTag(playerTag))
            {
                RecoverPlayer(playerInput != null ? playerInput.gameObject : other.gameObject);
                return;
            }

            var boat = other.GetComponentInParent<RowboatController>();
            if (boat != null)
                RecoverBoat(boat);
        }

        private void RecoverPlayer(GameObject player)
        {
            if (player == null || playerRecoveryMarker == null) return;

            if (rowboat == null) rowboat = FindFirstObjectByType<RowboatController>();
            if (rowboat != null && rowboat.IsRowing)
                rowboat.ExitBoatAt(playerRecoveryMarker);

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetParent(null, true);
            player.transform.SetPositionAndRotation(playerRecoveryMarker.position, playerRecoveryMarker.rotation);
            if (controller != null) controller.enabled = true;

            TRLM.UI.SimpleTutorialPrompt.ShowGlobal("Recovered to safe shore", 2.5f);
        }

        private void RecoverBoat(RowboatController boat)
        {
            if (boat == null || boatRecoveryMarker == null) return;
            boat.ForceExit();
            var rb = boat.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            boat.transform.SetPositionAndRotation(boatRecoveryMarker.position, boatRecoveryMarker.rotation);
        }
    }
}
