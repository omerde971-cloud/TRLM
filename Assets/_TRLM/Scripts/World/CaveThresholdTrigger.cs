using UnityEngine;
using TRLM.Progression;
using TRLM.Story;

namespace TRLM.World
{
    /// <summary>
    /// Order-safe objective trigger for the deepest cave beat. Unlike a plain
    /// <see cref="RegionEntryTrigger"/> (which advances unconditionally), this only advances to
    /// <c>targetStep</c> once the player has already reached <c>minimumStep</c> — so walking to the
    /// back of the cave cannot skip an un-collected Prophecy page. The advance itself still rides on
    /// the existing <see cref="ObjectiveSystem"/>; persistence is via ProgressionData.currentObjective.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CaveThresholdTrigger : MonoBehaviour
    {
        [SerializeField] private ObjectiveStep targetStep = ObjectiveStep.CaveThresholdComplete;
        [Tooltip("Player must already be at or past this step for the advance to fire (ordering guard).")]
        [SerializeField] private ObjectiveStep minimumStep = ObjectiveStep.RecoverFirstProphecyPage;
        [SerializeField] private string playerTag = "Player";
        [Tooltip("Optional StoryFlag set when this fires.")]
        [SerializeField] private string setFlag = "cave_threshold_complete";

        private bool fired;

        private void OnTriggerEnter(Collider other)
        {
            if (fired) return;
            if (!other.CompareTag(playerTag)) return;
            var system = ObjectiveSystem.Instance;
            if (system == null) return;
            if ((int)system.Current < (int)minimumStep) return; // ordering guard — page not yet recovered

            fired = true;
            system.AdvanceTo(targetStep);
            if (!string.IsNullOrEmpty(setFlag) && StoryFlags.Instance != null)
                StoryFlags.Instance.Set(setFlag);
        }
    }
}
