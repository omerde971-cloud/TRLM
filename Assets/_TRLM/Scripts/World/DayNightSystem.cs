using UnityEngine;

namespace TRLM.World
{
    /// <summary>
    /// Real day/night cycle, drop-in replacement for DebugWorldTimeSource (implements the same
    /// IWorldTimeSource interface). Drives a simple lerp of the scene's Directional Light between
    /// day/night intensity, color and rotation — not a full sky system. Wildlife/ColdExposure keep
    /// working unmodified once their timeSourceBehaviour reference is re-pointed at this component.
    /// </summary>
    public class DayNightSystem : MonoBehaviour, IWorldTimeSource
    {
        [Header("Cycle Duration")]
        [SerializeField] private float dayDurationSeconds = 480f;   // ~8 min
        [SerializeField] private float nightDurationSeconds = 600f; // ~10 min
        [Tooltip("Seconds into the cycle the game starts at. Default puts the opening in mid-morning light.")]
        [SerializeField] private float startElapsedSeconds = 100f;

        [Header("Sun")]
        [SerializeField] private Light sun; // Directional Light; found by type if not assigned
        [SerializeField] private Color dayColor = new Color(1f, 0.96f, 0.88f);
        [SerializeField] private Color horizonColor = new Color(1f, 0.62f, 0.35f); // dawn/dusk warmth
        [SerializeField] private Color nightColor = new Color(0.15f, 0.2f, 0.35f);
        [SerializeField] private float dayIntensity = 1.1f;
        [SerializeField] private float nightIntensity = 0.05f;

        [Header("Ambient & Fog Sync")]
        [SerializeField] private Color dayAmbientSky = new Color(0.52f, 0.60f, 0.70f);
        [SerializeField] private Color dayAmbientEquator = new Color(0.42f, 0.44f, 0.44f);
        [SerializeField] private Color dayAmbientGround = new Color(0.22f, 0.21f, 0.18f);
        [SerializeField] private Color nightAmbientSky = new Color(0.07f, 0.09f, 0.14f);
        [SerializeField] private Color nightAmbientEquator = new Color(0.05f, 0.06f, 0.09f);
        [SerializeField] private Color nightAmbientGround = new Color(0.02f, 0.02f, 0.03f);
        [SerializeField] private Color dayFogColor = new Color(0.55f, 0.62f, 0.64f);
        [SerializeField] private Color nightFogColor = new Color(0.04f, 0.06f, 0.10f);

        // 0 = start of day, dayDurationSeconds = start of night, full cycle = day+night.
        private float elapsedSeconds;

        public float NormalizedTimeOfDay { get; private set; }
        public bool IsNight { get; private set; }

        /// <summary>Number of full day/night cycles completed so far, for save metadata/UI. Starts
        /// at 1 (the player's first day), increments each time night rolls back over into day.</summary>
        public int DayCount { get; private set; } = 1;

        /// <summary>Raw elapsed-seconds-into-the-cycle, for save persistence — not otherwise
        /// exposed since every other system reads NormalizedTimeOfDay/IsNight instead.</summary>
        public float ElapsedSeconds => elapsedSeconds;

        private void Awake()
        {
            if (sun == null)
            {
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (light.type == LightType.Directional) { sun = light; break; }
                }
            }

            float cycleLength = Mathf.Max(1f, dayDurationSeconds + nightDurationSeconds);
            elapsedSeconds = Mathf.Repeat(startElapsedSeconds, cycleLength);
            IsNight = elapsedSeconds >= dayDurationSeconds;
            NormalizedTimeOfDay = elapsedSeconds / cycleLength;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            ApplyLighting();
        }

        private void Update()
        {
            float cycleLength = Mathf.Max(1f, dayDurationSeconds + nightDurationSeconds);
            elapsedSeconds = Mathf.Repeat(elapsedSeconds + Time.deltaTime, cycleLength);

            bool wasNight = IsNight;
            IsNight = elapsedSeconds >= dayDurationSeconds;
            NormalizedTimeOfDay = elapsedSeconds / cycleLength;

            ApplyLighting();

            if (IsNight && !wasNight)
                // Gated: nightfall is a clock event, not a route event — without the gate it
                // leapfrogs the exploration steps whenever the player is slower than the day timer.
                TRLM.Progression.ObjectiveSystem.Instance?.AdvanceToInOrder(
                    TRLM.Progression.ObjectiveStep.NightBegins,
                    TRLM.Progression.ObjectiveStep.AcquireEssentialLoot);
            if (!IsNight && wasNight)
                DayCount++;
        }

        private void ApplyLighting()
        {
            if (sun == null) return;

            float sunAngle;   // elevation: 0 = sunrise horizon, 90 = noon, 180 = sunset horizon
            float nightness;  // 0 = full day, 1 = full night
            float horizonGlow; // warm tint strength near sunrise/sunset

            if (!IsNight)
            {
                float dayFrac = Mathf.Clamp01(elapsedSeconds / Mathf.Max(1f, dayDurationSeconds));
                sunAngle = Mathf.Lerp(4f, 176f, dayFrac);
                // Sun arc height drives brightness: full at midday, fading at dawn/dusk.
                float arc = Mathf.Sin(dayFrac * Mathf.PI);
                nightness = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arc * 2.2f));
                horizonGlow = Mathf.Clamp01(1f - arc * 1.6f);
            }
            else
            {
                float nightFrac = Mathf.Clamp01((elapsedSeconds - dayDurationSeconds) / Mathf.Max(1f, nightDurationSeconds));
                sunAngle = Mathf.Lerp(184f, 356f, nightFrac);
                nightness = 1f;
                horizonGlow = 0f;
            }

            Color litColor = Color.Lerp(dayColor, horizonColor, horizonGlow);
            sun.color = Color.Lerp(litColor, nightColor, nightness);
            sun.intensity = Mathf.Lerp(dayIntensity, nightIntensity, nightness);
            sun.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

            RenderSettings.ambientSkyColor = Color.Lerp(dayAmbientSky, nightAmbientSky, nightness);
            RenderSettings.ambientEquatorColor = Color.Lerp(dayAmbientEquator, nightAmbientEquator, nightness);
            RenderSettings.ambientGroundColor = Color.Lerp(dayAmbientGround, nightAmbientGround, nightness);
            Color fogTarget = Color.Lerp(dayFogColor, nightFogColor, nightness);
            RenderSettings.fogColor = Color.Lerp(fogTarget, new Color(horizonColor.r, horizonColor.g, horizonColor.b) * 0.7f, horizonGlow * 0.35f);
        }

        /// <summary>Called by SleepInteraction to jump straight to the next morning.</summary>
        public void SkipToMorning()
        {
            elapsedSeconds = 0f;
            ApplyLighting();
            IsNight = false;
            NormalizedTimeOfDay = 0f;
            DayCount++;
        }

        /// <summary>Save/load restore only. Jumps straight to a saved point in the cycle without
        /// incrementing DayCount a second time (the saved value already accounts for it) and
        /// without firing NightBegins on the ObjectiveSystem (that already happened last session).</summary>
        public void SetTimeState(float savedElapsedSeconds, int savedDayCount)
        {
            float cycleLength = Mathf.Max(1f, dayDurationSeconds + nightDurationSeconds);
            elapsedSeconds = Mathf.Repeat(savedElapsedSeconds, cycleLength);
            IsNight = elapsedSeconds >= dayDurationSeconds;
            NormalizedTimeOfDay = elapsedSeconds / cycleLength;
            DayCount = Mathf.Max(1, savedDayCount);
            ApplyLighting();
        }
    }
}
