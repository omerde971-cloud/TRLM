using System.Collections;
using UnityEngine;
using TRLM.Interaction;
using TRLM.Flow;

namespace TRLM.Progression
{
    /// <summary>
    /// Minimal mechanical transition out of the pre-island cinematic scene (Sprint 06 —
    /// "player should not need to manually open scenes"). Deliberately not a real cinematic: no
    /// dialogue, no character animation, just an E-to-interact on PreparationTrigger, a short
    /// configurable placeholder delay, then AdvanceTo(PreparationComplete) and a scene load into
    /// the island. A Collider on this GameObject also lets it be entered as a trigger volume
    /// (OnTriggerEnter, tag-gated) as an alternative to interacting, since either is a reasonable
    /// "reached departure" signal for this placeholder hook.
    /// </summary>
    public class PreparationSequence : MonoBehaviour, IInteractable
    {
        [SerializeField] private float placeholderDelaySeconds = 3f;
        [SerializeField] private string islandSceneName = "20_Island_Blockout";
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool triggerOnEnter = true;

        private bool started;

        public string InteractionPrompt => started ? "Departing..." : "Depart for the Island";

        public void Interact(GameObject interactor)
        {
            BeginDeparture();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnEnter) return;
            if (!other.CompareTag(playerTag)) return;
            BeginDeparture();
        }

        private void BeginDeparture()
        {
            if (started) return;
            started = true;
            StartCoroutine(DepartureRoutine());
        }

        private IEnumerator DepartureRoutine()
        {
            yield return new WaitForSeconds(placeholderDelaySeconds);

            ObjectiveSystem.Instance?.AdvanceTo(ObjectiveStep.PreparationComplete);
            SceneFlow.RequestLoad(islandSceneName, "PreparationSequenceFallback", this);
        }
    }
}
