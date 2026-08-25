using System.Collections.Generic;
using UnityEngine;

namespace TRLM.Survival
{
    /// <summary>
    /// Container that ticks active IStatusEffects and removes expired ones. Holds no effect
    /// logic itself — real effects (Bleeding, Poison, etc.) are a future sprint; this just
    /// proves the architecture via MinorBleedEffect.
    /// </summary>
    [RequireComponent(typeof(HealthSystem))]
    public class StatusEffectController : MonoBehaviour
    {
        private readonly List<IStatusEffect> effects = new List<IStatusEffect>();
        private HealthSystem health;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
        }

        public void ApplyEffect(IStatusEffect effect)
        {
            if (effect != null) effects.Add(effect);
        }

        public bool HasEffect(string id)
        {
            foreach (var e in effects)
                if (e.Id == id) return true;
            return false;
        }

        private void Update()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                effects[i].Tick(Time.deltaTime, health);
                if (effects[i].IsExpired)
                    effects.RemoveAt(i);
            }
        }
    }
}
