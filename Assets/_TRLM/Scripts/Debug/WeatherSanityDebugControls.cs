using UnityEngine;
using TRLM.Weather;
using TRLM.Survival;
using TRLM.Companions;

namespace TRLM.DebugTools
{
    /// <summary>
    /// Developer-only OnGUI panel for 94_Test_WeatherSanity (same pattern as BurialZone/SleepInteraction's
    /// OnGUI usage elsewhere in the project). Never included in a shipping UI flow.
    /// </summary>
    public class WeatherSanityDebugControls : MonoBehaviour
    {
        [SerializeField] private WeatherSystem weather;
        [SerializeField] private WetnessSystem wetness;
        [SerializeField] private ColdExposureSystem cold;
        [SerializeField] private PsychologicalState psych;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 220, 480), GUI.skin.box);
            GUILayout.Label("Weather / Sanity Debug");

            GUILayout.Label("Weather");
            if (GUILayout.Button("Force Clear")) weather?.ForceWeather(WeatherType.Clear, 5f);
            if (GUILayout.Button("Force Cloudy")) weather?.ForceWeather(WeatherType.Cloudy, 5f);
            if (GUILayout.Button("Force Light Rain")) weather?.ForceWeather(WeatherType.LightRain, 5f);
            if (GUILayout.Button("Force Heavy Rain")) weather?.ForceWeather(WeatherType.HeavyRain, 5f);
            if (GUILayout.Button("Force Storm")) weather?.ForceWeather(WeatherType.Storm, 5f);
            if (GUILayout.Button("Release Override")) weather?.ReleaseWeatherOverride();
            GUILayout.Label(weather != null ? $"Current: {weather.CurrentWeather} rain={weather.CurrentRainIntensity:0.00}" : "no WeatherSystem");

            GUILayout.Space(8);
            GUILayout.Label("Wetness");
            if (GUILayout.Button("+20 Wetness")) wetness?.AddWetness(20f);
            GUILayout.Label(wetness != null ? $"Wetness: {wetness.Wetness:0} sheltered={wetness.IsSheltered}" : "no WetnessSystem");

            GUILayout.Space(8);
            GUILayout.Label("Cold");
            if (GUILayout.Button("-20 Body Temp")) cold?.DebugSetBodyTemperature(Mathf.Max(0f, cold.BodyTemperature - 20f));
            if (GUILayout.Button("+20 Body Temp")) cold?.DebugSetBodyTemperature(Mathf.Min(100f, cold.BodyTemperature + 20f));
            GUILayout.Label(cold != null ? $"BodyTemp: {cold.BodyTemperature:0} ({cold.CurrentStage})" : "no ColdExposureSystem");

            GUILayout.Space(8);
            GUILayout.Label("Sanity");
            if (GUILayout.Button("-20 Stability")) psych?.DebugSetStability(Mathf.Max(0f, (psych?.Stability ?? 0f) - 20f));
            if (GUILayout.Button("+20 Stability")) psych?.DebugSetStability(Mathf.Min(100f, (psych?.Stability ?? 0f) + 20f));
            if (GUILayout.Button("Simulate Jonah Death")) psych?.OnCompanionDied(CompanionId.Jonah);
            GUILayout.Label(psych != null ? $"Stability: {psych.Stability:0} ({psych.CurrentTier})" : "no PsychologicalState");

            GUILayout.EndArea();
        }
    }
}
