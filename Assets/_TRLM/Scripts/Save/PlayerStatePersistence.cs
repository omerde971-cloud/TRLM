using UnityEngine;
using TRLM.Player;
using TRLM.Survival;
using TRLM.Combat;
using TRLM.Inventory;
using TRLM.Equipment;

namespace TRLM.Save
{
    /// <summary>
    /// Owns capturing/restoring everything about the player: transform, health/survival, injuries,
    /// psychological state, inventory, equipment. Lives on PF_Player. SaveOrchestrator calls
    /// Capture()/Restore() and knows nothing about any of these subsystems' internals — this class
    /// is the one place that does.
    /// </summary>
    public class PlayerStatePersistence : MonoBehaviour
    {
        [SerializeField] private ItemDatabase itemDatabase;

        private CharacterController characterController;
        private FirstPersonController movement;
        private HealthSystem health;
        private HungerSystem hunger;
        private ThirstSystem thirst;
        private WetnessSystem wetness;
        private ColdExposureSystem cold;
        private RegionalInjurySystem injury;
        private PsychologicalState psych;
        private PlayerInventory inventory;
        private PlayerEquipment equipment;
        private FlashlightController flashlight;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            movement = GetComponent<FirstPersonController>();
            health = GetComponentInChildren<HealthSystem>();
            hunger = GetComponentInChildren<HungerSystem>();
            thirst = GetComponentInChildren<ThirstSystem>();
            wetness = GetComponentInChildren<WetnessSystem>();
            cold = GetComponentInChildren<ColdExposureSystem>();
            injury = GetComponentInChildren<RegionalInjurySystem>();
            psych = GetComponentInChildren<PsychologicalState>();
            inventory = GetComponentInChildren<PlayerInventory>();
            equipment = GetComponentInChildren<PlayerEquipment>();
            flashlight = GetComponentInChildren<FlashlightController>();
        }

        public PlayerStateData CapturePlayer()
        {
            var d = new PlayerStateData
            {
                posX = transform.position.x,
                posY = transform.position.y,
                posZ = transform.position.z,
                yawDegrees = transform.eulerAngles.y,
                health = health != null ? health.CurrentHealth : 100f,
                isDead = health != null && health.IsDead,
                hunger = hunger != null ? hunger.Hunger : 100f,
                thirst = thirst != null ? thirst.Thirst : 100f,
                wetness = wetness != null ? wetness.Wetness : 0f,
                bodyTemperature = cold != null ? cold.BodyTemperature : 100f,
                bleedSeverity = 0f,
                poisonSeverity = injury != null ? injury.PoisonSeverity : 0f,
                sanityStability = psych != null ? psych.Stability : 100f,
                flashlightBatteryPercent = flashlight != null ? flashlight.BatteryPercent : 100f,
            };

            if (injury != null)
            {
                foreach (var kv in injury.AllSeverities())
                {
                    if (kv.Value <= 0f) continue;
                    d.injuries.Add(new InjuryEntry { region = kv.Key, severity = kv.Value });
                }
                d.bleedSeverity = injury.IsBleeding ? 1f : 0f; // exact severity isn't exposed; treated/not-treated is what matters on restore
            }

            return d;
        }

        public void RestorePlayer(PlayerStateData d)
        {
            if (d == null) return;

            // Disable the CharacterController while teleporting — moving its Transform directly
            // while enabled can fight the controller's own collision resolution mid-frame.
            if (characterController != null) characterController.enabled = false;
            transform.position = new Vector3(d.posX, d.posY, d.posZ);
            transform.rotation = Quaternion.Euler(0f, d.yawDegrees, 0f);
            if (characterController != null) characterController.enabled = true;

            health?.RestoreState(d.health, d.isDead);
            hunger?.RestoreHunger(d.hunger);
            thirst?.RestoreThirst(d.thirst);
            wetness?.AddWetness(d.wetness); // starts at 0 on fresh scene load, additive-set is safe
            cold?.DebugSetBodyTemperature(d.bodyTemperature);
            psych?.DebugSetStability(d.sanityStability);

            if (injury != null)
            {
                foreach (var entry in d.injuries)
                    injury.RestoreInjury(entry.region, entry.severity);
                if (d.bleedSeverity > 0f) injury.ApplyBleeding(1f);
                if (d.poisonSeverity > 0f) injury.ApplyPoison(d.poisonSeverity);
            }

            flashlight?.RestoreBattery(d.flashlightBatteryPercent);
        }

        public InventoryData CaptureInventory()
        {
            var d = new InventoryData { selectedSlotIndex = inventory != null ? inventory.SelectedSlotIndex : 0 };
            if (inventory == null) return d;

            foreach (var slot in inventory.Slots)
                d.slots.Add(slot.IsEmpty ? new InventorySlotData() : new InventorySlotData { itemId = slot.item.itemId, count = slot.count });

            return d;
        }

        public void RestoreInventory(InventoryData d)
        {
            if (d == null || inventory == null) return;

            inventory.ClearAllSlots();
            for (int i = 0; i < d.slots.Count; i++)
            {
                var s = d.slots[i];
                if (string.IsNullOrEmpty(s.itemId)) continue;
                var item = itemDatabase != null ? itemDatabase.FindItem(s.itemId) : null;
                if (item == null) { Debug.LogWarning($"[PlayerStatePersistence] Unknown itemId '{s.itemId}' in save, skipping slot {i}."); continue; }
                inventory.RestoreSlot(i, item, s.count);
            }
            inventory.RestoreComplete(d.selectedSlotIndex);
        }

        public EquipmentData CaptureEquipment()
        {
            var d = new EquipmentData { activeSlotIndex = equipment != null && equipment.ActiveSlot.HasValue ? (int)equipment.ActiveSlot.Value : -1 };
            if (equipment == null) return d;

            d.sidearm = CaptureSlot(EquipmentSlotType.Sidearm);
            d.longGunA = CaptureSlot(EquipmentSlotType.LongGunA);
            d.longGunB = CaptureSlot(EquipmentSlotType.LongGunB);
            d.melee = CaptureSlot(EquipmentSlotType.Melee);
            return d;
        }

        private EquippedWeaponData CaptureSlot(EquipmentSlotType slot)
        {
            var def = equipment.GetEquipped(slot);
            if (def == null) return new EquippedWeaponData();

            var runtime = equipment.GetRuntimeState(slot);
            return new EquippedWeaponData { weaponId = def.weaponId, currentMagazine = runtime != null ? runtime.currentMagazine : def.magazineCapacity };
        }

        public void RestoreEquipment(EquipmentData d)
        {
            if (d == null || equipment == null || itemDatabase == null) return;

            RestoreSlot(d.sidearm);
            RestoreSlot(d.longGunA);
            RestoreSlot(d.longGunB);
            RestoreSlot(d.melee);

            if (d.activeSlotIndex >= 0)
                equipment.SetActive((EquipmentSlotType)d.activeSlotIndex);
        }

        private void RestoreSlot(EquippedWeaponData data)
        {
            if (data == null || string.IsNullOrEmpty(data.weaponId)) return;

            var def = itemDatabase.FindWeapon(data.weaponId);
            if (def == null) { Debug.LogWarning($"[PlayerStatePersistence] Unknown weaponId '{data.weaponId}' in save, skipping."); return; }

            equipment.TryEquip(def);
            var runtime = equipment.GetRuntimeState(def.category == WeaponCategory.Sidearm ? EquipmentSlotType.Sidearm
                : def.category == WeaponCategory.Melee ? EquipmentSlotType.Melee
                : equipment.GetEquipped(EquipmentSlotType.LongGunA) == def ? EquipmentSlotType.LongGunA : EquipmentSlotType.LongGunB);
            if (runtime != null) runtime.currentMagazine = data.currentMagazine;
        }
    }
}
