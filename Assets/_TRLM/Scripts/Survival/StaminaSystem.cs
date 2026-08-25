using System;
using UnityEngine;

namespace TRLM.Survival
{
    /// <summary>
    /// Reusable stamina component. FirstPersonController asks IsExhausted before allowing
    /// sprint, and calls ConsumeSprint/ConsumeJump; it never touches CurrentStamina directly.
    /// </summary>
    public class StaminaSystem : MonoBehaviour
    {
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float sprintDrainRate = 20f; // per second while sprinting
        [SerializeField] private float jumpCost = 10f;
        [SerializeField] private float regenRate = 15f;       // per second once regen starts
        [SerializeField] private float regenDelay = 1.5f;     // seconds of no drain before regen starts

        private float currentStamina = -1f; // -1 = "not yet initialized"
        private float timeSinceLastDrain;

        public event Action<float, float> OnStaminaChanged; // (current, max)

        public float MaxStamina => maxStamina;
        public float CurrentStamina => EnsureInitialized();
        public float Normalized => maxStamina <= 0f ? 0f : Mathf.Clamp01(CurrentStamina / maxStamina);
        public bool IsExhausted => CurrentStamina <= 0f;

        /// <summary>Multiplies regen rate. Hunger/Thirst/Cold drive this via StaminaRegenModifier, never directly.</summary>
        public float RegenMultiplier { get; set; } = 1f;

        // Lazily initialized instead of relying on Awake — see HealthSystem for why.
        private float EnsureInitialized()
        {
            if (currentStamina < 0f)
                currentStamina = maxStamina;
            return currentStamina;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances the regen timer/logic by deltaTime. Called from Update, and directly
        /// from tests so regen can be verified without Play Mode.
        /// </summary>
        public void Tick(float deltaTime)
        {
            timeSinceLastDrain += deltaTime;

            if (timeSinceLastDrain >= regenDelay && EnsureInitialized() < maxStamina)
            {
                Regenerate(regenRate * deltaTime * RegenMultiplier);
            }
        }

        /// <summary>Call every frame the player is actively sprinting. Returns false once exhausted.</summary>
        public bool ConsumeSprint(float deltaTime)
        {
            if (IsExhausted) return false;

            Drain(sprintDrainRate * deltaTime);
            return !IsExhausted;
        }

        /// <summary>Call once when the player jumps. Returns false if there wasn't enough stamina.</summary>
        public bool ConsumeJump()
        {
            if (EnsureInitialized() < jumpCost) return false;

            Drain(jumpCost);
            return true;
        }

        /// <summary>Sprint 07 (A2) — flat stamina drain for actions with no dedicated Consume*
        /// method (melee attacks). Unlike ConsumeJump this never blocks the action for having too
        /// little stamina; it just drains what's available down to zero (callers that need a hard
        /// gate check IsExhausted first, same pattern MeleeController uses).</summary>
        public void ConsumeFlat(float amount)
        {
            if (amount <= 0f) return;
            Drain(amount);
        }

        private void Drain(float amount)
        {
            if (amount <= 0f) return;

            currentStamina = Mathf.Max(0f, EnsureInitialized() - amount);
            timeSinceLastDrain = 0f;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        private void Regenerate(float amount)
        {
            if (amount <= 0f) return;

            currentStamina = Mathf.Min(maxStamina, EnsureInitialized() + amount);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }
}
