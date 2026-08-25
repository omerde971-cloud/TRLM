using UnityEngine;

namespace TRLM.Progression
{
    public enum DifficultyLevel { Story, Normal, Hard, Custom }

    /// <summary>
    /// One full set of difficulty multipliers. Plain serializable data — Story/Normal/Hard are
    /// static presets below; Custom is whatever the (future) menu builds and SaveGameData stores.
    /// Ranges are the GDD's suggested 0.5x-2.0x; nothing here enforces that at the type level since
    /// a director may want to go outside it for one field without a code change.
    /// </summary>
    [System.Serializable]
    public class DifficultyProfile
    {
        public float PlayerDamageMultiplier = 1f;
        public float EnemyDamageMultiplier = 1f;
        public float LootAmmoMultiplier = 1f;
        public float InjurySeverityMultiplier = 1f;
        public float HungerRateMultiplier = 1f;
        public float ThirstRateMultiplier = 1f;
        public float SanityPressureMultiplier = 1f;
        public float WeatherSeverityMultiplier = 1f;
        public float WildlifeAggressionMultiplier = 1f;
        // No current system has a clean "checkpoint tolerance" concept to scale (see
        // CheckpointManager remarks) — the field exists so a save/menu can carry the value now,
        // consumption is DEFERRED.
        public float CheckpointToleranceMultiplier = 1f;

        public DifficultyProfile Clone() => (DifficultyProfile)MemberwiseClone();

        public static DifficultyProfile Story() => new DifficultyProfile
        {
            PlayerDamageMultiplier = 0.7f,
            EnemyDamageMultiplier = 0.8f,
            LootAmmoMultiplier = 1.4f,
            InjurySeverityMultiplier = 0.7f,
            HungerRateMultiplier = 0.7f,
            ThirstRateMultiplier = 0.7f,
            SanityPressureMultiplier = 0.6f,
            WeatherSeverityMultiplier = 0.75f,
            WildlifeAggressionMultiplier = 0.75f,
            CheckpointToleranceMultiplier = 1.5f,
        };

        public static DifficultyProfile Normal() => new DifficultyProfile();

        public static DifficultyProfile Hard() => new DifficultyProfile
        {
            PlayerDamageMultiplier = 1.3f,
            EnemyDamageMultiplier = 1.15f,
            LootAmmoMultiplier = 0.7f,
            InjurySeverityMultiplier = 1.3f,
            HungerRateMultiplier = 1.3f,
            ThirstRateMultiplier = 1.3f,
            SanityPressureMultiplier = 1.4f,
            WeatherSeverityMultiplier = 1.3f,
            WildlifeAggressionMultiplier = 1.25f,
            CheckpointToleranceMultiplier = 0.75f,
        };
    }

    /// <summary>
    /// Global difficulty hooks. Kept as static fields (Sprint 07 origin) so every existing call
    /// site (WeaponController, MeleeController, RegionalInjurySystem, ...) keeps compiling and
    /// reading live values with zero changes; Apply()/ApplyPreset() are the only new surface,
    /// copying a DifficultyProfile's values onto these fields plus recording which
    /// level/profile is active so SaveGameData can persist it. Not a MonoBehaviour — no scene
    /// reference needed, works before any scene loads (main menu difficulty pick).
    /// </summary>
    public static class DifficultySettings
    {
        public static float PlayerDamageMultiplier = 1f;
        public static float EnemyDamageMultiplier = 1f;
        public static float LootAmmoMultiplier = 1f;
        public static float InjurySeverityMultiplier = 1f;
        public static float HungerRateMultiplier = 1f;
        public static float ThirstRateMultiplier = 1f;
        public static float SanityPressureMultiplier = 1f;
        public static float WeatherSeverityMultiplier = 1f;
        public static float WildlifeAggressionMultiplier = 1f;
        public static float CheckpointToleranceMultiplier = 1f;

        public static DifficultyLevel CurrentLevel { get; private set; } = DifficultyLevel.Normal;

        /// <summary>The exact values behind CurrentLevel — for Custom this IS the authored profile;
        /// for Story/Normal/Hard it's a fresh preset instance (so mutating it does nothing until
        /// Apply() is called again). Save this into SaveGameData, not CurrentLevel alone, so a
        /// Custom run's exact numbers survive a reload.</summary>
        public static DifficultyProfile CurrentProfile { get; private set; } = DifficultyProfile.Normal();

        public static void ApplyPreset(DifficultyLevel level, DifficultyProfile customProfile = null)
        {
            DifficultyProfile profile = level switch
            {
                DifficultyLevel.Story => DifficultyProfile.Story(),
                DifficultyLevel.Hard => DifficultyProfile.Hard(),
                DifficultyLevel.Custom => customProfile ?? DifficultyProfile.Normal(),
                _ => DifficultyProfile.Normal(),
            };
            CurrentLevel = level;
            Apply(profile);
        }

        public static void Apply(DifficultyProfile profile)
        {
            if (profile == null) return;
            CurrentProfile = profile;

            PlayerDamageMultiplier = profile.PlayerDamageMultiplier;
            EnemyDamageMultiplier = profile.EnemyDamageMultiplier;
            LootAmmoMultiplier = profile.LootAmmoMultiplier;
            InjurySeverityMultiplier = profile.InjurySeverityMultiplier;
            HungerRateMultiplier = profile.HungerRateMultiplier;
            ThirstRateMultiplier = profile.ThirstRateMultiplier;
            SanityPressureMultiplier = profile.SanityPressureMultiplier;
            WeatherSeverityMultiplier = profile.WeatherSeverityMultiplier;
            WildlifeAggressionMultiplier = profile.WildlifeAggressionMultiplier;
            CheckpointToleranceMultiplier = profile.CheckpointToleranceMultiplier;
        }
    }
}
