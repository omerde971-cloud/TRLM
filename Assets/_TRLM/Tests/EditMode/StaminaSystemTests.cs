using NUnit.Framework;
using UnityEngine;
using TRLM.Survival;

namespace TRLM.Tests
{
    public class StaminaSystemTests
    {
        private StaminaSystem CreateStamina()
        {
            var go = new GameObject("TestStamina");
            return go.AddComponent<StaminaSystem>();
        }

        [Test]
        public void ConsumeSprint_ReducesStamina()
        {
            var stamina = CreateStamina();
            float before = stamina.CurrentStamina;

            stamina.ConsumeSprint(1f); // 1 second at default 20/s drain

            Assert.Less(stamina.CurrentStamina, before);
            Object.DestroyImmediate(stamina.gameObject);
        }

        [Test]
        public void ConsumeSprint_CannotGoBelowZero()
        {
            var stamina = CreateStamina();

            for (int i = 0; i < 50; i++)
                stamina.ConsumeSprint(1f);

            Assert.AreEqual(0f, stamina.CurrentStamina);
            Object.DestroyImmediate(stamina.gameObject);
        }

        [Test]
        public void ConsumeSprint_ReturnsFalse_OnceExhausted()
        {
            var stamina = CreateStamina();

            bool result = true;
            for (int i = 0; i < 50 && result; i++)
                result = stamina.ConsumeSprint(1f);

            Assert.IsFalse(result);
            Assert.IsTrue(stamina.IsExhausted);
            Object.DestroyImmediate(stamina.gameObject);
        }

        [Test]
        public void ConsumeJump_FailsWhenNotEnoughStamina()
        {
            var stamina = CreateStamina();
            stamina.ConsumeSprint(10f); // drain to 0 well past max

            bool jumped = stamina.ConsumeJump();

            Assert.IsFalse(jumped);
            Object.DestroyImmediate(stamina.gameObject);
        }

        [Test]
        public void Regenerate_RespectsMaximum()
        {
            var stamina = CreateStamina();
            stamina.ConsumeSprint(0.5f); // small drain, current < max

            // Advance past the regen delay by a large margin so regen kicks in and overshoots.
            for (int i = 0; i < 200; i++)
                stamina.Tick(0.1f);

            Assert.AreEqual(stamina.MaxStamina, stamina.CurrentStamina);
            Object.DestroyImmediate(stamina.gameObject);
        }

        [Test]
        public void Regenerate_DoesNotStart_BeforeDelayElapses()
        {
            var stamina = CreateStamina();
            stamina.ConsumeSprint(1f);
            float afterDrain = stamina.CurrentStamina;

            stamina.Tick(0.1f); // well under the default 1.5s regenDelay

            Assert.AreEqual(afterDrain, stamina.CurrentStamina);
            Object.DestroyImmediate(stamina.gameObject);
        }
    }
}
