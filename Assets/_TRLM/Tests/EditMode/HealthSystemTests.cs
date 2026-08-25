using NUnit.Framework;
using UnityEngine;
using TRLM.Survival;

namespace TRLM.Tests
{
    public class HealthSystemTests
    {
        private HealthSystem CreateHealth()
        {
            // AddComponent invokes Awake() synchronously in Edit Mode, so CurrentHealth
            // is already initialized from maxHealth by the time this returns.
            var go = new GameObject("TestHealth");
            return go.AddComponent<HealthSystem>();
        }

        [Test]
        public void TakeDamage_ReducesHealth()
        {
            var health = CreateHealth();
            float before = health.CurrentHealth;

            health.TakeDamage(20f);

            Assert.AreEqual(before - 20f, health.CurrentHealth);
            Object.DestroyImmediate(health.gameObject);
        }

        [Test]
        public void TakeDamage_CannotGoBelowZero()
        {
            var health = CreateHealth();

            health.TakeDamage(9999f);

            Assert.AreEqual(0f, health.CurrentHealth);
            Object.DestroyImmediate(health.gameObject);
        }

        [Test]
        public void Heal_CannotExceedMaxHealth()
        {
            var health = CreateHealth();

            health.Heal(9999f);

            Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
            Object.DestroyImmediate(health.gameObject);
        }

        [Test]
        public void Death_FiresOnDeathEvent_WhenHealthReachesZero()
        {
            var health = CreateHealth();
            bool died = false;
            health.OnDeath += () => died = true;

            health.TakeDamage(health.MaxHealth);

            Assert.IsTrue(died);
            Assert.IsTrue(health.IsDead);
            Object.DestroyImmediate(health.gameObject);
        }

        [Test]
        public void TakeDamage_DoesNothing_AfterDeath()
        {
            var health = CreateHealth();
            health.TakeDamage(health.MaxHealth); // dies
            health.Heal(50f);

            Assert.AreEqual(0f, health.CurrentHealth);
            Object.DestroyImmediate(health.gameObject);
        }
    }
}
