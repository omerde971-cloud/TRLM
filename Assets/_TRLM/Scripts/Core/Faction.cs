namespace TRLM.Core
{
    /// <summary>
    /// Coarse team classification for hit-detection filtering (Sprint 07 Section 29 — friendly
    /// fire foundation). Not a full relationship matrix, just enough to support one explicit
    /// rule: player-fired weapons never damage PlayerTeam targets.
    /// </summary>
    public enum Faction
    {
        PlayerTeam,
        Wildlife,
        HumanHostile,
        Environment
    }
}
