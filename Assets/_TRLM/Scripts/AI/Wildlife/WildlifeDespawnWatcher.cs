using UnityEngine;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Attached to every spawned animal. Removes it once the player has been far away for
    /// a while, so distant zones don't accumulate wildlife forever while the player is
    /// elsewhere on the island.
    /// </summary>
    public class WildlifeDespawnWatcher : MonoBehaviour
    {
        private WildlifeSpawnZone zone;
        private WildlifeSpeciesProfile species;
        private float farTimer;
        private const float FarGraceSeconds = 20f;
        private const float CheckInterval = 3f;

        public void Initialize(WildlifeSpawnZone owningZone, WildlifeSpeciesProfile owningSpecies)
        {
            zone = owningZone;
            species = owningSpecies;
            InvokeRepeating(nameof(CheckDistance), CheckInterval, CheckInterval);
        }

        private void CheckDistance()
        {
            var manager = WildlifeSpawnManager.Instance;
            if (manager == null || manager.Player == null || species == null) return;

            float dist = Vector3.Distance(transform.position, manager.Player.position);
            if (dist > species.despawnDistance)
            {
                farTimer += CheckInterval;
                if (farTimer >= FarGraceSeconds) Despawn();
            }
            else
            {
                farTimer = 0f;
            }
        }

        private void Despawn() => Destroy(gameObject);

        private void OnDestroy()
        {
            // Single cleanup point — covers distance despawn, death, or manual removal alike.
            zone?.UnregisterAnimal(gameObject);
            WildlifeSpawnManager.Instance?.NotifyDespawned(species);
        }
    }
}
