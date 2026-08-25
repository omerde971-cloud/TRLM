using UnityEngine;

namespace TRLM.Core
{
    /// <summary>
    /// Implemented by anything that can receive damage or healing.
    /// Kept intentionally minimal so future systems (combat, hazards, animals) can share it.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, GameObject source = null);
        void Heal(float amount);
        bool IsDead { get; }
    }
}
