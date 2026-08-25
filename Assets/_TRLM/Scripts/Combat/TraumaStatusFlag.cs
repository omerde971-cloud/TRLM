using TRLM.Survival;

namespace TRLM.Combat
{
    /// <summary>
    /// Non-damaging queryable flag status effect for a severe regional injury (Section 25
    /// fracture/trauma foundation). Tick does nothing but count down a real, finite duration —
    /// not effectively-permanent. StatusEffectController.HasEffect("TraumaLeg"/"TraumaArm")
    /// makes it queryable for future UI. AccelerateRecovery lets a safe-house sleep
    /// (SleepInteraction.ApplyRest -> RegionalInjurySystem.AccelerateRecovery) speed up healing.
    /// </summary>
    public class TraumaStatusFlag : IStatusEffect
    {
        private readonly string id;
        private float remainingSeconds;

        public TraumaStatusFlag(string id, float durationSeconds)
        {
            this.id = id;
            remainingSeconds = durationSeconds;
        }

        public string Id => id;
        public bool IsExpired => remainingSeconds <= 0f;

        public void Tick(float deltaTime, HealthSystem target)
        {
            if (IsExpired) return;
            remainingSeconds -= deltaTime;
        }

        public void AccelerateRecovery()
        {
            remainingSeconds *= 0.5f;
        }
    }
}
