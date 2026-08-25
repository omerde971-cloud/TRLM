using System;
using UnityEngine;

namespace TRLM.Weather
{
    /// <summary>
    /// Owns current/target weather and smoothly interpolates the exposed gameplay values between
    /// them — it does not touch rain VFX, fog, or audio directly (see RainVisualController); other
    /// systems (WetnessSystem, ColdExposureSystem, WolfPerception, PsychologicalState) just read
    /// the public Current* properties or subscribe to OnWeatherChanged, so this stays a small
    /// orchestrator instead of a god class that also owns survival/AI logic.
    ///
    /// Controlled random: each weather profile carries its own hold-duration range and relative
    /// pick weight, so storms stay rare and clear/cloudy periods dominate without a hardcoded
    /// state table. SetWeather() drives the normal cycle; ForceWeather()/ReleaseWeatherOverride()
    /// let a future story/authored event pin the weather and hand control back cleanly.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        [SerializeField] private WeatherProfile[] profiles;
        [SerializeField] private WeatherType startingWeather = WeatherType.Clear;
        [SerializeField] private float defaultTransitionSeconds = 25f;

        private WeatherProfile current;
        private WeatherProfile target;
        private float transitionDuration;
        private float transitionElapsed;
        private float holdTimer;
        private bool overridden;

        public event Action<WeatherType> OnWeatherChanged;
        public event Action<float> OnRainIntensityChanged;

        public WeatherType CurrentWeather => current != null ? current.type : WeatherType.Clear;
        public float CurrentRainIntensity { get; private set; }
        public float CurrentWindIntensity { get; private set; }
        public float CurrentFogModifier { get; private set; }
        public float CurrentVisibilityModifier { get; private set; }
        public float CurrentTemperatureModifier { get; private set; }
        public float CurrentAudioIntensity { get; private set; }
        public bool IsStorm => target != null && target.isStorm;

        private void Awake()
        {
            Instance = this;
            var start = FindProfile(startingWeather) ?? (profiles.Length > 0 ? profiles[0] : null);
            current = start;
            target = start;
            ApplyImmediate(start);
            if (start != null) holdTimer = UnityEngine.Random.Range(start.minHoldSeconds, start.maxHoldSeconds);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (target != current)
            {
                transitionElapsed += Time.deltaTime;
                float t = transitionDuration > 0f ? Mathf.Clamp01(transitionElapsed / transitionDuration) : 1f;
                Lerp(current, target, t);
                if (t >= 1f)
                {
                    current = target;
                    OnWeatherChanged?.Invoke(current.type);
                }
                return;
            }

            if (overridden) return;

            holdTimer -= Time.deltaTime;
            if (holdTimer <= 0f)
                SetWeather(PickNextWeather(), defaultTransitionSeconds);
        }

        /// <summary>Normal cycle transition — does not clear an active ForceWeather override.</summary>
        public void SetWeather(WeatherType type, float transitionSeconds = -1f)
        {
            var profile = FindProfile(type);
            if (profile == null || profile == target) return;

            target = profile;
            transitionDuration = transitionSeconds >= 0f ? transitionSeconds : defaultTransitionSeconds;
            transitionElapsed = 0f;
            holdTimer = UnityEngine.Random.Range(profile.minHoldSeconds, profile.maxHoldSeconds);
        }

        /// <summary>Authored/story override: pins the weather and stops the controlled-random cycle until released.</summary>
        public void ForceWeather(WeatherType type, float transitionSeconds = -1f)
        {
            overridden = true;
            SetWeather(type, transitionSeconds);
        }

        /// <summary>Save/load restore only. Snaps directly to the saved weather with no transition
        /// (a fresh scene load has no rain particles/fog mid-fade to preserve anyway — see Sprint
        /// 10 spec Part L, "does not need frame-perfect particle restoration") and does not touch
        /// the override flag, so a save taken mid-ForceWeather resumes as override; a save taken
        /// during the normal cycle resumes cycling normally.</summary>
        public void RestoreWeather(WeatherType type)
        {
            var profile = FindProfile(type);
            if (profile == null) return;

            current = profile;
            target = profile;
            transitionElapsed = 0f;
            ApplyImmediate(profile);
            holdTimer = UnityEngine.Random.Range(profile.minHoldSeconds, profile.maxHoldSeconds);
        }

        public void ReleaseWeatherOverride()
        {
            overridden = false;
            holdTimer = current != null ? UnityEngine.Random.Range(current.minHoldSeconds, current.maxHoldSeconds) : 60f;
        }

        private WeatherType PickNextWeather()
        {
            float totalWeight = 0f;
            foreach (var p in profiles) totalWeight += Mathf.Max(0f, p.transitionWeight);
            if (totalWeight <= 0f) return current != null ? current.type : WeatherType.Clear;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            foreach (var p in profiles)
            {
                roll -= Mathf.Max(0f, p.transitionWeight);
                if (roll <= 0f) return p.type;
            }
            return profiles[profiles.Length - 1].type;
        }

        private WeatherProfile FindProfile(WeatherType type)
        {
            foreach (var p in profiles)
                if (p != null && p.type == type) return p;
            return null;
        }

        private void ApplyImmediate(WeatherProfile p)
        {
            Lerp(p, p, 1f);
        }

        private void Lerp(WeatherProfile a, WeatherProfile b, float t)
        {
            if (a == null || b == null) return;

            CurrentRainIntensity = Mathf.Lerp(a.rainIntensity, b.rainIntensity, t);
            CurrentWindIntensity = Mathf.Lerp(a.windIntensity, b.windIntensity, t);
            CurrentFogModifier = Mathf.Lerp(a.fogModifier, b.fogModifier, t);
            CurrentVisibilityModifier = Mathf.Lerp(a.visibilityModifier, b.visibilityModifier, t);
            CurrentTemperatureModifier = Mathf.Lerp(a.temperatureModifier, b.temperatureModifier, t);
            CurrentAudioIntensity = Mathf.Lerp(a.audioIntensity, b.audioIntensity, t);
            OnRainIntensityChanged?.Invoke(CurrentRainIntensity);

            // Heavy rain muffles hearing a little; conservative (never below 0.75x) per the brief's
            // "no unrealistic rain-makes-wolves-superpowered rules." Wolves read the static field,
            // not this system, so no per-wolf weather polling.
            TRLM.AI.Wolf.WolfPerception.WeatherHearingMultiplier = Mathf.Lerp(1f, 0.75f, CurrentRainIntensity);
        }
    }
}
