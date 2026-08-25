using System;
using UnityEngine;

namespace TRLM.AI.Perception
{
    /// <summary>
    /// Minimal world-noise bus. Anything can raise a noise (footsteps, jump landings, a
    /// future gunshot); anything that cares (wolf perception) subscribes. Kept as a static
    /// event rather than a scene object so listeners never need a scene reference wired up.
    /// </summary>
    public static class NoiseEvents
    {
        /// <summary>(worldPosition, loudness in meters — roughly "how far this can be heard").</summary>
        public static event Action<Vector3, float> OnNoise;

        public static void Raise(Vector3 position, float loudness) => OnNoise?.Invoke(position, loudness);
    }
}
