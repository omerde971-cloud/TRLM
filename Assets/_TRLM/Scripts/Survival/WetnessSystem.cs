using System;
using UnityEngine;
using TRLM.World;
using TRLM.Weather;

namespace TRLM.Survival
{
    /// <summary>
    /// Sprint 05 wetness tracker, extended (not rebuilt) for Sprint 09: rain now actually adds
    /// wetness via the same periodic tick that already checked shelter, instead of a second timer.
    /// A lightweight upward raycast adds "under a roof" as a shelter case alongside the existing
    /// SafeHouse-marker and fire checks, so a cave/shed counts even without an authored marker.
    /// IsNearFire/IsSheltered are exposed publicly so ColdExposureSystem and PsychologicalState can
    /// reuse this one 0.5s scan instead of each re-scanning FirePoint/WorldMarker themselves.
    /// </summary>
    public class WetnessSystem : MonoBehaviour
    {
        [Header("Dry Rate")]
        [SerializeField] private float passiveDryPerSecond = 1.5f;
        [SerializeField] private float shelteredDryPerSecond = 6f;

        [Header("Shelter Detection")]
        [SerializeField] private float fireWarmthRadius = 6f;
        [SerializeField] private float checkIntervalSeconds = 0.5f;
        [SerializeField] private float roofCheckHeight = 30f;
        [SerializeField] private LayerMask roofMask = ~0;

        [Header("Rain Exposure")]
        [SerializeField] private float maxWetPerSecondAtFullRain = 6f;

        private float wetness;
        private float checkTimer;
        private bool nearFire;
        private bool underRoof;
        private bool nearShelter;

        public event Action<float> OnWetnessChanged;

        public float Wetness => wetness;
        public bool IsNearFire => nearFire;

        /// <summary>True if sheltered from rain for any reason (SafeHouse marker, roof overhead, or fire warmth radius).</summary>
        public bool IsSheltered => nearShelter;

        /// <summary>Hook for rain/wave/ocean-exposure callers.</summary>
        public void AddWetness(float amount)
        {
            if (amount <= 0f) return;
            SetWetness(wetness + amount);
        }

        private void Update()
        {
            checkTimer += Time.deltaTime;
            if (checkTimer >= checkIntervalSeconds)
            {
                checkTimer = 0f;
                nearFire = IsNearFireCheck();
                underRoof = IsUnderRoof();
                nearShelter = nearFire || underRoof || IsInsideSafeHouse();
            }

            float rain = WeatherSystem.Instance != null ? WeatherSystem.Instance.CurrentRainIntensity : 0f;
            if (rain > 0f && !nearShelter)
            {
                float severity = Mathf.Max(0f, TRLM.Progression.DifficultySettings.WeatherSeverityMultiplier);
                SetWetness(wetness + rain * maxWetPerSecondAtFullRain * severity * Time.deltaTime);
            }

            float dryRate = nearShelter ? shelteredDryPerSecond : passiveDryPerSecond;
            if (wetness > 0f)
                SetWetness(wetness - dryRate * Time.deltaTime);
        }

        private bool IsNearFireCheck()
        {
            foreach (var fire in FirePoint.ActiveLitFires)
            {
                if (fire == null || !fire.IsLit) continue;
                if (Vector3.Distance(fire.transform.position, transform.position) <= fireWarmthRadius)
                    return true;
            }
            return false;
        }

        private bool IsUnderRoof()
        {
            // Cheap periodic raycast (same 0.5s tick as the rest of this check), not per-frame.
            return Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.up, roofCheckHeight, roofMask, QueryTriggerInteraction.Ignore);
        }

        private bool IsInsideSafeHouse()
        {
            foreach (var marker in FindObjectsByType<WorldMarker>(FindObjectsSortMode.None))
            {
                if (marker.type != WorldMarker.MarkerType.SafeHouse) continue;
                if (Vector3.Distance(marker.transform.position, transform.position) <= marker.radius)
                    return true;
            }
            return false;
        }

        private void SetWetness(float value)
        {
            wetness = Mathf.Clamp(value, 0f, 100f);
            OnWetnessChanged?.Invoke(wetness);
        }
    }
}
