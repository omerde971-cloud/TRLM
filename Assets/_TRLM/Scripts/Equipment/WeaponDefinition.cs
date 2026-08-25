using UnityEngine;
using TRLM.Inventory;

namespace TRLM.Equipment
{
    /// <summary>
    /// Data-only description of a firearm. WeaponController drives ANY equipped weapon purely
    /// from these fields plus a per-instance WeaponRuntimeState — no per-weapon subclassing.
    /// Instances live under Assets/_TRLM/ScriptableObjects/Weapons/.
    ///
    /// NOTE — zero weapon 3D assets exist in this project (confirmed empty
    /// Assets/ThirdParty/Weapons/, no AssetRegistry.md entries, standing zero-budget
    /// constraint). placeholderVisualPrefab is expected to be a simple primitive-geometry
    /// DEV_Placeholder_* prefab (see FirePoint.visualPlaceholder precedent), not real weapon
    /// art. If left unassigned, PlayerEquipment falls back to a bare scaled cube.
    /// </summary>
    [CreateAssetMenu(menuName = "TRLM/Equipment/Weapon Definition", fileName = "NewWeapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string weaponId;
        public string displayName;
        public WeaponCategory category;

        [Header("Ammo (real ItemDefinition, consumed from PlayerInventory)")]
        public ItemDefinition requiredAmmo;
        public int magazineCapacity = 8;

        [Header("Damage / Range")]
        public float damage = 25f;
        public float range = 50f;

        [Header("Pellets (1 = single precise shot, >1 = shotgun-style cone)")]
        [Tooltip("Capped at 10 by WeaponController regardless of this value.")]
        public int pelletCount = 1;
        public float spreadAngleDegrees = 0f;

        [Header("Firing")]
        public float fireRateSeconds = 0.4f; // minimum time between shots
        public bool isAutomatic = false;      // always false this sprint — semi-auto only

        [Header("Reload")]
        public float reloadSeconds = 1.8f;

        [Header("Recoil")]
        public float recoilPitch = 2.5f;
        public float recoilYawRandom = 1f;

        [Header("Sway")]
        public float baseSwayDegrees = 1.5f;

        [Header("Noise (Sprint 03 NoiseEvents.Raise loudness, meters)")]
        public float noiseLoudness = 80f;

        [Header("Visual (placeholder — see class remarks)")]
        public GameObject placeholderVisualPrefab;
    }
}
