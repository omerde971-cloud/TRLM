using UnityEngine;
using TRLM.Survival;

namespace TRLM.Combat
{
    /// <summary>
    /// Poison foundation (Section 26). Same periodic-tick pattern as BleedingEffect (internal
    /// timer, not every frame). Antidote handling is intentionally simple: Medicine-category
    /// items reduce severity via PlayerInventory -> RegionalInjurySystem.ReducePoisonSeverity,
    /// same as they reduce injury severity — no separate antidote item type this sprint.
    /// </summary>
    public class PoisonEffect : IStatusEffect
    {
        private const float TickIntervalSeconds = 3f;
        private const float NaturalMetabolizePerTick = 0.5f;

        private float severity;
        private float tickTimer;

        public PoisonEffect(float initialSeverity)
        {
            severity = Mathf.Max(0f, initialSeverity);
        }

        public string Id => "Poison";
        public bool IsExpired => severity <= 0f;
        public float Severity => severity;

        public void AddSeverity(float amount)
        {
            if (amount > 0f) severity += amount;
        }

        public void ReduceSeverity(float amount)
        {
            severity = Mathf.Max(0f, severity - amount);
        }

        public void Tick(float deltaTime, HealthSystem target)
        {
            if (IsExpired) return;

            tickTimer += deltaTime;
            if (tickTimer < TickIntervalSeconds) return;

            tickTimer -= TickIntervalSeconds;
            target.TakeDamage(severity);
            severity = Mathf.Max(0f, severity - NaturalMetabolizePerTick);
        }
    }
}
