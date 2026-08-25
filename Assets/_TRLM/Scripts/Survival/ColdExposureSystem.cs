using System;
using UnityEngine;
using TRLM.World;
using TRLM.Weather;
using TRLM.Player;

namespace TRLM.Survival
{
    /// <summary>
    /// Body temperature foundation, extended for Sprint 09: drains at night (via IWorldTimeSource),
    /// faster the wetter the player is, and faster still under a cold weather profile
    /// (WeatherSystem.CurrentTemperatureModifier) — none of that requires the island to be
    /// permanently freezing, since the modifier is 0 for Clear weather. Three readable stages
    /// (Mild/Moderate/Critical) instead of two: Mild costs stamina regen, Moderate adds a movement
    /// penalty on top, Critical drains health gradually (never a sudden unavoidable death — the
    /// tick interval gives time to react). Warms automatically near a lit fire via WetnessSystem's
    /// already-computed IsNearFire (no second fire scan). Composes with Hunger/Thirst through the
    /// shared StaminaRegenModifier rather than touching StaminaSystem directly.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(WetnessSystem))]
    public class ColdExposureSystem : MonoBehaviour
    {
        private const string PenaltyId = "Cold";

        [Header("Time Source (optional)")]
        [SerializeField] private MonoBehaviour timeSourceBehaviour; // must implement IWorldTimeSource

        [Header("Movement (optional)")]
        [SerializeField] private FirstPersonController movement;

        [Header("Drain")]
        [SerializeField] private float nightDrainPerSecond = 0.4f;
        [SerializeField] private float wetnessDrainMultiplierAt100 = 2.5f; // extra drain factor when fully wet
        [SerializeField] private float weatherDrainScale = 0.3f; // WeatherSystem.CurrentTemperatureModifier -> extra drain/sec

        [Header("Warmth")]
        [SerializeField] private float fireWarmPerSecond = 8f;

        [Header("Thresholds — Mild")]
        [SerializeField] private float mildThreshold = 65f;
        [SerializeField] private float mildStaminaPenaltyMultiplier = 0.75f;

        [Header("Thresholds — Moderate")]
        [SerializeField] private float moderateThreshold = 40f;
        [SerializeField] private float moderateStaminaPenaltyMultiplier = 0.5f;
        [SerializeField] private float moderateSpeedMultiplier = 0.85f;

        [Header("Thresholds — Critical")]
        [SerializeField] private float criticalThreshold = 15f;
        [SerializeField] private float criticalDamagePerTick = 2f;
        [SerializeField] private float criticalTickIntervalSeconds = 5f;

        private float bodyTemperature = 100f;
        private float criticalTimer;
        private IWorldTimeSource timeSource;
        private HealthSystem health;
        private WetnessSystem wetness;
        private StaminaRegenModifier regenModifier;

        public enum Stage { None, Mild, Moderate, Critical }

        public event Action<float> OnBodyTemperatureChanged;

        public float BodyTemperature => bodyTemperature;
        public Stage CurrentStage { get; private set; }

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            wetness = GetComponent<WetnessSystem>();
            regenModifier = GetComponent<StaminaRegenModifier>();
            timeSource = timeSourceBehaviour as IWorldTimeSource;
            if (movement == null) movement = GetComponent<FirstPersonController>();
        }

        private void Update()
        {
            if (wetness.IsNearFire)
            {
                SetBodyTemperature(bodyTemperature + fireWarmPerSecond * Time.deltaTime);
            }
            else
            {
                bool isNight = timeSource != null && timeSource.IsNight;
                float wetnessFactor = 1f + (wetness.Wetness / 100f) * (wetnessDrainMultiplierAt100 - 1f);
                float weatherTemp = WeatherSystem.Instance != null ? WeatherSystem.Instance.CurrentTemperatureModifier : 0f;

                float severity = Mathf.Max(0f, TRLM.Progression.DifficultySettings.WeatherSeverityMultiplier);
                float drain = ((isNight ? nightDrainPerSecond : 0f) * wetnessFactor + weatherTemp * weatherDrainScale) * severity;
                if (drain > 0f)
                    SetBodyTemperature(bodyTemperature - drain * Time.deltaTime);
            }

            UpdateStageEffects();
        }

        private void UpdateStageEffects()
        {
            if (bodyTemperature <= criticalThreshold) CurrentStage = Stage.Critical;
            else if (bodyTemperature <= moderateThreshold) CurrentStage = Stage.Moderate;
            else if (bodyTemperature <= mildThreshold) CurrentStage = Stage.Mild;
            else CurrentStage = Stage.None;

            if (regenModifier != null)
            {
                switch (CurrentStage)
                {
                    case Stage.Mild: regenModifier.SetPenalty(PenaltyId, mildStaminaPenaltyMultiplier); break;
                    case Stage.Moderate:
                    case Stage.Critical: regenModifier.SetPenalty(PenaltyId, moderateStaminaPenaltyMultiplier); break;
                    default: regenModifier.ClearPenalty(PenaltyId); break;
                }
            }

            if (movement != null)
            {
                if (CurrentStage == Stage.Moderate || CurrentStage == Stage.Critical)
                    movement.SetSpeedModifier(PenaltyId, moderateSpeedMultiplier);
                else
                    movement.ClearSpeedModifier(PenaltyId);
            }

            if (CurrentStage == Stage.Critical)
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

        /// <summary>Lets a fire/shelter system warm the player back up on demand (e.g. sleep rest).</summary>
        public void Warm(float amount)
        {
            if (amount <= 0f) return;
            SetBodyTemperature(bodyTemperature + amount);
        }

        // ---------------------------------------------------------------- Debug (test scene only)
        public void DebugSetBodyTemperature(float value) => SetBodyTemperature(value);

        private void SetBodyTemperature(float value)
        {
            bodyTemperature = Mathf.Clamp(value, 0f, 100f);
            OnBodyTemperatureChanged?.Invoke(bodyTemperature);
        }
    }
}
