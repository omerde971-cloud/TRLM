using System;
using UnityEngine;
using TRLM.Core;

namespace TRLM.Survival
{
    /// <summary>
    /// Reusable health component. Future systems (bleeding, poisoning, hypothermia,
    /// animal attacks, firearms) should call TakeDamage/Heal rather than touching
    /// CurrentHealth directly.
    /// </summary>
    public class HealthSystem : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = -1f; // -1 = "not yet initialized"

        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<float, GameObject> OnDamaged;  // (amount, source)
        public event Action OnDeath;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => EnsureInitialized();
        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / maxHealth);
        public bool IsDead { get; private set; }

        // Lazily initialized instead of relying on Awake, which is not guaranteed to have
        // run yet when this component is queried immediately after AddComponent (e.g. from
        // Edit Mode tests or other Awake methods racing against this one).
        private float EnsureInitialized()
        {
            if (currentHealth < 0f)
                currentHealth = maxHealth;
            return currentHealth;
        }

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDead || amount <= 0f) return;

            currentHealth = Mathf.Max(0f, EnsureInitialized() - amount);
            OnDamaged?.Invoke(amount, source);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
                Die();
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            currentHealth = Mathf.Min(maxHealth, EnsureInitialized() + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Die()
        {
            IsDead = true;
            OnDeath?.Invoke();
        }

        /// <summary>Save/load restore only. Sets health/death state directly instead of going
        /// through TakeDamage/Heal so restoring an alive save never re-runs damage-response side
        /// effects (screen shake, injury rolls, etc.) for damage that already happened last session.
        /// Restoring into a dead state still fires OnDeath — real listeners (CompanionAI.HandleDeath
        /// disabling the NavMeshAgent, etc.) need to run so the object ends up in the same disabled
        /// state it would after a live death; callers that must avoid a specific listener's sideeffect
        /// (e.g. PsychologicalState's morale hit) guard themselves instead (see
        /// PsychologicalState.MarkCompanionDeathAlreadyProcessed).</summary>
        public void RestoreState(float health, bool dead)
        {
            currentHealth = Mathf.Clamp(health, 0f, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (dead && !IsDead) Die();
            else IsDead = dead;
        }
    }
}
