using System;
using TRLM.Companions;
using TRLM.Progression;

namespace TRLM.Save
{
    public enum SaveType { Autosave, Manual }

    /// <summary>
    /// Lightweight, cheap-to-read-many-of summary for a future save/load menu — deliberately a
    /// separate small file from the full SaveGameData so listing 6 slots doesn't require
    /// deserializing 6 full companion/world-state payloads. SaveManager writes this as a small
    /// sidecar alongside each slot's full data.
    /// </summary>
    [Serializable]
    public class SaveSlotMetadata
    {
        public int saveVersion = SaveManager.CurrentSaveVersion;
        public string slotId; // "autosave" or "manual_1".."manual_5"
        public SaveType saveType;
        public long savedAtUnixSeconds;
        public float totalPlaytimeSeconds;
        public string regionName = "";
        /// <summary>Scene the save was made in — lets the Main Menu's Continue/Load Game load the
        /// right scene without deserializing the full SaveGameData first.</summary>
        public string sceneName = "";
        public int dayCount = 1;
        public DifficultyLevel difficultyLevel;
        public string currentObjective = "";
        // Parallel arrays instead of a Dictionary (JsonUtility can't serialize one) — index i's id
        // maps to livingCompanions[i].
        public CompanionId[] companionIds = Array.Empty<CompanionId>();
        public bool[] livingCompanions = Array.Empty<bool>();
        /// <summary>Absolute path to a captured screenshot, or empty. See ScreenshotService —
        /// DEFERRED this sprint, always empty for now; the field exists so the future save UI has
        /// somewhere to read from without a schema change later.</summary>
        public string screenshotPath = "";
    }
}
