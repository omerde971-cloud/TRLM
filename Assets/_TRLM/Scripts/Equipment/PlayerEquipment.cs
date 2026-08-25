using System;
using System.Collections.Generic;
using UnityEngine;

namespace TRLM.Equipment
{
    [Serializable]
    public class EquippedWeapon
    {
        public WeaponDefinition definition;
        public WeaponRuntimeState runtimeState;
        public GameObject visualInstance;
    }

    /// <summary>
    /// Physical weapon-holding slots on the player — distinct from PlayerInventory's 10-slot
    /// carried-items inventory (that's for consumables/ammo/loot). Sidearm/LongGunA/LongGunB/
    /// Melee each hold at most one WeaponDefinition + its own runtime ammo/reload state. Only
    /// one slot is "active" (drawn/usable by WeaponController) at a time; equipped-but-inactive
    /// slots stay mechanically equipped (ammo keeps its state) and holstered at their mount
    /// Transform visually. The Melee slot's container lives here so the equipment architecture
    /// is complete; Sub-Agent A2 (melee/injury) populates its content — this agent does not
    /// build melee weapons or block on them.
    ///
    /// ANIMATION_PLACEHOLDER: HipMount/BackLeftMount/BackRightMount/MeleeMount are empty
    /// Transforms with approximate, not anatomically-fitted, offsets — there is no real
    /// character mesh to align sockets to yet. Mechanical slot behavior (what's equipped, its
    /// ammo) is authoritative; the holstered visual is a secondary, honestly-limited stand-in.
    /// </summary>
    public class PlayerEquipment : MonoBehaviour
    {
        [Header("Mount Points (ANIMATION_PLACEHOLDER — see class remarks)")]
        [SerializeField] private Transform hipMount;
        [SerializeField] private Transform backLeftMount;
        [SerializeField] private Transform backRightMount;
        [SerializeField] private Transform meleeMount;

        private readonly Dictionary<EquipmentSlotType, EquippedWeapon> slots = new Dictionary<EquipmentSlotType, EquippedWeapon>();

        public event Action OnEquipmentChanged;

        public EquipmentSlotType? ActiveSlot { get; private set; }

        private void Awake()
        {
            EnsureMounts();
        }

        private void EnsureMounts()
        {
            // Auto-creates missing mounts so the system is usable even in ad-hoc/test setups
            // that haven't wired mount Transforms in the prefab yet.
            if (hipMount == null) hipMount = CreateMount("HipMount", new Vector3(0.3f, -0.3f, 0.15f));
            if (backLeftMount == null) backLeftMount = CreateMount("BackLeftMount", new Vector3(-0.25f, 0.25f, -0.2f));
            if (backRightMount == null) backRightMount = CreateMount("BackRightMount", new Vector3(0.25f, 0.25f, -0.2f));
            if (meleeMount == null) meleeMount = CreateMount("MeleeMount", new Vector3(-0.3f, -0.25f, 0.15f));
        }

        private Transform CreateMount(string mountName, Vector3 localPos)
        {
            var go = new GameObject(mountName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private Transform MountFor(EquipmentSlotType slot)
        {
            switch (slot)
            {
                case EquipmentSlotType.Sidearm: return hipMount;
                case EquipmentSlotType.LongGunA: return backLeftMount;
                case EquipmentSlotType.LongGunB: return backRightMount;
                case EquipmentSlotType.Melee: return meleeMount;
                default: return transform;
            }
        }

        /// <summary>Auto-picks the correct slot type from the definition's category and equips
        /// it there, creating/replacing the runtime state and holstered visual. Returns false
        /// only if def is null (there is always some slot for a valid category).</summary>
        public bool TryEquip(WeaponDefinition def)
        {
            if (def == null) return false;

            EquipmentSlotType slot = PickSlotFor(def.category);
            var equipped = new EquippedWeapon
            {
                definition = def,
                runtimeState = new WeaponRuntimeState { definition = def, currentMagazine = def.magazineCapacity }
            };

            if (slots.TryGetValue(slot, out var previous) && previous.visualInstance != null)
                Destroy(previous.visualInstance);

            SpawnVisual(equipped, slot);
            slots[slot] = equipped;

            if (ActiveSlot == null)
                SetActive(slot);

            OnEquipmentChanged?.Invoke();
            return true;
        }

        private EquipmentSlotType PickSlotFor(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Sidearm:
                    return EquipmentSlotType.Sidearm;
                case WeaponCategory.LongGun:
                    if (!slots.ContainsKey(EquipmentSlotType.LongGunA)) return EquipmentSlotType.LongGunA;
                    if (!slots.ContainsKey(EquipmentSlotType.LongGunB)) return EquipmentSlotType.LongGunB;
                    return EquipmentSlotType.LongGunA; // both full: replace A — simple, documented behavior
                case WeaponCategory.Melee:
                default:
                    return EquipmentSlotType.Melee;
            }
        }

        public void Unequip(EquipmentSlotType slot)
        {
            if (!slots.TryGetValue(slot, out var equipped)) return;

            if (equipped.visualInstance != null) Destroy(equipped.visualInstance);
            slots.Remove(slot);
            if (ActiveSlot == slot) ActiveSlot = null;

            OnEquipmentChanged?.Invoke();
        }

        public WeaponDefinition GetEquipped(EquipmentSlotType slot) =>
            slots.TryGetValue(slot, out var e) ? e.definition : null;

        public WeaponRuntimeState GetRuntimeState(EquipmentSlotType slot) =>
            slots.TryGetValue(slot, out var e) ? e.runtimeState : null;

        public bool IsSlotFilled(EquipmentSlotType slot) => slots.ContainsKey(slot);

        /// <summary>Draws the given slot's weapon, or empty hands when slot is null. Returns
        /// false (no-op) if asking to draw a slot that has nothing equipped.</summary>
        public bool SetActive(EquipmentSlotType? slot)
        {
            if (slot.HasValue && !slots.ContainsKey(slot.Value)) return false;

            ActiveSlot = slot;
            OnEquipmentChanged?.Invoke();
            return true;
        }

        public WeaponDefinition GetActiveDefinition() => ActiveSlot.HasValue ? GetEquipped(ActiveSlot.Value) : null;
        public WeaponRuntimeState GetActiveRuntimeState() => ActiveSlot.HasValue ? GetRuntimeState(ActiveSlot.Value) : null;

        private void SpawnVisual(EquippedWeapon equipped, EquipmentSlotType slot)
        {
            Transform mount = MountFor(slot);
            if (mount == null) return;

            GameObject visual;
            if (equipped.definition.placeholderVisualPrefab != null)
            {
                visual = Instantiate(equipped.definition.placeholderVisualPrefab, mount);
            }
            else
            {
                // No weapon 3D assets exist in this project — bare primitive fallback so
                // equip/unequip is at least visually observable without a designed placeholder
                // prefab assigned on the WeaponDefinition. See WeaponDefinition remarks.
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "DEV_Placeholder_" + (string.IsNullOrEmpty(equipped.definition.weaponId) ? "Weapon" : equipped.definition.weaponId);
                visual.transform.SetParent(mount, false);
                visual.transform.localScale = new Vector3(0.08f, 0.08f, 0.3f);
                var col = visual.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            equipped.visualInstance = visual;
        }
    }
}
