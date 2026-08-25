using UnityEngine;
using TRLM.World;

namespace TRLM.Audio
{
    public class ForestAmbienceController : MonoBehaviour
    {
        [SerializeField] private AudioSource daySource;
        [SerializeField] private AudioSource nightSource;
        [SerializeField] private AudioClip dayLoop;
        [SerializeField] private AudioClip nightLoop;
        [SerializeField] private DayNightSystem dayNight;
        [SerializeField, Range(0f, 1f)] private float dayVolume = 0.22f;
        [SerializeField, Range(0f, 1f)] private float nightVolume = 0.18f;
        [SerializeField] private float fadeSpeed = 0.35f;

        private void Awake()
        {
            if (dayNight == null) dayNight = FindFirstObjectByType<DayNightSystem>();
            Configure(daySource, dayLoop);
            Configure(nightSource, nightLoop);
        }

        private void OnEnable()
        {
            if (daySource != null && daySource.clip != null && !daySource.isPlaying) daySource.Play();
            if (nightSource != null && nightSource.clip != null && !nightSource.isPlaying) nightSource.Play();
        }

        private void Update()
        {
            bool night = dayNight != null && dayNight.IsNight;
            if (daySource != null) daySource.volume = Mathf.MoveTowards(daySource.volume, night ? 0f : dayVolume, fadeSpeed * Time.deltaTime);
            if (nightSource != null) nightSource.volume = Mathf.MoveTowards(nightSource.volume, night ? nightVolume : 0f, fadeSpeed * Time.deltaTime);
        }

        private static void Configure(AudioSource source, AudioClip clip)
        {
            if (source == null) return;
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }
    }
}
