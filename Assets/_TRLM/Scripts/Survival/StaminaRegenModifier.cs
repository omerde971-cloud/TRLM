using System.Collections.Generic;
using UnityEngine;

namespace TRLM.Survival
{
    /// <summary>
    /// Central place where Hunger/Thirst/Cold/Sanity/Injury register stamina-regen penalties, so
    /// StaminaSystem.RegenMultiplier only ever gets one write path instead of five systems fighting
    /// over it. Uses the WORST active penalty rather than multiplying them together — multiplying
    /// would let e.g. hunger(0.5) x thirst(0.4) x cold(0.5) x sanity(0.6) x injury(0.3) compound to
    /// ~0.018x, an effectively-zero-regen lockout from five merely-moderate penalties. Worst-of
    /// already keeps the floor at whatever the single harshest active penalty is (0.3 in that same
    /// example); minCombinedMultiplier below is a defensive floor on top of that in case a future
    /// system ever registers something harsher, not a fix for an active bug in this combination.
    /// </summary>
    [RequireComponent(typeof(StaminaSystem))]
    public class StaminaRegenModifier : MonoBehaviour
    {
        [Tooltip("Combined regen multiplier never drops below this, no matter how many penalties " +
                 "are stacked — Quality Pass #1's chosen floor.")]
        [SerializeField] private float minCombinedMultiplier = 0.2f;

        private readonly Dictionary<string, float> penalties = new Dictionary<string, float>();
        private StaminaSystem stamina;

        private void Awake()
        {
            stamina = GetComponent<StaminaSystem>();
        }

        public void SetPenalty(string sourceId, float multiplier)
        {
            penalties[sourceId] = Mathf.Clamp01(multiplier);
            Recompute();
        }

        public void ClearPenalty(string sourceId)
        {
            if (penalties.Remove(sourceId))
                Recompute();
        }

        private void Recompute()
        {
            float worst = 1f;
            foreach (var value in penalties.Values)
                worst = Mathf.Min(worst, value);

            stamina.RegenMultiplier = Mathf.Max(worst, minCombinedMultiplier);
        }
    }
}
