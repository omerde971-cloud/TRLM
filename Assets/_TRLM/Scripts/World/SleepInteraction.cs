using System.Collections;
using UnityEngine;
using TRLM.Interaction;
using TRLM.Survival;
using TRLM.Combat;

namespace TRLM.World
{
    /// <summary>
    /// E-to-sleep, only available while the player is inside the paired SafeHouseArea. Fades to
    /// black, skips to morning via DayNightSystem, and applies a partial (not full) survival-stat
    /// restore. Deliberately does not touch HealthSystem beyond a small natural-rest heal — sleep
    /// is not meant to erase injuries.
    /// </summary>
    public class SleepInteraction : MonoBehaviour, IInteractable
    {
        [Header("References")]
        [SerializeField] private SafeHouseArea safeHouseArea;
        [SerializeField] private DayNightSystem dayNightSystem;

        [Header("Fade")]
        [SerializeField] private float fadeSeconds = 1f;

        [Header("Rest Restore (partial, not full)")]
        [SerializeField] private float hungerRestore = 30f;
        [SerializeField] private float thirstRestore = 25f;
        [SerializeField] private float staminaRestoreFraction = 0.6f; // fraction of max stamina restored
        [SerializeField] private float naturalRestHeal = 10f;
        [SerializeField] private float restStabilityRestore = 20f; // psychological recovery — real but partial, not instant calm
        [SerializeField] private float restWarmthRestore = 30f; // non-critical cold state only — Sprint 09's "does NOT magically cure every serious injury" extends to hypothermia too

        private static Texture2D fadeTex;
        private float fadeAlpha;
        private bool sleeping;

        private void Awake()
        {
            // Scene wiring fallback: an unassigned reference silently skipped SkipToMorning,
            // leaving the player to "wake" into the same night they went to sleep in.
            if (dayNightSystem == null) dayNightSystem = FindAnyObjectByType<DayNightSystem>();
            if (safeHouseArea == null) safeHouseArea = GetComponentInParent<SafeHouseArea>();
        }

        public string InteractionPrompt =>
            (safeHouseArea == null || safeHouseArea.PlayerInside) ? "Sleep" : "Nowhere safe to sleep here";

        public void Interact(GameObject interactor)
        {
            if (sleeping) return;
            if (safeHouseArea != null && !safeHouseArea.PlayerInside) return;

            StartCoroutine(SleepRoutine(interactor));
        }

        private IEnumerator SleepRoutine(GameObject interactor)
        {
            sleeping = true;
            // Going to sleep IS the Sleep step — without this the enum value was permanently unreachable.
            TRLM.Progression.ObjectiveSystem.Instance?.AdvanceTo(TRLM.Progression.ObjectiveStep.Sleep);

            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                fadeAlpha = Mathf.Clamp01(t / fadeSeconds);
                yield return null;
            }
            fadeAlpha = 1f;

            ApplyRest(interactor);
            if (dayNightSystem != null) dayNightSystem.SkipToMorning();
            TRLM.Progression.ObjectiveSystem.Instance?.AdvanceTo(TRLM.Progression.ObjectiveStep.WakeNextMorning);
            // Waking up after a safe-house sleep is the natural end of the vertical slice —
            // nothing else in the sprint advanced to SliceComplete, leaving it permanently unreachable.
            // One frame apart so listeners (checkpoints, HUD notification) see both transitions.
            yield return null;
            TRLM.Progression.ObjectiveSystem.Instance?.AdvanceTo(TRLM.Progression.ObjectiveStep.SliceComplete);

            yield return new WaitForSeconds(0.3f);

            t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                fadeAlpha = 1f - Mathf.Clamp01(t / fadeSeconds);
                yield return null;
            }
            fadeAlpha = 0f;
            sleeping = false;
        }

        private void ApplyRest(GameObject interactor)
        {
            var hunger = interactor.GetComponentInChildren<HungerSystem>();
            hunger?.Eat(hungerRestore);

            var thirst = interactor.GetComponentInChildren<ThirstSystem>();
            thirst?.Drink(thirstRestore);

            var stamina = interactor.GetComponentInChildren<StaminaSystem>();
            if (stamina != null)
            {
                // No direct "add" API beyond regen ticks — approximate a rest restore by ticking
                // regen forward a large amount of simulated time, respecting RegenMultiplier.
                float need = stamina.MaxStamina * staminaRestoreFraction;
                float before = stamina.CurrentStamina;
                float safetyBudgetSeconds = 60f;
                while (stamina.CurrentStamina - before < need && safetyBudgetSeconds > 0f)
                {
                    stamina.Tick(1f);
                    safetyBudgetSeconds -= 1f;
                }
            }

            var health = interactor.GetComponentInChildren<HealthSystem>();
            health?.Heal(naturalRestHeal);

            // Sprint 07 (A2, Section 25) — "safe-house sleep may accelerate recovery" for
            // fracture/trauma markers and general injury severity. Small additive hook only.
            interactor.GetComponentInChildren<RegionalInjurySystem>()?.AccelerateRecovery();

            // Sprint 09 — sleep restores some psychological stability and warms the player back up
            // (not to full — a safe house doesn't erase a genuinely critical condition).
            interactor.GetComponentInChildren<PsychologicalState>()?.Recover(restStabilityRestore);
            interactor.GetComponentInChildren<ColdExposureSystem>()?.Warm(restWarmthRestore);
        }

        private void OnGUI()
        {
            if (fadeAlpha <= 0f) return;

            if (fadeTex == null)
            {
                fadeTex = new Texture2D(1, 1);
                fadeTex.SetPixel(0, 0, Color.black);
                fadeTex.Apply();
            }

            var color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), fadeTex);
            GUI.color = color;
        }
    }
}
