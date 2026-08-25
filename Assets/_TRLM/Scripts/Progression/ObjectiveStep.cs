namespace TRLM.Progression
{
    /// <summary>Vertical-slice objective order. Not every step has an automatic trigger yet —
    /// see ObjectiveSystem's class remarks for which ones are wired vs. AdvanceTo-only.</summary>
    public enum ObjectiveStep
    {
        PreparationComplete,
        RowToIsland,
        ReachLandingZone,
        EnterCoastalForest,
        ReachAbandonedHouse,
        SearchHouse,
        AcquireEssentialLoot,
        NightBegins,
        WolfThreat,
        ReachSafeHouse,
        LightFire,
        Sleep,
        WakeNextMorning,
        SliceComplete,

        // --- Sprint 3: Cave Threshold beat ---
        // Appended (never inserted) so existing persisted ObjectiveStep values are preserved.
        ReachCaveEntrance,      // player arrives at the summit cave-staging area (threshold dialogue fires)
        EnterCave,              // player crosses into the cave interior
        RecoverFirstProphecyPage, // first Kehanet Defteri page collected inside the cave
        CaveThresholdComplete
    }
}
