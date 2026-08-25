using UnityEngine;

namespace TRLM.Weather
{
    /// <summary>
    /// Pure data for one WeatherType — kept separate from WeatherSystem's transition/runtime
    /// logic so designers can retune numbers without touching code. minHoldSeconds/maxHoldSeconds
    /// control how long the controlled-random cycle stays on this weather before rolling again.
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherProfile", menuName = "TRLM/Weather Profile")]
    public class WeatherProfile : ScriptableObject
    {
        public WeatherType type;

        [Header("Visual/Gameplay Intensity (0-1 unless noted)")]
        [Range(0f, 1f)] public float rainIntensity;
        [Range(0f, 1f)] public float windIntensity;
        [Range(0f, 1f)] public float fogModifier;
        [Range(0f, 1f)] public float visibilityModifier; // 0 = clearest, 1 = most obscured
        public float temperatureModifier; // added to cold drain; 0 = no extra chill, higher = colder
        [Range(0f, 1f)] public float audioIntensity;
        public bool isStorm; // lightning/thunder hook

        [Header("Controlled Random — how long this weather persists before re-rolling")]
        public float minHoldSeconds = 120f;
        public float maxHoldSeconds = 300f;

        [Header("Controlled Random — relative chance to be picked next (0 = never rolled)")]
        public float transitionWeight = 1f;
    }
}
