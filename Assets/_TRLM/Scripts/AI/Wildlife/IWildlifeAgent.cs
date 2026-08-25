using UnityEngine;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Implemented by every spawnable animal brain (WolfAI, BearAI, PassiveWildlifeAI) so
    /// WildlifeSpawner can hand over the owning zone + species profile without a per-species
    /// GetComponent chain.
    /// </summary>
    public interface IWildlifeAgent
    {
        void Initialize(WildlifeSpawnZone owningZone, WildlifeSpeciesProfile owningSpecies);
    }
}
