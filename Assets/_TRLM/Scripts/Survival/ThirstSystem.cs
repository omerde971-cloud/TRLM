using System;
using UnityEngine;

namespace TRLM.Survival
{
    /// <summary>
    /// Same shape as HungerSystem but drains faster. Lives on PF_Player/Systems.
    /// DrinkSeaWater() is an intentionally bad trade — small thirst relief for a penalty —
    /// wired from SeaWaterSource, not something the player should do repeatedly.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class ThirstSystem : MonoBehaviour
    {
        private const string PenaltyId = "Thirst";

        [Header("Drain")]
        // Debug-friendly default: empties in ~8 real minutes, faster than hunger.
        [SerializeField] private float depletionPerSecond = 100f / (8f * 60f);

        [Header("Penalty Thresholds")]
        [SerializeField] private float lowThreshold = 35f;
        [SerializeField] private float staminaPenaltyMultiplier = 0.4f;
        [SerializeField] private float criticalThreshold = 8f;
        [SerializeField] private float criticalDamagePerTick = 3f;
        [SerializeField] private float criticalTickIntervalSeconds = 3f;

        [Header("Sea Water (bad trade — see DrinkSeaWater)")]
        [SerializeField] private float seaWaterThirstRelief = 8f;
        [SerializeField] private float seaWaterImmediateDamage = 4f;
        [SerializeField] private float seaWaterExtraDrainMultiplier = 1.75f;
        [SerializeField] private float seaWaterPenaltyDurationSeconds = 90f;

        private float thirst = 100f;
        private float criticalTimer;
        private float seaWaterPenaltyTimer;
        private HealthSystem health;
        private StaminaRegenModifier regenModifier;

        public event Action<float> OnThirstChanged;

        public float Thirst => thirst;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            regenModifier = GetComponent<StaminaRegenModifier>();
        }

        private void Update()
        {
            float drainMultiplier = 1f;
            if (seaWaterPenaltyTimer > 0f)
            {
                seaWaterPenaltyTimer -= Time.deltaTime;
                drainMultiplier = seaWaterExtraDrainMultiplier;
            }

            drainMultiplier *= Mathf.Max(0f, TRLM.Progression.DifficultySettings.ThirstRateMultiplier);
            SetThirst(thirst - depletionPerSecond * drainMultiplier * Time.deltaTime);

            bool low = thirst <= lowThreshold;
            if (regenModifier != null)
            {
                if (low) regenModifier.SetPenalty(PenaltyId, staminaPenaltyMultiplier);
                else regenModifier.ClearPenalty(PenaltyId);
            }

            if (thirst <= criticalThreshold)
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

        public void Drink(float amount)
        {
            if (amount <= 0f) return;
            SetThirst(thirst + amount);
        }

        /// <summary>
        /// Drinking sea water is a deliberately bad trade, not an exploit: it gives only a small
        /// amount of thirst relief, costs immediate health, and worsens thirst drain for a short
        /// window afterward. It exists so a desperate player has an option, not a strategy.
        /// </summary>
        public void DrinkSeaWater()
        {
            SetThirst(thirst + seaWaterThirstRelief);
            health.TakeDamage(seaWaterImmediateDamage);
            seaWaterPenaltyTimer = seaWaterPenaltyDurationSeconds;
        }

        /// <summary>Save/load restore only — direct setter, Drink() is additive-only.</summary>
        public void RestoreThirst(float value) => SetThirst(value);

        private void SetThirst(float value)
        {
            thirst = Mathf.Clamp(value, 0f, 100f);
            OnThirstChanged?.Invoke(thirst);
        }
    }
}
