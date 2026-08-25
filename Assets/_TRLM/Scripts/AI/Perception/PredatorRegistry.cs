using System.Collections.Generic;
using UnityEngine;

namespace TRLM.AI.Perception
{
    /// <summary>
    /// A hostile animal the companion squad can notice. Implemented by WolfAI/BearAI so
    /// CompanionAwareness never needs species-specific references (or FindObjectsByType scans).
    /// </summary>
    public interface IPredator
    {
        Transform PredatorTransform { get; }
        /// <summary>True while the animal is actively menacing (alert/stalk/chase/attack states) —
        /// idle roaming animals shouldn't put the squad on edge from 40m away.</summary>
        bool IsMenacing { get; }
        bool IsDeadPredator { get; }
    }

    /// <summary>Static registry of live predators, maintained by the predators themselves in
    /// OnEnable/OnDisable. Read by CompanionAwareness and passive wildlife on their sensor ticks.</summary>
    public static class PredatorRegistry
    {
        private static readonly List<IPredator> predators = new List<IPredator>();

        public static IReadOnlyList<IPredator> All => predators;

        public static void Register(IPredator p)
        {
            if (p != null && !predators.Contains(p)) predators.Add(p);
        }

        public static void Unregister(IPredator p) => predators.Remove(p);

        /// <summary>Nearest live predator to a point within maxRange; null if none. menacingOnly
        /// filters to animals that are actively hostile right now.</summary>
        public static IPredator FindNearest(Vector3 point, float maxRange, bool menacingOnly)
        {
            IPredator best = null;
            float bestSqr = maxRange * maxRange;
            for (int i = 0; i < predators.Count; i++)
            {
                var p = predators[i];
                if (p == null || p.IsDeadPredator || p.PredatorTransform == null) continue;
                if (menacingOnly && !p.IsMenacing) continue;
                float sqr = (p.PredatorTransform.position - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = p;
                }
            }
            return best;
        }
    }
}
