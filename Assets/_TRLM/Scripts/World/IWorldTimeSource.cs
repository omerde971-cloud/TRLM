namespace TRLM.World
{
    /// <summary>
    /// Abstract time-of-day source. Wildlife (and later, other systems) read IsNight through
    /// this interface instead of any concrete day/night implementation, so a future
    /// DayNightSystem can replace DebugWorldTimeSource without touching AI code.
    /// </summary>
    public interface IWorldTimeSource
    {
        bool IsNight { get; }

        /// <summary>0 = midnight, 0.5 = midday, 1 = next midnight. Optional finer-grained value for future use; DebugWorldTimeSource just derives it from IsNight.</summary>
        float NormalizedTimeOfDay { get; }
    }
}
