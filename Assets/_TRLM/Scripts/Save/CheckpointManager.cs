using System.Collections.Generic;
using UnityEngine;
using TRLM.Progression;

namespace TRLM.Save
{
    /// <summary>
    /// Authored automatic checkpoints — writes the same autosave slot SaveOrchestrator/manual saves
    /// use (Part D: "checkpoint data should use the same main persistence architecture"), not a
    /// parallel system. Debounced per checkpoint id (Part D2) so a trigger volume the player walks
    /// back and forth over, or two systems both calling Trigger for the same event in one frame,
    /// writes at most once per id per debounce window — and a repeat call with an id already
    /// recorded this session is a permanent no-op, not just a timed one, matching "no duplicate
    /// writes for the same objective event."
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        [SerializeField] private float minSecondsBetweenAnyCheckpoint = 5f;

        private readonly HashSet<string> firedCheckpointIds = new HashSet<string>();
        private float lastCheckpointTime = -999f;
        private bool subscribed;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        // Existing ObjectiveStep values ARE the game's authored major-milestone list (region
        // transitions, night falling, the wolf threat encounter, reaching the safe house, lighting
        // the fire, sleeping) — subscribing here satisfies "integrate with current objective
        // architecture" without inventing a second, parallel checkpoint-trigger system.
        //
        // Subscription is retried in Start: OnEnable can run before ObjectiveSystem.Awake has set
        // Instance (script execution order is undefined between them), and the old enable-only
        // subscribe silently produced sessions where no checkpoint ever fired.
        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void TrySubscribe()
        {
            if (subscribed || ObjectiveSystem.Instance == null) return;
            ObjectiveSystem.Instance.OnObjectiveChanged += HandleObjectiveChanged;
            subscribed = true;
        }

        private void OnDisable()
        {
            if (subscribed && ObjectiveSystem.Instance != null)
                ObjectiveSystem.Instance.OnObjectiveChanged -= HandleObjectiveChanged;
            subscribed = false;
        }

        private void HandleObjectiveChanged(ObjectiveStep step) => TriggerCheckpoint(step.ToString());

        /// <summary>Call from an authored trigger/event (region transition, story beat, encounter
        /// completion). checkpointId should be stable and unique per authored checkpoint (e.g.
        /// "ReachSafeHouse", not a GameObject name that could change) — reusing an id is exactly
        /// how the "once per checkpoint" guarantee is enforced.</summary>
        public bool TriggerCheckpoint(string checkpointId)
        {
            if (string.IsNullOrEmpty(checkpointId)) return false;
            if (firedCheckpointIds.Contains(checkpointId)) return false; // this exact checkpoint already fired this session
            if (Time.unscaledTime - lastCheckpointTime < minSecondsBetweenAnyCheckpoint) return false; // global spam guard

            firedCheckpointIds.Add(checkpointId);
            lastCheckpointTime = Time.unscaledTime;

            var outcome = SaveOrchestrator.Instance?.SaveAutosave();
            if (outcome.HasValue && outcome.Value.Success)
                Debug.Log($"[CheckpointManager] Checkpoint '{checkpointId}' saved.");
            else
                Debug.LogWarning($"[CheckpointManager] Checkpoint '{checkpointId}' save failed: {outcome?.Message}");

            return true;
        }

        /// <summary>Restoring a save re-seeds which checkpoints have already fired this run (via
        /// its ObjectiveStep/day-count, not a raw id list — a full "fired ids" list isn't persisted
        /// since checkpoint ids are meant to be one-way progress markers, not per-session-only
        /// dedup state). Called by SaveOrchestrator after a successful Restore.</summary>
        public void ResetSessionDedup() => firedCheckpointIds.Clear();
    }
}
