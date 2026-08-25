using UnityEngine;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Data-only species definition. One asset per species (Wolf, Bear, Boar, Snake,
    /// MountainGoat); zones reference a profile instead of duplicating these numbers.
    /// </summary>
    [CreateAssetMenu(fileName = "SpeciesProfile_", menuName = "TRLM/Wildlife/Species Profile")]
    public class WildlifeSpeciesProfile : ScriptableObject
    {
        [Header("Identity")]
        public string speciesName = "Wolf";
        public GameObject animalPrefab;

        [Header("Population")]
        public int minPopulation = 1;
        public int maxPopulation = 3;
        [Range(0f, 1f)] public float spawnChance = 0.6f;
        public float spawnCooldownSeconds = 30f;
        public float respawnDelaySeconds = 300f;
        public int maxActiveAnimalsGlobally = 12;

        [Header("Player Safety")]
        public float minDistanceFromPlayer = 25f;
        public float despawnDistance = 120f;

        [Header("Behavior")]
        public float preferredPatrolRadius = 25f;
        [Range(0f, 1f)] public float aggressionModifier = 0.5f;

        [Header("Time / Weather Modifiers")]
        [Tooltip("Activity/spawn-chance multiplier while it's day.")]
        public float dayActivityMultiplier = 1f;
        [Tooltip("Activity/spawn-chance multiplier while it's night.")]
        public float nightActivityMultiplier = 1f;
        [Tooltip("Multiplier applied when a future weather system reports rain.")]
        public float rainModifier = 1f;
    }
}
