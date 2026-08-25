using UnityEngine;
using TRLM.AI.Wolf;

namespace TRLM.Progression
{
    /// <summary>
    /// Drives the WolfThreat objective step from real wolf AI state rather than a proximity
    /// sphere (explicit Sprint 06 requirement). WolfAI exposes a public, safely readable
    /// <see cref="WolfAI.CurrentState"/> property, so this polls every active wolf on a timer
    /// (not every frame, consistent with the project's existing "don't poll every frame"
    /// convention — see HungerSystem/ThirstSystem's tick-based critical damage) and advances the
    /// objective the first time any wolf is in Alert/Stalk/Chase.
    ///
    /// Place this once in the Island scene (e.g. near the Deep Forest wolf territories / the
    /// authored night wolf corridor). If <see cref="zoneCenter"/> is left unassigned, every active
    /// wolf in the scene is checked instead of scoping to one area — simplest correct behavior for
    /// the vertical slice, since the only wolves that exist are the authored deep-forest packs.
    /// </summary>
    public class WolfThreatObjectiveWatcher : MonoBehaviour
    {
        [SerializeField] private Transform zoneCenter; // optional; null = check all wolves
        [SerializeField] private float zoneRadius = 80f;
        [SerializeField] private float pollIntervalSeconds = 0.5f;

        private float timer;

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < pollIntervalSeconds) return;
            timer = 0f;

            if (ObjectiveSystem.Instance == null) return;
            if (ObjectiveSystem.Instance.Current >= ObjectiveStep.WolfThreat) return;

            foreach (var wolf in FindObjectsByType<WolfAI>(FindObjectsSortMode.None))
            {
                if (wolf.IsDead) continue;
                if (zoneCenter != null && Vector3.Distance(wolf.transform.position, zoneCenter.position) > zoneRadius)
                    continue;

                if (wolf.CurrentState == WolfAI.State.Alert ||
                    wolf.CurrentState == WolfAI.State.Stalk ||
                    wolf.CurrentState == WolfAI.State.Chase)
                {
                    // Gated: an early daytime wolf scare shouldn't leapfrog the loot/night beats.
                    ObjectiveSystem.Instance.AdvanceToInOrder(ObjectiveStep.WolfThreat, ObjectiveStep.NightBegins);
                    return;
                }
            }
        }
    }
}
