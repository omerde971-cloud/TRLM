using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TRLM.Survival
{
    /// <summary>
    /// Restrained visual feedback for low psychological stability: a subtle vignette only, no
    /// screen blur/distortion/color overlays (explicitly ruled out by the brief). Reads the
    /// Vignette override on a URP Volume Profile and lerps its intensity toward a small
    /// per-tier target instead of snapping, so a tier change doesn't read as a hard flash.
    /// </summary>
    public class PsychologicalVisualEffects : MonoBehaviour
    {
        [SerializeField] private PsychologicalState psychState;
        [SerializeField] private Volume volume;
        [SerializeField] private float lerpSpeed = 0.5f;

        [Header("Vignette Intensity Per Tier")]
        [SerializeField] private float stableVignette = 0f;
        [SerializeField] private float uneasyVignette = 0.06f;
        [SerializeField] private float stressedVignette = 0.14f;
        [SerializeField] private float criticalVignette = 0.24f;

        private Vignette vignette;
        private float targetIntensity;

        private void Awake()
        {
            if (psychState == null) psychState = GetComponentInParent<PsychologicalState>();
            if (volume != null && volume.profile != null)
                volume.profile.TryGet(out vignette);
        }

        private void OnEnable()
        {
            if (psychState != null) psychState.OnTierChanged += HandleTierChanged;
        }

        private void OnDisable()
        {
            if (psychState != null) psychState.OnTierChanged -= HandleTierChanged;
        }

        private void HandleTierChanged(PsychologicalState.Tier tier)
        {
            targetIntensity = tier switch
            {
                PsychologicalState.Tier.Uneasy => uneasyVignette,
                PsychologicalState.Tier.Stressed => stressedVignette,
                PsychologicalState.Tier.Critical => criticalVignette,
                _ => stableVignette,
            };
        }

        private void Update()
        {
            if (vignette == null) return;
            vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, targetIntensity, Time.deltaTime * lerpSpeed);
        }
    }
}
