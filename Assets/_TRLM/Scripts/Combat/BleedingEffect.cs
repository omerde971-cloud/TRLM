using UnityEngine;
using TRLM.Survival;

namespace TRLM.Combat
{
    /// <summary>
    /// Real bleeding implementation superseding MinorBleedEffect's Sprint 05 proof-of-concept
    /// (kept alongside, not deleted — nothing else references it, but removing a working
    /// architecture example isn't this system's job). Periodic damage tick on an internal timer
    /// (every TickIntervalSeconds), never every frame — StatusEffectController.Update calls Tick
    /// every frame, but this class only actually applies damage once the accumulated time crosses
    /// the interval, per IStatusEffect's contract. Severity is capped (MaxSeverity) and stacks
    /// via AddSeverity instead of RegionalInjurySystem creating unlimited simultaneous instances,
    /// so repeated hits can't produce an unkillable-fast bleed.
    /// </summary>
    public class BleedingEffect : IStatusEffect
    {
        private const float TickIntervalSeconds = 2f;
        private const float MaxSeverity = 3f;
        private const float DamagePerSeverityPerTick = 1.5f;
        private const float NaturalTaperPerTick = 0.1f;

        private float severity;
        private float tickTimer;
        private bool cured;

        public BleedingEffect(float initialSeverity)
        {
            severity = Mathf.Clamp(initialSeverity, 0f, MaxSeverity);
        }

        public string Id => "Bleeding";
        public bool IsExpired => cured || severity <= 0f;
        public float Severity => severity;

        public void AddSeverity(float amount)
        {
            if (amount <= 0f) return;
            severity = Mathf.Min(MaxSeverity, severity + amount);
        }

        public void ReduceSeverity(float amount)
        {
            severity = Mathf.Max(0f, severity - amount);
        }

        /// <summary>Bandage use — fully stops the bleed (Section 23).</summary>
        public void Cure()
        {
            cured = true;
            severity = 0f;
        }

        public void Tick(float deltaTime, HealthSystem target)
        {
            if (IsExpired) return;

            tickTimer += deltaTime;
            if (tickTimer < TickIntervalSeconds) return;

            tickTimer -= TickIntervalSeconds;
            target.TakeDamage(severity * DamagePerSeverityPerTick);
            severity = Mathf.Max(0f, severity - NaturalTaperPerTick); // bleeding tapers slowly on its own
        }
    }
}
