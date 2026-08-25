using UnityEngine;
using TRLM.Weather;

namespace TRLM.Audio
{
    /// <summary>
    /// Biome-aware 2D ambience beds layered on top of ForestAmbienceController's day/night
    /// forest loops: a wind bed whose volume follows the WeatherSystem's live wind intensity,
    /// and a shore/waves bed that fades in as the player nears sea level (cheap altitude proxy
    /// for "on the coast" — the island rises steeply enough that altitude tracks distance to
    /// water well). All volume moves are eased; no per-frame allocations.
    /// </summary>
    public class WorldAmbienceController : MonoBehaviour
    {
        [Header("Wind")]
        [SerializeField] private AudioSource windSource;
        [SerializeField] private AudioClip windLoop;
        [SerializeField, Range(0f, 1f)] private float windBaseVolume = 0.08f;
        [SerializeField, Range(0f, 1f)] private float windStormVolume = 0.38f;

        [Header("Shore")]
        [SerializeField] private AudioSource shoreSource;
        [SerializeField] private AudioClip wavesLoop;
        [SerializeField, Range(0f, 1f)] private float shoreMaxVolume = 0.3f;
        [Tooltip("Player altitude (world Y) at/below which the shore bed is at full volume.")]
        [SerializeField] private float shoreFullAltitude = 4f;
        [Tooltip("Player altitude above which the shore bed is silent.")]
        [SerializeField] private float shoreSilentAltitude = 18f;

        [SerializeField] private float fadeSpeed = 0.4f;
        [SerializeField] private Transform listener; // player; found by tag if unset

        private void Awake()
        {
            Configure(windSource, windLoop);
            Configure(shoreSource, wavesLoop);
            if (listener == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) listener = player.transform;
            }
        }

        private void OnEnable()
        {
            if (windSource != null && windSource.clip != null) windSource.Play();
            if (shoreSource != null && shoreSource.clip != null) shoreSource.Play();
        }

        private void Update()
        {
            float dt = Time.deltaTime * fadeSpeed;

            if (windSource != null && windSource.clip != null)
            {
                float windIntensity = WeatherSystem.Instance != null ? WeatherSystem.Instance.CurrentWindIntensity : 0.3f;
                float target = Mathf.Lerp(windBaseVolume, windStormVolume, Mathf.Clamp01(windIntensity));
                windSource.volume = Mathf.MoveTowards(windSource.volume, target, dt);
            }

            if (shoreSource != null && shoreSource.clip != null && listener != null)
            {
                float t = Mathf.InverseLerp(shoreSilentAltitude, shoreFullAltitude, listener.position.y);
                shoreSource.volume = Mathf.MoveTowards(shoreSource.volume, shoreMaxVolume * t, dt);
            }
        }

        private static void Configure(AudioSource source, AudioClip clip)
        {
            if (source == null) return;
            if (clip != null) source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }
    }
}
