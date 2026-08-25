namespace TRLM.Survival
{
    /// <summary>
    /// Contract for future status effects (Bleeding, Poison, Infection, Trauma, Hypothermia).
    /// Implementations must go through the HealthSystem passed to Tick — never bypass it.
    /// </summary>
    public interface IStatusEffect
    {
        string Id { get; }
        void Tick(float deltaTime, HealthSystem target);
        bool IsExpired { get; }
    }
}
