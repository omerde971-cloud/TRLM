using System;

namespace TRLM.Equipment
{
    /// <summary>
    /// Plain C# per-equipped-weapon runtime data (not a MonoBehaviour) — lives inside
    /// PlayerEquipment's per-slot data, one instance per equipped weapon. Deliberately built
    /// only from plain serializable fields (per the Sprint 07 save-hook design constraint): no
    /// Coroutine/Action references stored here, so this remains save-friendly even though no
    /// save system consumes it yet.
    /// </summary>
    [Serializable]
    public class WeaponRuntimeState
    {
        public WeaponDefinition definition;
        public int currentMagazine;
        public bool isReloading;
        public float nextFireTime; // Time.time-based fire-rate cooldown
    }
}
