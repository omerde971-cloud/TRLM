using UnityEngine;

namespace TRLM.Weather
{
    /// <summary>
    /// Makes rain visibly wet the world without rewriting any shared/third-party material asset.
    /// Always sets a global shader float (_TRLM_RainWetness01, 0-1) that any current or future TRLM
    /// shader/shader-graph can read. Additionally, for a small explicitly-assigned set of production
    /// surface renderers, darkens albedo and raises smoothness via MaterialPropertyBlock — a
    /// per-renderer instance override, so the shared .mat asset on disk is never modified. The
    /// renderer list starts empty; art/Haiku assigns the ground/rock renderers that should visibly
    /// wet in the Inspector, this is infrastructure rather than a scene-wide material rewrite.
    /// </summary>
    public class WetSurfaceResponse : MonoBehaviour
    {
        private static readonly int GlobalWetnessId = Shader.PropertyToID("_TRLM_RainWetness01");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Selective Surfaces (optional — empty by default)")]
        [SerializeField] private Renderer[] wetSurfaces;
        [SerializeField] private float wetSmoothness = 0.9f;
        [SerializeField] private float wetAlbedoDarken = 0.25f;

        [Header("Response")]
        [SerializeField] private float riseSpeed = 0.6f;
        [SerializeField] private float fallSpeed = 0.15f;

        private float wetness01;
        private MaterialPropertyBlock mpb;
        private Color[] baseColors;
        private float[] baseSmoothness;

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            if (wetSurfaces == null) return;

            baseColors = new Color[wetSurfaces.Length];
            baseSmoothness = new float[wetSurfaces.Length];
            for (int i = 0; i < wetSurfaces.Length; i++)
            {
                var r = wetSurfaces[i];
                var mat = r != null ? r.sharedMaterial : null;
                baseColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
                baseSmoothness[i] = mat != null && mat.HasProperty(SmoothnessId) ? mat.GetFloat(SmoothnessId) : 0.5f;
            }
        }

        private void Update()
        {
            float rain = WeatherSystem.Instance != null ? WeatherSystem.Instance.CurrentRainIntensity : 0f;
            float speed = rain > wetness01 ? riseSpeed : fallSpeed;
            wetness01 = Mathf.MoveTowards(wetness01, rain, speed * Time.deltaTime);

            Shader.SetGlobalFloat(GlobalWetnessId, wetness01);
            ApplySelectiveSurfaces();
        }

        private void ApplySelectiveSurfaces()
        {
            if (wetSurfaces == null) return;
            for (int i = 0; i < wetSurfaces.Length; i++)
            {
                var r = wetSurfaces[i];
                if (r == null) continue;

                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, Color.Lerp(baseColors[i], baseColors[i] * (1f - wetAlbedoDarken), wetness01));
                mpb.SetFloat(SmoothnessId, Mathf.Lerp(baseSmoothness[i], wetSmoothness, wetness01));
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
