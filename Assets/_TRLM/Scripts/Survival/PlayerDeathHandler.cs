using UnityEngine;

namespace TRLM.Survival
{
    /// <summary>
    /// Sprint 06 soft-lock safety net: previously nothing subscribed to the player's
    /// HealthSystem.OnDeath at all (no respawn, no game-over state, not even a log). A full
    /// death/respawn system is explicitly out of scope for the vertical slice — this only logs a
    /// clear console message so a death is visible/debuggable. Further damage is already blocked
    /// by HealthSystem.TakeDamage's own `if (IsDead) return;` guard — verified, not re-implemented
    /// here.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        private HealthSystem health;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath -= HandleDeath;
        }

        private void HandleDeath()
        {
            Debug.LogWarning("[PlayerDeathHandler] Player has died. No respawn/game-over flow exists yet " +
                              "(known, documented vertical-slice gap) — further damage is blocked by HealthSystem.IsDead.");
        }
    }
}
