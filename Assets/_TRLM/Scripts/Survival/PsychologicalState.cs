using System;
using System.Collections.Generic;
using UnityEngine;
using TRLM.World;
using TRLM.Combat;
using TRLM.Companions;
using TRLM.Equipment;
using TRLM.Player;

namespace TRLM.Survival
{
    /// <summary>
    /// One coherent psychological-condition model (Sprint 09 explicitly asks for this instead of
    /// separate Sanity/Morale systems) — a single normalized Stability value with four readable
    /// tiers. Sources of drain/recovery are mostly event-driven (HealthSystem.OnDamaged,
    /// RegionalInjurySystem.OnInjuryChanged, Hunger/ThirstSystem.On*Changed, BurialZone's static
    /// OnBurialComplete) rather than each polling its own subsystem; the few genuinely continuous
    /// inputs (night, cold, isolation, companion proximity) are checked on one shared 0.5s tick,
    /// the same interval WetnessSystem already uses, not a fresh Update-per-source.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class PsychologicalState : MonoBehaviour
    {
        public enum Tier { Stable, Uneasy, Stressed, Critical }

        [Header("References (optional — auto-found on self/children if left empty)")]
        [SerializeField] private HealthSystem health;
        [SerializeField] private HungerSystem hunger;
        [SerializeField] private ThirstSystem thirst;
        [SerializeField] private ColdExposureSystem cold;
        [SerializeField] private WetnessSystem wetness;
        [SerializeField] private RegionalInjurySystem injury;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private StaminaRegenModifier staminaRegen;
        [SerializeField] private MonoBehaviour timeSourceBehaviour; // IWorldTimeSource

        [Header("Tier Thresholds (Stability 0-100)")]
        [SerializeField] private float stressedThreshold = 40f;
        [SerializeField] private float criticalThreshold = 15f;
        [SerializeField] private float uneasyThreshold = 70f;

        [Header("Continuous Drain (per second, capped/summed)")]
        [SerializeField] private float nightDrain = 0.3f;
        [SerializeField] private float lowHealthDrain = 0.5f;
        [SerializeField] private float seriousInjuryDrain = 0.4f;
        [SerializeField] private float lowHungerThirstDrain = 0.3f;
        [SerializeField] private float hypothermiaDrain = 0.5f;
        [SerializeField] private float isolationDrain = 0.2f;
        [SerializeField] private float maxDrainPerSecond = 1.5f; // cap so night+rain+injury+cold can't spiral

        [Header("Continuous Recovery (per second)")]
        [SerializeField] private float shelterRecovery = 0.4f;
        [SerializeField] private float fireRecovery = 0.5f;
        [SerializeField] private float daylightRecovery = 0.15f;
        [SerializeField] private float wellFedRecovery = 0.1f;
        [SerializeField] private float companionRecoveryPerCompanion = 0.15f;
        [SerializeField] private int companionRecoveryCap = 3; // diminishing: at most this many companions count

        [Header("Instant Hits / Recovery")]
        [SerializeField] private float companionDeathHit = 25f;
        [SerializeField] private float burialRecovery = 10f;

        [Header("Isolation")]
        [SerializeField] private float companionProximityRadius = 20f;

        [Header("Perception Events (foundation only — see PerceptionEventSystem)")]
        [SerializeField] private bool enablePerceptionEvents = true;
        [SerializeField] private float minPerceptionIntervalSeconds = 45f;
        [SerializeField] private float maxPerceptionIntervalSeconds = 120f;
        [SerializeField] private float perceptionEventRadius = 15f;

        [Header("Effects")]
        [SerializeField] private float stressedSwayMultiplier = 1.3f;
        [SerializeField] private float criticalSwayMultiplier = 1.7f;
        [SerializeField] private float stressedStaminaMultiplier = 0.85f;
        [SerializeField] private float criticalStaminaMultiplier = 0.6f;

        private const string EffectId = "Sanity";

        private float stability = 100f;
        private float checkTimer;
        private bool lowHealthActive;
        private bool seriousInjuryActive;
        private bool lowHungerThirstActive;
        private IWorldTimeSource timeSource;
        private readonly HashSet<CompanionId> processedDeaths = new HashSet<CompanionId>();
        private readonly HashSet<CompanionId> processedBurials = new HashSet<CompanionId>();
        private float perceptionEventTimer;

        public event Action<float> OnStabilityChanged;
        public event Action<Tier> OnTierChanged;

        public float Stability => stability;
        public Tier CurrentTier { get; private set; } = Tier.Stable;

        private void Awake()
        {
            if (health == null) health = GetComponent<HealthSystem>();
            if (hunger == null) hunger = GetComponent<HungerSystem>();
            if (thirst == null) thirst = GetComponent<ThirstSystem>();
            if (cold == null) cold = GetComponent<ColdExposureSystem>();
            if (wetness == null) wetness = GetComponent<WetnessSystem>();
            if (injury == null) injury = GetComponentInChildren<RegionalInjurySystem>();
            if (weapon == null) weapon = GetComponentInChildren<WeaponController>();
            if (staminaRegen == null) staminaRegen = GetComponent<StaminaRegenModifier>();
            timeSource = timeSourceBehaviour as IWorldTimeSource;

            SetTier(Tier.Stable, silent: true);
            perceptionEventTimer = UnityEngine.Random.Range(minPerceptionIntervalSeconds, maxPerceptionIntervalSeconds);
        }

        private void OnEnable()
        {
            if (health != null) health.OnDamaged += HandleDamaged;
            if (hunger != null) hunger.OnHungerChanged += HandleHungerChanged;
            if (thirst != null) thirst.OnThirstChanged += HandleThirstChanged;
            if (injury != null) injury.OnInjuryChanged += HandleInjuryChanged;
            BurialZone.OnBurialComplete += HandleBurialComplete;

            foreach (var companion in FindObjectsByType<CompanionAI>(FindObjectsSortMode.None))
            {
                var companionHealth = companion.GetComponent<HealthSystem>();
                var identity = companion.GetComponent<CompanionIdentity>();
                if (companionHealth != null && identity != null)
                {
                    var id = identity.Id;
                    companionHealth.OnDeath += () => OnCompanionDied(id);
                }
            }
        }

        private void OnDisable()
        {
            if (health != null) health.OnDamaged -= HandleDamaged;
            if (hunger != null) hunger.OnHungerChanged -= HandleHungerChanged;
            if (thirst != null) thirst.OnThirstChanged -= HandleThirstChanged;
            if (injury != null) injury.OnInjuryChanged -= HandleInjuryChanged;
            BurialZone.OnBurialComplete -= HandleBurialComplete;
        }

        private void Update()
        {
            checkTimer += Time.deltaTime;
            if (checkTimer < 0.5f) return;
            float dt = checkTimer;
            checkTimer = 0f;

            float drain = 0f;
            float recover = 0f;

            bool isNight = timeSource != null && timeSource.IsNight;
            if (isNight) drain += nightDrain;
            else recover += daylightRecovery;

            if (lowHealthActive) drain += lowHealthDrain;
            if (seriousInjuryActive) drain += seriousInjuryDrain;
            if (lowHungerThirstActive) drain += lowHungerThirstDrain;
            else recover += wellFedRecovery;

            if (cold != null && cold.CurrentStage == ColdExposureSystem.Stage.Critical) drain += hypothermiaDrain;

            bool sheltered = wetness != null && wetness.IsSheltered;
            if (sheltered) recover += shelterRecovery;
            if (wetness != null && wetness.IsNearFire) recover += fireRecovery;

            int livingCompanionsNearby = CountLivingCompanionsNearby();
            if (livingCompanionsNearby > 0)
                recover += companionRecoveryPerCompanion * Mathf.Min(livingCompanionsNearby, companionRecoveryCap);
            else if (isNight)
                drain += isolationDrain; // isolation only meaningfully punishing at night

            drain *= Mathf.Max(0f, TRLM.Progression.DifficultySettings.SanityPressureMultiplier);
            drain = Mathf.Min(drain, maxDrainPerSecond);
            float netPerSecond = recover - drain;
            SetStability(stability + netPerSecond * dt);

            TickPerceptionEvents(dt, livingCompanionsNearby == 0);
        }

        /// <summary>Two safe example triggers only — see PerceptionEventSystem. Only fires while
        /// Stressed/Critical and alone, never damages the player, clearly separate from real threats.</summary>
        private void TickPerceptionEvents(float dt, bool isolated)
        {
            if (!enablePerceptionEvents) return;
            if (CurrentTier != Tier.Stressed && CurrentTier != Tier.Critical) return;
            if (!isolated) return;

            perceptionEventTimer -= dt;
            if (perceptionEventTimer > 0f) return;
            perceptionEventTimer = UnityEngine.Random.Range(minPerceptionIntervalSeconds, maxPerceptionIntervalSeconds);

            string kind = UnityEngine.Random.value < 0.5f ? PerceptionEventSystem.DistantBranchCrack : PerceptionEventSystem.DistantWhisper;
            Vector3 pos = transform.position + UnityEngine.Random.insideUnitSphere * perceptionEventRadius;
            PerceptionEventSystem.Trigger(pos, kind);
        }

        private int CountLivingCompanionsNearby()
        {
            int count = 0;
            foreach (var companion in FindObjectsByType<CompanionAI>(FindObjectsSortMode.None))
            {
                if (companion.IsDead) continue;
                if (Vector3.Distance(companion.transform.position, transform.position) <= companionProximityRadius)
                    count++;
            }
            return count;
        }

        private void HandleDamaged(float amount, GameObject source)
        {
            lowHealthActive = health.Normalized <= 0.3f;
        }

        private void HandleHungerChanged(float value)
        {
            lowHungerThirstActive = value <= 25f || (thirst != null && thirst.Thirst <= 25f);
        }

        private void HandleThirstChanged(float value)
        {
            lowHungerThirstActive = value <= 25f || (hunger != null && hunger.Hunger <= 25f);
        }

        private void HandleInjuryChanged(BodyRegion region, float severity)
        {
            seriousInjuryActive = injury != null && (injury.GetSeverity(BodyRegion.Head) > 0.5f
                || injury.AllSeverities().Count > 0 && SeverityMax() > 0.6f);
        }

        private float SeverityMax()
        {
            float max = 0f;
            foreach (var kv in injury.AllSeverities())
                if (kv.Value > max) max = kv.Value;
            return max;
        }

        private void HandleBurialComplete(CompanionId id)
        {
            if (!processedBurials.Add(id)) return;
            SetStability(stability + burialRecovery);
        }

        /// <summary>Companion death morale hit. Guarded against duplicate application if the same
        /// death event somehow fires twice (e.g. HealthSystem.OnDeath re-subscribed).</summary>
        public void OnCompanionDied(CompanionId id)
        {
            if (!processedDeaths.Add(id)) return;
            SetStability(stability - companionDeathHit);
        }

        /// <summary>Architecture hook for a future guilt/consequence system — not implemented this
        /// sprint (no existing corpse-abandonment timer to hang it off of).</summary>
        public void NotifyCorpseAbandoned(CompanionId id) { }

        /// <summary>Save/load restore only. Seeds the dedup guards for a death/burial that already
        /// happened last session WITHOUT applying their stability change again — call this before
        /// HealthSystem.RestoreState(dead:true) re-fires OnDeath (which calls OnCompanionDied), and
        /// before restoring a burial marker, so the real event handlers see "already processed" and
        /// skip the hit/recovery instead of double-applying it.</summary>
        public void MarkCompanionDeathAlreadyProcessed(CompanionId id) => processedDeaths.Add(id);
        public void MarkCompanionBurialAlreadyProcessed(CompanionId id) => processedBurials.Add(id);

        private void SetStability(float value)
        {
            stability = Mathf.Clamp(value, 0f, 100f);
            OnStabilityChanged?.Invoke(stability);

            Tier tier = stability <= criticalThreshold ? Tier.Critical
                : stability <= stressedThreshold ? Tier.Stressed
                : stability <= uneasyThreshold ? Tier.Uneasy
                : Tier.Stable;

            if (tier != CurrentTier) SetTier(tier, silent: false);
            ApplyTierEffects(tier);
        }

        private void SetTier(Tier tier, bool silent)
        {
            CurrentTier = tier;
            if (!silent) OnTierChanged?.Invoke(tier);
        }

        private void ApplyTierEffects(Tier tier)
        {
            switch (tier)
            {
                case Tier.Stressed:
                    weapon?.SetSwayModifier(EffectId, stressedSwayMultiplier);
                    staminaRegen?.SetPenalty(EffectId, stressedStaminaMultiplier);
                    break;
                case Tier.Critical:
                    weapon?.SetSwayModifier(EffectId, criticalSwayMultiplier);
                    staminaRegen?.SetPenalty(EffectId, criticalStaminaMultiplier);
                    break;
                default:
                    weapon?.ClearSwayModifier(EffectId);
                    staminaRegen?.ClearPenalty(EffectId);
                    break;
            }
        }

        /// <summary>Generic positive nudge — sleep, food, etc. Not instant full recovery on its own.</summary>
        public void Recover(float amount)
        {
            if (amount > 0f) SetStability(stability + amount);
        }

        // ---------------------------------------------------------------- Debug (test scene only)
        public void DebugSetStability(float value) => SetStability(value);
    }
}
