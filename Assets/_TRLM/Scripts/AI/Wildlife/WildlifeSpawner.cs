using UnityEngine;
using UnityEngine.AI;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Timed spawn decision for one WildlifeSpawnZone. Separate from the zone itself so
    /// the "when/whether to spawn" logic (cooldowns, player distance, day/night, global
    /// cap) doesn't clutter the zone's plain territory data.
    /// </summary>
    [RequireComponent(typeof(WildlifeSpawnZone))]
    public class WildlifeSpawner : MonoBehaviour
    {
        [SerializeField] private float checkIntervalSeconds = 5f;

        private WildlifeSpawnZone zone;
        private float cooldownRemaining;

        private void Awake() => zone = GetComponent<WildlifeSpawnZone>();

        private void Start()
        {
            var species = zone.Species;
            if (species == null || species.animalPrefab == null)
            {
                Debug.LogWarning($"[WildlifeSpawner] {name} has no species/prefab assigned — disabled.");
                enabled = false;
                return;
            }

            // Seed initial population immediately so zones aren't empty at scene start.
            // Sprint 2: respect the global species cap during seeding too — with four wolf
            // zones the unchecked seed could overshoot maxActiveAnimalsGlobally at startup.
            int initial = Random.Range(species.minPopulation, species.maxPopulation + 1);
            for (int i = 0; i < initial; i++)
            {
                var manager = WildlifeSpawnManager.Instance;
                if (manager != null && !manager.CanSpawnGlobally(species)) break;
                TrySpawnOne();
            }

            InvokeRepeating(nameof(EvaluateSpawn), checkIntervalSeconds, checkIntervalSeconds);
        }

        private void EvaluateSpawn()
        {
            var species = zone.Species;
            var manager = WildlifeSpawnManager.Instance;
            if (species == null || manager == null) return;

            if (cooldownRemaining > 0f) { cooldownRemaining -= checkIntervalSeconds; return; }
            if (!zone.CanSpawnMore()) return;
            if (!manager.CanSpawnGlobally(species)) return;

            float activityMultiplier = manager.IsNight ? species.nightActivityMultiplier : species.dayActivityMultiplier;
            float chance = Mathf.Clamp01(species.spawnChance * activityMultiplier);
            if (Random.value > chance) return;

            if (TrySpawnOne())
                cooldownRemaining = species.spawnCooldownSeconds;
        }

        private bool TrySpawnOne()
        {
            var species = zone.Species;
            var manager = WildlifeSpawnManager.Instance;

            Vector3 point = zone.GetRandomPointInZone();
            if (manager != null && manager.Player != null)
            {
                if (Vector3.Distance(point, manager.Player.position) < species.minDistanceFromPlayer)
                    return false; // never spawn on top of the player
            }

            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                return false; // no valid ground nearby (cliff/water/etc.) — skip this attempt

            var instance = Instantiate(species.animalPrefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            instance.name = species.speciesName + "_" + zone.name;

            // Species-agnostic: every animal brain (WolfAI/BearAI/PassiveWildlifeAI) implements
            // IWildlifeAgent, so new species never need another branch here.
            foreach (var agentBrain in instance.GetComponents<IWildlifeAgent>())
                agentBrain.Initialize(zone, species);

            zone.RegisterAnimal(instance);
            manager?.NotifySpawned(species);

            var despawnWatcher = instance.AddComponent<WildlifeDespawnWatcher>();
            despawnWatcher.Initialize(zone, species);

            return true;
        }
    }
}
