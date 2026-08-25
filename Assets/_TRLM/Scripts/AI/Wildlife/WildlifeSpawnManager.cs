using System.Collections.Generic;
using UnityEngine;
using TRLM.World;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Scene-level coordinator: tracks how many of each species are alive across every
    /// zone (so a global cap can be enforced even with many zones), and exposes the
    /// scene's IWorldTimeSource so individual spawners/AI don't each need to find one.
    /// One per gameplay scene — this is the one place a light singleton-style accessor is
    /// justified (every zone/spawner needs it), not used anywhere else in the codebase.
    /// </summary>
    public class WildlifeSpawnManager : MonoBehaviour
    {
        public static WildlifeSpawnManager Instance { get; private set; }

        [SerializeField] private MonoBehaviour timeSourceBehaviour; // must implement IWorldTimeSource
        [SerializeField] private Transform player;

        private readonly Dictionary<WildlifeSpeciesProfile, int> activeCountsBySpecies = new Dictionary<WildlifeSpeciesProfile, int>();

        public IWorldTimeSource TimeSource { get; private set; }
        public Transform Player => player;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WildlifeSpawnManager] Multiple instances in scene — keeping the first.");
                Destroy(this);
                return;
            }
            Instance = this;
            TimeSource = timeSourceBehaviour as IWorldTimeSource;

            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null) player = found.transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int GetActiveCount(WildlifeSpeciesProfile species)
            => species != null && activeCountsBySpecies.TryGetValue(species, out int c) ? c : 0;

        public bool CanSpawnGlobally(WildlifeSpeciesProfile species)
            => species == null || GetActiveCount(species) < species.maxActiveAnimalsGlobally;

        public void NotifySpawned(WildlifeSpeciesProfile species)
        {
            if (species == null) return;
            activeCountsBySpecies.TryGetValue(species, out int c);
            activeCountsBySpecies[species] = c + 1;
        }

        public void NotifyDespawned(WildlifeSpeciesProfile species)
        {
            if (species == null) return;
            if (activeCountsBySpecies.TryGetValue(species, out int c))
                activeCountsBySpecies[species] = Mathf.Max(0, c - 1);
        }

        public bool IsNight => TimeSource != null && TimeSource.IsNight;
    }
}
