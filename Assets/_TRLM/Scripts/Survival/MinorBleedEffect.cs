namespace TRLM.Survival
{
    /// <summary>
    /// Proof-of-architecture example only — not real content. Demonstrates that IStatusEffect
    /// can be implemented and driven by StatusEffectController end-to-end. A real bleed effect
    /// (with stacking, bandaging, etc.) is future work.
    /// </summary>
    public class MinorBleedEffect : IStatusEffect
    {
        private readonly float damagePerSecond;
        private float remainingSeconds;

        public MinorBleedEffect(float damagePerSecond, float durationSeconds)
        {
            this.damagePerSecond = damagePerSecond;
            remainingSeconds = durationSeconds;
        }

        public string Id => "MinorBleed";
        public bool IsExpired => remainingSeconds <= 0f;

        public void Tick(float deltaTime, HealthSystem target)
        {
            if (IsExpired) return;

            target.TakeDamage(damagePerSecond * deltaTime);
            remainingSeconds -= deltaTime;
        }
    }
}
