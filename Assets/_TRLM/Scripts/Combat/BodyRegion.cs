namespace TRLM.Combat
{
    /// <summary>Coarse regional hit-location classification for RegionalInjurySystem (Sprint 07
    /// Section 20). No precise hit-location data exists outside gunfire raycasts, so most damage
    /// sources (wolf bites, rockfalls, melee received) roll a weighted-random region instead.</summary>
    public enum BodyRegion
    {
        Head,
        Torso,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }
}
