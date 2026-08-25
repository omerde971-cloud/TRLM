using System.Collections;
using UnityEngine;
using TRLM.Survival;

namespace TRLM.Weather
{
    /// <summary>
    /// Sprint 09 audio hooks. AUDIO_ASSET_MISSING — no audio clips exist in the project yet, so every
    /// AudioClip field below is left null on purpose; sound design only has to assign AudioSource +
    /// AudioClip fields in the Inspector later, no code changes required. Driven entirely by data that
    /// already exists (WeatherSystem.CurrentAudioIntensity/CurrentWindIntensity/IsStorm,
    /// ColdExposureSystem.CurrentStage, PsychologicalState.OnTierChanged) — nothing here polls a
    /// subsystem that isn't already computing this value for something else, and nothing is
    /// instantiated at runtime.
    /// </summary>
    public class WeatherAudioController : MonoBehaviour
    {
        [Header("Rain / Wind (looped, volume-driven)")]
        [SerializeField] private AudioSource rainAudioSource;
        [SerializeField] private AudioClip lightRainClip;
        [SerializeField] private AudioClip heavyRainClip;
        [SerializeField] private AudioSource windAudioSource;
        [SerializeField] private AudioClip windClip;
        [SerializeField] private float indoorRainMuffle = 0.35f;
        [SerializeField, Range(0f, 1f)] private float lightRainVolume = 0.28f;
        [SerializeField, Range(0f, 1f)] private float heavyRainVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float windVolume = 0.32f;

        [Header("Thunder (Storm only, one-shot)")]
        [SerializeField] private AudioSource thunderAudioSource;
        [SerializeField] private AudioClip closeThunderClip;
        [SerializeField] private AudioClip distantThunderClip;
        [SerializeField] private float minThunderIntervalSeconds = 10f;
        [SerializeField] private float maxThunderIntervalSeconds = 30f;

        [Header("Cold Breathing (Moderate/Critical hypothermia)")]
        [SerializeField] private AudioSource coldBreathAudioSource;
        [SerializeField] private AudioClip coldBreathClip;

        [Header("Sanity Tier Cues (one-shot on tier change)")]
        [SerializeField] private AudioSource sanityCueAudioSource;
        [SerializeField] private AudioClip uneasyCue;
        [SerializeField] private AudioClip stressedCue;
        [SerializeField] private AudioClip criticalCue;

        [Header("Optional local refs (auto-found on self if left empty)")]
        [SerializeField] private WetnessSystem wetness;
        [SerializeField] private ColdExposureSystem cold;
        [SerializeField] private PsychologicalState psych;

        private WeatherSystem weather;

        private void Awake()
        {
            if (wetness == null) wetness = GetComponent<WetnessSystem>();
            if (cold == null) cold = GetComponent<ColdExposureSystem>();
            if (psych == null) psych = GetComponent<PsychologicalState>();

            if (rainAudioSource != null) { rainAudioSource.clip = lightRainClip; rainAudioSource.loop = true; rainAudioSource.volume = 0f; }
            if (windAudioSource != null) { windAudioSource.clip = windClip; windAudioSource.loop = true; windAudioSource.volume = 0f; }
            if (coldBreathAudioSource != null) { coldBreathAudioSource.clip = coldBreathClip; coldBreathAudioSource.loop = true; coldBreathAudioSource.volume = 0f; }
        }

        private void OnEnable()
        {
            weather = WeatherSystem.Instance;
            if (psych != null) psych.OnTierChanged += HandleTierChanged;
            StartCoroutine(ThunderLoop());

            if (rainAudioSource != null && rainAudioSource.clip != null) rainAudioSource.Play();
            if (windAudioSource != null && windClip != null) windAudioSource.Play();
            if (coldBreathAudioSource != null && coldBreathClip != null) coldBreathAudioSource.Play();
        }

        private void OnDisable()
        {
            if (psych != null) psych.OnTierChanged -= HandleTierChanged;
            StopAllCoroutines();
        }

        private void Update()
        {
            if (weather == null) weather = WeatherSystem.Instance;
            if (weather == null) return;

            if (rainAudioSource != null)
            {
                bool sheltered = wetness != null && wetness.IsSheltered;
                float muffle = sheltered ? indoorRainMuffle : 1f;
                AudioClip targetClip = weather.CurrentRainIntensity > 0.55f ? heavyRainClip : lightRainClip;
                if (targetClip != null && rainAudioSource.clip != targetClip)
                {
                    rainAudioSource.clip = targetClip;
                    rainAudioSource.Play();
                }
                float rainScale = weather.CurrentRainIntensity > 0.55f ? heavyRainVolume : lightRainVolume;
                rainAudioSource.volume = weather.CurrentAudioIntensity * rainScale * muffle;
            }

            if (windAudioSource != null)
                windAudioSource.volume = weather.CurrentWindIntensity * windVolume;

            if (coldBreathAudioSource != null)
            {
                bool coldEnough = cold != null && cold.CurrentStage >= ColdExposureSystem.Stage.Moderate;
                coldBreathAudioSource.volume = coldEnough ? 1f : 0f;
            }
        }

        private IEnumerator ThunderLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minThunderIntervalSeconds, maxThunderIntervalSeconds));

                if (weather == null || !weather.IsStorm) continue;
                if (thunderAudioSource == null) continue;

                bool close = Random.value > 0.65f;
                var clip = close ? closeThunderClip : distantThunderClip;
                if (clip != null) thunderAudioSource.PlayOneShot(clip, close ? 0.72f : 0.38f);
            }
        }

        private void HandleTierChanged(PsychologicalState.Tier tier)
        {
            if (sanityCueAudioSource == null) return;

            AudioClip clip = tier switch
            {
                PsychologicalState.Tier.Uneasy => uneasyCue,
                PsychologicalState.Tier.Stressed => stressedCue,
                PsychologicalState.Tier.Critical => criticalCue,
                _ => null,
            };
            if (clip != null) sanityCueAudioSource.PlayOneShot(clip);
        }
    }
}
