using System.Collections.Generic;
using UnityEngine;

namespace TRLM.Equipment
{
    /// <summary>
    /// Plain C# sway model (not a MonoBehaviour) owned and ticked by WeaponController. With no
    /// real weapon-in-hand geometry to visually wobble (see WeaponDefinition remarks on the
    /// project's asset reality), sway is expressed mechanically instead: a maximum random
    /// angular deviation (degrees) applied to each hitscan shot direction on top of the
    /// weapon's own pellet spread. Higher sway = worse accuracy.
    ///
    /// SetSwayModifier/ClearSwayModifier follow the same additive, source-keyed, worst-active-
    /// value-wins pattern as StaminaRegenModifier and FirstPersonController's speed modifiers —
    /// except here "worst" means the LARGEST multiplier (more sway), the mirror image of those
    /// two (where worst means smallest). Sub-Agent A2 can call SetSwayModifier("ArmInjury", x)
    /// from the injury system without this class knowing anything about injuries.
    /// </summary>
    public class WeaponSway
    {
        private readonly Dictionary<string, float> modifiers = new Dictionary<string, float>();

        public float Multiplier { get; private set; } = 1f;

        public void SetSwayModifier(string sourceId, float multiplier)
        {
            modifiers[sourceId] = Mathf.Max(0f, multiplier);
            Recompute();
        }

        public void ClearSwayModifier(string sourceId)
        {
            if (modifiers.Remove(sourceId))
                Recompute();
        }

        private void Recompute()
        {
            float worst = 1f;
            foreach (var value in modifiers.Values)
                worst = Mathf.Max(worst, value);
            Multiplier = worst;
        }

        /// <summary>Computes current sway in degrees from base weapon sway and live player state.</summary>
        public float ComputeSwayDegrees(float baseSwayDegrees, float staminaNormalized, bool crouching, bool moving, bool aiming)
        {
            float sway = baseSwayDegrees;
            sway *= Mathf.Lerp(2f, 1f, Mathf.Clamp01(staminaNormalized)); // low stamina = more sway
            if (crouching) sway *= 0.6f;
            if (moving) sway *= 1.4f;
            if (aiming) sway *= 0.35f; // aiming down sights steadies the shot
            return Mathf.Max(0f, sway * Multiplier);
        }
    }
}
