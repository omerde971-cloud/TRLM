using UnityEngine;

namespace TRLM.UI
{
    /// <summary>
    /// Very slow, restrained drift on the menu's background RectTransform — a few pixels of pan and
    /// a hair of scale "breathing", nothing a player would consciously register as animation. Per
    /// spec: atmospheric only, no giant movement, not a looping mobile-menu effect.
    /// </summary>
    public class MainMenuAtmosphere : MonoBehaviour
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private float panAmplitudePixels = 14f;
        [SerializeField] private float panPeriodSeconds = 45f;
        [SerializeField] private float scaleAmplitude = 0.01f;
        [SerializeField] private float scalePeriodSeconds = 60f;

        private Vector2 basePosition;
        private Vector3 baseScale;

        private void Awake()
        {
            if (background == null) background = GetComponent<RectTransform>();
            basePosition = background.anchoredPosition;
            baseScale = background.localScale;
        }

        private void Update()
        {
            if (background == null) return;

            float t = Time.unscaledTime;
            float panPhase = (t / Mathf.Max(1f, panPeriodSeconds)) * Mathf.PI * 2f;
            float scalePhase = (t / Mathf.Max(1f, scalePeriodSeconds)) * Mathf.PI * 2f;

            background.anchoredPosition = basePosition + new Vector2(Mathf.Sin(panPhase) * panAmplitudePixels, Mathf.Cos(panPhase * 0.6f) * panAmplitudePixels * 0.4f);
            background.localScale = baseScale * (1f + Mathf.Sin(scalePhase) * scaleAmplitude);
        }
    }
}
