namespace TRLM.Save
{
    /// <summary>
    /// Cross-scene handoff for the Main Menu: SaveOrchestrator only exists inside a gameplay scene
    /// (it needs PlayerStatePersistence etc. present), so the menu can't call Load()/NewGame()
    /// directly. Instead it stashes intent here, loads the target scene, and SaveOrchestrator.Start
    /// in that scene consumes it. Plain static fields are enough — nothing here needs to survive
    /// a domain reload, only a single scene load within the same play session.
    /// </summary>
    public static class PendingLoad
    {
        /// <summary>Slot id ("autosave" or "manual_1".."manual_5") to restore once the target
        /// scene's SaveOrchestrator wakes up, or null for none.</summary>
        public static string RequestedSlotId;

        /// <summary>Set by Main Menu's New Game flow so the target scene's SaveOrchestrator resets
        /// playtime/burial tracking and applies the chosen difficulty before anything else runs.</summary>
        public static bool NewGameRequested;

        public static TRLM.Progression.DifficultyLevel NewGameDifficulty = TRLM.Progression.DifficultyLevel.Normal;

        public static void ClearAll()
        {
            RequestedSlotId = null;
            NewGameRequested = false;
        }
    }
}
