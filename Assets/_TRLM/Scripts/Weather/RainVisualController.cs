using System.Collections;
using UnityEngine;
using TRLM.Survival;

namespace TRLM.Weather
{
    /// <summary>
    /// Translates WeatherSystem's numbers into an actual look: rain particle emission, RenderSettings
    /// fog, and an occasional lightning flash during storms. Deliberately camera-relative — the rain
    /// particle system is meant to be parented under the player camera (a small emitter box that
    /// follows the player) rather than scattered across the island, so it costs the same whether the
    /// map is 800m or 8000m wide. Haiku authors the actual ParticleSystem module settings (shape,
    /// texture, color) in the Inspector; this script only ever touches emission rate and a few
    /// RenderSettings/Light fields at runtime.
    /// </summary>
    public class RainVisualController : MonoBehaviour
    {
        [Header("Rain")]
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private float maxEmissionRate = 800f;

        [Header("Fog")]
        [SerializeField] private bool driveFog = true;
        [SerializeField] private float clearFogDensity = 0.002f;
        [SerializeField] private float maxFogDensity = 0.02f;

        [Header("Lightning (Storm only)")]
        [SerializeField] private Light lightningLight;
        [SerializeField] private float minLightningIntervalSeconds = 8f;
        [SerializeField] private float maxLightningIntervalSeconds = 25f;
        [SerializeField] private float lightningFlashSeconds = 0.15f;
        [SerializeField] private float lightningIntensity = 6f;

        [Header("Shelter (matches WeatherAudioController's indoor muffle)")]
        // Must be wired in the Inspector — this component lives on the camera-relative RainVFX
        // rig, not alongside WetnessSystem (see WeatherAudioController, which sits on PF_Player's
        // Systems object and can GetComponent<WetnessSystem> on itself instead).
        [SerializeField] private WetnessSystem wetness;

        private WeatherSystem weather;
        private Coroutine lightningRoutine;

        private void Start()
        {
            weather = WeatherSystem.Instance;
            if (weather == null) { enabled = false; return; }

            if (driveFog) RenderSettings.fog = true;
            if (lightningLight != null) lightningLight.enabled = false;

            lightningRoutine = StartCoroutine(LightningLoop());
        }

        private void Update()
        {
            if (weather == null) return;

            if (rainParticles != null)
            {
                bool sheltered = wetness != null && wetness.IsSheltered;
                float rainIntensity = sheltered ? 0f : weather.CurrentRainIntensity;

                var emission = rainParticles.emission;
                emission.rateOverTime = rainIntensity * maxEmissionRate;
                if (rainIntensity > 0f && !rainParticles.isPlaying) rainParticles.Play();
                else if (rainIntensity <= 0f && rainParticles.isPlaying) rainParticles.Stop();
            }

            if (driveFog)
                RenderSettings.fogDensity = Mathf.Lerp(clearFogDensity, maxFogDensity, weather.CurrentFogModifier);
        }

        private IEnumerator LightningLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(minLightningIntervalSeconds, maxLightningIntervalSeconds));

                if (weather == null || !weather.IsStorm || lightningLight == null) continue;

                lightningLight.intensity = lightningIntensity;
                lightningLight.enabled = true;
                yield return new WaitForSeconds(lightningFlashSeconds);
                lightningLight.enabled = false;
            }
        }
    }
}
