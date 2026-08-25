using System;
using System.Collections.Generic;
using UnityEngine;
using TRLM.Survival;
using TRLM.Player;
using TRLM.Equipment;
using TRLM.Progression;

namespace TRLM.Combat
{
    /// <summary>
    /// Tracks per-region injury severity on the player (Sections 20-21, 25) and applies/clears the
    /// appropriate existing modifier APIs — never a parallel health framework. Subscribes to the
    /// player's own HealthSystem.OnDamaged: since only gunfire currently provides a precise hit
    /// location (handled inline by WeaponController/MeleeController), every other damage source
    /// (wolf bites, rockfalls, received melee) has no region info, so this system rolls a
    /// weighted-random region on every OnDamaged event — the honest fallback the brief calls for.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class RegionalInjurySystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HealthSystem health;
        [SerializeField] private StatusEffectController statusEffects;
        [SerializeField] private FirstPersonController movement;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private StaminaRegenModifier staminaRegen;

        [Header("Tuning")]
        [SerializeField] private float severityPerDamage = 0.6f;
        [SerializeField, Range(0f, 1f)] private float bleedChanceTorso = 0.35f;
        [SerializeField, Range(0f, 1f)] private float bleedChanceOther = 0.15f;
        [SerializeField] private float bleedSeverityPerHit = 1f;
        [SerializeField] private float armTraumaThreshold = 6f;
        [SerializeField] private float legTraumaThreshold = 6f;
        [SerializeField] private float legSprintBlockThreshold = 8f;
        [SerializeField] private float headBleedSeverityThreshold = 4f;
        [SerializeField] private float traumaDurationSeconds = 90f;

        private readonly Dictionary<BodyRegion, float> severities = new Dictionary<BodyRegion, float>();
        private BleedingEffect activeBleed;
        private PoisonEffect activePoison;
        private TraumaStatusFlag legTrauma;
        private TraumaStatusFlag armTrauma;

        /// <summary>Head-hit hook (Section 20) — HUD/camera-shake systems can subscribe without
        /// this class knowing anything about screen effects. No screen-shake built here.</summary>
        public event Action OnHeadInjury;
        public event Action<BodyRegion, float> OnInjuryChanged;

        private void Awake()
        {
            if (health == null) health = GetComponent<HealthSystem>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectController>();
            if (movement == null) movement = GetComponentInParent<FirstPersonController>();
            if (weaponController == null) weaponController = GetComponentInParent<WeaponController>();
            if (staminaRegen == null) staminaRegen = GetComponent<StaminaRegenModifier>();

            foreach (BodyRegion r in Enum.GetValues(typeof(BodyRegion)))
                severities[r] = 0f;
        }

        private void OnEnable()
        {
            if (health != null) health.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDamaged -= HandleDamaged;
        }

        private void HandleDamaged(float amount, GameObject source)
        {
            // Damage with no source is internal/environmental (BleedingEffect, PoisonEffect,
            // and Sprint 05's Hunger/Thirst/ColdExposure critical-damage ticks all call
            // TakeDamage with no source argument) — every real external attacker (WolfAI,
            // MeleeController, WeaponController, RockfallPlayerDamage) always passes its own
            // gameObject. Without this guard, a bleed/poison tick's own damage would re-enter
            // here, roll a new region, and could spawn MORE bleeding — a runaway self-damage
            // feedback loop discovered during Sprint 07 QA (killed a test player in ~90s from
            // nothing but its own status-effect ticks). Only react to real attacks/hazards.
            if (source == null) return;

            BodyRegion region = RollRegion();
            ApplyInjury(region, amount * severityPerDamage);

            float bleedChance = region == BodyRegion.Torso ? bleedChanceTorso : bleedChanceOther;
            if (UnityEngine.Random.value < bleedChance)
                ApplyBleeding(bleedSeverityPerHit);
        }

        /// <summary>Weighted-random region roll: Head 10%, Torso 30%, each arm 15%, each leg 15%
        /// (roughly realistic — torso/legs are bigger, more likely targets than the head).</summary>
        private static BodyRegion RollRegion()
        {
            float roll = UnityEngine.Random.value;
            if (roll < 0.10f) return BodyRegion.Head;
            if (roll < 0.40f) return BodyRegion.Torso;
            if (roll < 0.55f) return BodyRegion.LeftArm;
            if (roll < 0.70f) return BodyRegion.RightArm;
            if (roll < 0.85f) return BodyRegion.LeftLeg;
            return BodyRegion.RightLeg;
        }

        public float GetSeverity(BodyRegion region) => severities.TryGetValue(region, out var v) ? v : 0f;

        public bool HasAnyInjury()
        {
            foreach (var v in severities.Values)
                if (v > 0f) return true;
            return false;
        }

        public IReadOnlyDictionary<BodyRegion, float> AllSeverities() => severities;

        public void ApplyInjury(BodyRegion region, float severity)
        {
            severity *= Mathf.Max(0f, DifficultySettings.InjurySeverityMultiplier);
            if (severity <= 0f) return;

            severities[region] = (severities.TryGetValue(region, out var cur) ? cur : 0f) + severity;
            OnInjuryChanged?.Invoke(region, severities[region]);
            ApplyRegionEffects(region);
        }

        private void ApplyRegionEffects(BodyRegion region)
        {
            switch (region)
            {
                case BodyRegion.LeftArm:
                case BodyRegion.RightArm:
                    ApplyArmPenalty(region);
                    break;
                case BodyRegion.LeftLeg:
                case BodyRegion.RightLeg:
                    ApplyLegPenalty(region);
                    break;
                case BodyRegion.Torso:
                    ApplyTorsoPenalty();
                    break;
                case BodyRegion.Head:
                    ApplyHeadPenalty();
                    break;
            }
        }

        private void ApplyArmPenalty(BodyRegion region)
        {
            float sev = severities[region];
            string key = "Injury_" + region;

            if (sev <= 0f)
            {
                weaponController?.ClearSwayModifier(key);
                weaponController?.ClearReloadSpeedModifier(key);
                return;
            }

            float swayMultiplier = 1f + Mathf.Clamp(sev * 0.15f, 0f, 2f); // worst-wins: bigger = more sway
            weaponController?.SetSwayModifier(key, swayMultiplier);

            float reloadMultiplier = 1f + Mathf.Clamp(sev * 0.1f, 0f, 1f); // worst-wins: bigger = slower reload
            weaponController?.SetReloadSpeedModifier(key, reloadMultiplier);

            if (sev >= armTraumaThreshold)
                ApplyArmTrauma();
        }

        private void ApplyLegPenalty(BodyRegion region)
        {
            float sev = severities[region];
            string key = "Injury_" + region;

            if (sev <= 0f)
            {
                movement?.ClearSpeedModifier(key);
                movement?.SetSprintBlocked(key, false);
                return;
            }

            float speedMultiplier = Mathf.Clamp(1f - sev * 0.08f, 0.3f, 1f);
            movement?.SetSpeedModifier(key, speedMultiplier);
            movement?.SetSprintBlocked(key, sev >= legSprintBlockThreshold);

            if (sev >= legTraumaThreshold)
                ApplyLegTrauma();
        }

        private void ApplyTorsoPenalty()
        {
            float sev = severities[BodyRegion.Torso];
            const string key = "Injury_Torso";

            if (sev <= 0f)
            {
                staminaRegen?.ClearPenalty(key);
                return;
            }

            float staminaMultiplier = Mathf.Clamp(1f - sev * 0.05f, 0.3f, 1f);
            staminaRegen?.SetPenalty(key, staminaMultiplier);
        }

        private void ApplyHeadPenalty()
        {
            OnHeadInjury?.Invoke();

            // Elevated bleeding/damage response for severe head injury — tuning call documented
            // here rather than a separate concussion system this sprint.
            if (severities[BodyRegion.Head] >= headBleedSeverityThreshold)
                ApplyBleeding(bleedSeverityPerHit * 1.5f);
        }

        public void ApplyBleeding(float severity)
        {
            if (statusEffects == null) return;

            if (activeBleed != null && !activeBleed.IsExpired)
            {
                activeBleed.AddSeverity(severity);
            }
            else
            {
                activeBleed = new BleedingEffect(severity);
                statusEffects.ApplyEffect(activeBleed);
            }
        }

        /// <summary>Bandage use (Section 23) — fully stops the currently active bleed.</summary>
        public void TreatBleeding() => activeBleed?.Cure();

        public bool IsBleeding => activeBleed != null && !activeBleed.IsExpired;

        public void ApplyPoison(float severity)
        {
            if (statusEffects == null) return;

            if (activePoison != null && !activePoison.IsExpired)
            {
                activePoison.AddSeverity(severity);
            }
            else
            {
                activePoison = new PoisonEffect(severity);
                statusEffects.ApplyEffect(activePoison);
            }
        }

        public void ReducePoisonSeverity(float amount) => activePoison?.ReduceSeverity(amount);

        public float PoisonSeverity => activePoison != null && !activePoison.IsExpired ? activePoison.Severity : 0f;

        /// <summary>Save/load restore only. Sets a region's severity directly instead of going
        /// through ApplyInjury, which would apply DifficultySettings.InjurySeverityMultiplier a
        /// second time onto an already-scaled saved value.</summary>
        public void RestoreInjury(BodyRegion region, float severity)
        {
            if (severity <= 0f) return;
            severities[region] = severity;
            OnInjuryChanged?.Invoke(region, severity);
            ApplyRegionEffects(region);
        }

        /// <summary>Section 24 — Medicine reduces injury severity modestly alongside its heal, via
        /// PlayerInventory.UseSelectedItem's existing Medicine branch. Deliberately NOT a full,
        /// instant cure of trauma — TraumaArm/TraumaLeg still have to run out their own timer (or
        /// be halved by AccelerateRecovery), per the brief's explicit instruction.</summary>
        public void ReduceAllInjurySeverity(float amount)
        {
            var regions = new List<BodyRegion>(severities.Keys);
            foreach (var region in regions)
            {
                if (severities[region] <= 0f) continue;
                severities[region] = Mathf.Max(0f, severities[region] - amount);
                OnInjuryChanged?.Invoke(region, severities[region]);
                ApplyRegionEffects(region);
            }
        }

        private void ApplyArmTrauma()
        {
            if (armTrauma != null && !armTrauma.IsExpired) return;

            armTrauma = new TraumaStatusFlag("TraumaArm", traumaDurationSeconds);
            statusEffects?.ApplyEffect(armTrauma);
            weaponController?.SetSwayModifier("TraumaArm", 2.5f);
        }

        private void ApplyLegTrauma()
        {
            if (legTrauma != null && !legTrauma.IsExpired) return;

            legTrauma = new TraumaStatusFlag("TraumaLeg", traumaDurationSeconds);
            statusEffects?.ApplyEffect(legTrauma);
            movement?.SetSpeedModifier("TraumaLeg", 0.4f);
            movement?.SetSprintBlocked("TraumaLeg", true);
        }

        /// <summary>Section 25 — "safe-house sleep may accelerate recovery". Called by
        /// SleepInteraction.ApplyRest's small additive hook.</summary>
        public void AccelerateRecovery()
        {
            legTrauma?.AccelerateRecovery();
            armTrauma?.AccelerateRecovery();
            ReduceAllInjurySeverity(3f);
        }

        // ---- Test hooks (also used by CombatTestHarness in 92_Test_Combat) ----
        public void DebugForceInjury(BodyRegion region, float severity) => ApplyInjury(region, severity);
        public void DebugForceBleed(float severity) => ApplyBleeding(severity);
        public void DebugForcePoison(float severity) => ApplyPoison(severity);
    }
}
