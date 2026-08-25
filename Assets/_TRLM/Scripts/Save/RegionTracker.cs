namespace TRLM.Save
{
    /// <summary>
    /// Current logical region for save metadata (Sprint 10 Part W) — set explicitly by
    /// RegionEntryTrigger volumes carrying a regionName, never derived from nearest-object-name
    /// guessing. Defaults to the vertical slice's starting area.
    /// </summary>
    public static class RegionTracker
    {
        public static string CurrentRegionName = "Sea Approach";
    }
}
