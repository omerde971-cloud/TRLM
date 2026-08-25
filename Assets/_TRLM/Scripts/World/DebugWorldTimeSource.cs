using UnityEngine;

namespace TRLM.World
{
    /// <summary>
    /// Temporary stand-in for a real day/night system. Exposes a single Inspector/debug
    /// toggle so wildlife night-behavior can be tested now. One of these should exist in
    /// each gameplay scene; WildlifeSpawnManager finds it via IWorldTimeSource.
    /// </summary>
    public class DebugWorldTimeSource : MonoBehaviour, IWorldTimeSource
    {
        [SerializeField] private bool forceNight;

        public bool IsNight => forceNight;
        public float NormalizedTimeOfDay => forceNight ? 0f : 0.5f;

        /// <summary>Lets a debug UI or test script flip night state at runtime.</summary>
        public void SetNight(bool night) => forceNight = night;
    }
}
