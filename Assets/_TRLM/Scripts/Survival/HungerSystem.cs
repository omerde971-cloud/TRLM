using System;
using UnityEngine;

namespace TRLM.Survival
{
    /// <summary>
    /// Slow player hunger drain. Lives on PF_Player/Systems alongside HealthSystem/StaminaSystem.
    /// Below lowThreshold it penalizes stamina regen via StaminaRegenModifier; near empty it
    /// periodically chips HealthSystem via a timer rather than every frame. Food items call Eat().
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class HungerSystem : MonoBehaviour
    {
        private const string PenaltyId = "Hunger";

        [Header("Drain")]
        // Debug-friendly default: empties in ~12 real minutes. Tune up for a real survival pace.
        [SerializeField] private float depletionPerSecond = 100f / (12f * 60f);

        [Header("Penalty Thresholds")]
        [SerializeField] private float lowThreshold = 35f;
        [SerializeField] private float staminaPenaltyMultiplier = 0.5f;
        [SerializeField] private float criticalThreshold = 8f;
        [SerializeField] private float criticalDamagePerTick = 2f;
        [SerializeField] private float criticalTickIntervalSeconds = 4f;

        private float hunger = 100f;
        private float criticalTimer;
        private HealthSystem health;
        private StaminaRegenModifier regenModifier;

        public event Action<float> OnHungerChanged;

        public float Hunger => hunger;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            regenModifier = GetComponent<StaminaRegenModifier>();
        }

        private void Update()
        {
            SetHunger(hunger - depletionPerSecond * Mathf.Max(0f, TRLM.Progression.DifficultySettings.HungerRateMultiplier) * Time.deltaTime);

            bool low = hunger <= lowThreshold;
            if (regenModifier != null)
            {
                if (low) regenModifier.SetPenalty(PenaltyId, staminaPenaltyMultiplier);
                else regenModifier.ClearPenalty(PenaltyId);
            }

            if (hunger <= criticalThreshold)
            {
                criticalTimer += Time.deltaTime;
                if (criticalTimer >= criticalTickIntervalSeconds)
                {
                    criticalTimer = 0f;
                    health.TakeDamage(criticalDamagePerTick);
                }
            }
            else
            {
                criticalTimer = 0f;
            }
        }

        public void Eat(float amount)
        {
            if (amount <= 0f) return;
            SetHunger(hunger + amount);
        }

        /// <summary>Save/load restore only — direct setter, Eat() is additive-only.</summary>
        public void RestoreHunger(float value) => SetHunger(value);

        private void SetHunger(float value)
        {
            hunger = Mathf.Clamp(value, 0f, 100f);
            OnHungerChanged?.Invoke(hunger);
        }
    }
}
