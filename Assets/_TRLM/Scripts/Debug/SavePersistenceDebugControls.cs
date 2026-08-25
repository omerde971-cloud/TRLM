using UnityEngine;
using TRLM.Save;
using TRLM.Survival;
using TRLM.Companions;
using TRLM.Weather;
using TRLM.World;
using TRLM.Progression;

namespace TRLM.DebugTools
{
    /// <summary>
    /// Developer-only OnGUI panel for 95_Test_SavePersistence, same pattern as
    /// WeatherSanityDebugControls. Never included in a shipping UI flow.
    /// </summary>
    public class SavePersistenceDebugControls : MonoBehaviour
    {
        [SerializeField] private SaveOrchestrator orchestrator;
        [SerializeField] private HealthSystem playerHealth;
        [SerializeField] private HungerSystem hunger;
        [SerializeField] private ThirstSystem thirst;
        [SerializeField] private PsychologicalState psych;
        [SerializeField] private WeatherSystem weather;
        [SerializeField] private DayNightSystem dayNight;

        private string lastResult = "";

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(240, 10, 260, 560), GUI.skin.box);
            GUILayout.Label("Save / Persistence Debug");

            GUILayout.Label("Save");
            if (GUILayout.Button("Save Autosave")) Report(orchestrator?.SaveAutosave());
            if (GUILayout.Button("Save Manual 1")) Report(orchestrator?.SaveManual(1));

            GUILayout.Space(6);
            GUILayout.Label("Load");
            if (GUILayout.Button("Load Autosave")) Report(orchestrator?.LoadAutosave());
            if (GUILayout.Button("Load Manual 1")) Report(orchestrator?.LoadManual(1));

            GUILayout.Space(6);
            if (GUILayout.Button("Delete Manual 1"))
                lastResult = orchestrator != null && orchestrator.DeleteManualSlot(1) ? "Deleted manual 1" : "Delete failed";

            string reason = null;
            bool canSave = orchestrator != null && orchestrator.CanManualSave(out reason);
            GUILayout.Label(canSave ? "CanManualSave: yes" : $"CanManualSave: no ({reason})");

            GUILayout.Space(8);
            GUILayout.Label("Mutate State");
            if (GUILayout.Button("Damage Player -20")) playerHealth?.TakeDamage(20f, null);
            if (GUILayout.Button("Set Hunger 30")) hunger?.RestoreHunger(30f);
            if (GUILayout.Button("Set Thirst 30")) thirst?.RestoreThirst(30f);
            if (GUILayout.Button("Set Sanity 30")) psych?.DebugSetStability(30f);
            if (GUILayout.Button("Kill Jonah")) FindCompanionHealth(CompanionId.Jonah)?.TakeDamage(9999f, null);
            if (GUILayout.Button("Change Weather: Storm")) weather?.ForceWeather(WeatherType.Storm, 2f);
            if (GUILayout.Button("Skip To Morning")) dayNight?.SkipToMorning();
            if (GUILayout.Button("Advance Objective")) ObjectiveSystem.Instance?.Advance();
            if (GUILayout.Button("Change Difficulty: Hard")) DifficultySettings.ApplyPreset(DifficultyLevel.Hard);
            if (GUILayout.Button("Change Difficulty: Story")) DifficultySettings.ApplyPreset(DifficultyLevel.Story);
            if (GUILayout.Button("Change Difficulty: Normal")) DifficultySettings.ApplyPreset(DifficultyLevel.Normal);

            GUILayout.Space(8);
            GUILayout.Label($"Difficulty: {DifficultySettings.CurrentLevel}");
            GUILayout.Label($"Objective: {ObjectiveSystem.Instance?.Current}");
            GUILayout.Label($"Playtime: {orchestrator?.TotalPlaytimeSeconds:0}s");
            GUILayout.Label(lastResult);

            GUILayout.EndArea();
        }

        private static HealthSystem FindCompanionHealth(CompanionId id)
        {
            foreach (var identity in FindObjectsByType<CompanionIdentity>(FindObjectsSortMode.None))
                if (identity.Id == id) return identity.GetComponent<HealthSystem>();
            return null;
        }

        private void Report(SaveLoadOutcome? outcome)
        {
            lastResult = outcome.HasValue ? $"{outcome.Value.Result}: {outcome.Value.Message}" : "no SaveOrchestrator";
        }
    }
}
