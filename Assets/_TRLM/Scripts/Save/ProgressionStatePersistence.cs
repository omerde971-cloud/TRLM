using UnityEngine;
using TRLM.Progression;
using TRLM.World;
using TRLM.Weather;
using TRLM.Survival;

namespace TRLM.Save
{
    /// <summary>Objective, day/night, weather, team provisions — small enough state that splitting
    /// each into its own file would be more ceremony than value; still one focused adapter, not
    /// folded into SaveOrchestrator itself.</summary>
    public static class ProgressionStatePersistence
    {
        public static ProgressionData CaptureProgression()
        {
            var data = new ProgressionData { currentObjective = ObjectiveSystem.Instance != null ? ObjectiveSystem.Instance.Current : ObjectiveStep.PreparationComplete };
            var dialogue = TRLM.Dialogue.DialogueSystem.Instance;
            if (dialogue != null) data.playedDialogueIds.AddRange(dialogue.PlayedOneShotIds);
            return data;
        }

        public static void RestoreProgression(ProgressionData d)
        {
            if (d == null) return;
            ObjectiveSystem.Instance?.AdvanceTo(d.currentObjective); // idempotent — safe no-op if already past this step
            TRLM.Dialogue.DialogueSystem.Instance?.SeedPlayedOneShots(d.playedDialogueIds);
        }

        public static TimeWeatherData CaptureTimeWeather(DayNightSystem dayNight)
        {
            return new TimeWeatherData
            {
                elapsedSeconds = dayNight != null ? dayNight.ElapsedSeconds : 0f,
                dayCount = dayNight != null ? dayNight.DayCount : 1,
                currentWeather = WeatherSystem.Instance != null ? WeatherSystem.Instance.CurrentWeather : WeatherType.Clear,
            };
        }

        public static void RestoreTimeWeather(TimeWeatherData d, DayNightSystem dayNight)
        {
            if (d == null) return;
            dayNight?.SetTimeState(d.elapsedSeconds, d.dayCount);
            WeatherSystem.Instance?.RestoreWeather(d.currentWeather);
        }

        public static TeamProvisionsData CaptureTeamProvisions(TeamProvisions provisions)
        {
            if (provisions == null) return new TeamProvisionsData();
            return new TeamProvisionsData
            {
                sharedFood = provisions.SharedFood,
                sharedWater = provisions.SharedWater,
                livingTeamMembers = provisions.LivingTeamMembers,
            };
        }

        public static void RestoreTeamProvisions(TeamProvisionsData d, TeamProvisions provisions) =>
            provisions?.RestoreProvisions(d.sharedFood, d.sharedWater, d.livingTeamMembers);
    }
}
