using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TRLM.Player;
using TRLM.Survival;
using TRLM.Equipment;
using TRLM.Combat;

namespace TRLM.Inventory
{
    /// <summary>
    /// Fixed 10-slot inventory. Lives on PF_Player. Other systems (PickupItem, LootSpawnPoint,
    /// FirePoint, Hunger/Thirst "use item" calls) go through TryAddItem/TryRemoveItem/HasItem;
    /// nothing reaches into Slots to mutate it directly.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        private const int SlotCount = 10;

        [Header("Drop")]
        [SerializeField] private GameObject dropPickupPrefab; // simple placeholder visual, see PickupItem
        [SerializeField] private float dropForwardOffset = 1f;

        [Header("Use (Sprint 06)")]
        [SerializeField] private float foodRestoreAmount = 30f;
        [SerializeField] private float waterRestoreAmount = 30f;
        [SerializeField] private float medicineHealAmount = 25f;

        [Header("Use (Sprint 07 A2 — injury/bandage)")]
        [SerializeField] private float medicineInjurySeverityReduction = 2f; // modest, not a full cure
        [SerializeField] private float medicinePoisonSeverityReduction = 2f;
        [SerializeField] private float bandageUseSeconds = 2f; // ANIMATION_PLACEHOLDER — no bandaging animation yet
        [SerializeField] private AudioSource inventoryAudioSource;
        [SerializeField] private AudioClip backpackRustleClip;
        [SerializeField] private AudioClip bandageHandleClip;

        private readonly InventorySlot[] slots = new InventorySlot[SlotCount];

        public event Action OnInventoryChanged;

        public IReadOnlyList<InventorySlot> Slots => slots;

        /// <summary>Currently selected hotbar slot — used by Drop (G) and Use (left click, while
        /// the inventory panel is open — see GameplayHUD).</summary>
        public int SelectedSlotIndex { get; private set; }

        private PlayerInputHandler input;
        private HungerSystem hunger;
        private ThirstSystem thirst;
        private HealthSystem health;
        private FlashlightController flashlight;
        private RegionalInjurySystem regionalInjury;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            hunger = GetComponentInChildren<HungerSystem>();
            thirst = GetComponentInChildren<ThirstSystem>();
            health = GetComponentInChildren<HealthSystem>();
            flashlight = GetComponentInChildren<FlashlightController>();
            regionalInjury = GetComponentInChildren<RegionalInjurySystem>();
            if (inventoryAudioSource == null) inventoryAudioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (input != null) input.DropPressed += HandleDropPressed;
        }

        private void OnDisable()
        {
            if (input != null) input.DropPressed -= HandleDropPressed;
        }

        private void Update()
        {
            // Slot-cycle keybind: [ and ] cycle the selected hotbar slot. PlayerInputHandler has
            // no hotbar-select action, so — following the same documented, scoped exception
            // CompanionCommandInput uses for its 1/2/3 bindings (which already occupy the number
            // row, ruling that reuse out here) — this reads Keyboard.current directly rather than
            // adding a whole new input-action pair for a vertical-slice-only feature.
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rightBracketKey.wasPressedThisFrame) SelectSlot(SelectedSlotIndex + 1);
            else if (kb.leftBracketKey.wasPressedThisFrame) SelectSlot(SelectedSlotIndex - 1);
        }

        public void SelectSlot(int index)
        {
            int wrapped = ((index % SlotCount) + SlotCount) % SlotCount;
            if (wrapped == SelectedSlotIndex) return;
            SelectedSlotIndex = wrapped;
            PlayInventoryClip(backpackRustleClip, 0.35f);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>Uses the item in the currently selected slot based on its category, consuming
        /// one unit on success. Returns false if the slot is empty or the category isn't usable
        /// this way (Tool/SpecialObject/Wood have no "use" effect here).</summary>
        public bool UseSelectedItem()
        {
            var slot = slots[SelectedSlotIndex];
            if (slot.IsEmpty) return false;

            ItemDefinition item = slot.item;
            bool used;
            switch (item.category)
            {
                case ItemCategory.SurvivalResource:
                    // Water vs. food aren't separate categories (both are SurvivalResource) —
                    // distinguished here by itemId, a documented simplification rather than
                    // adding a new enum value this sprint.
                    if (item.itemId != null && item.itemId.Contains("water"))
                        used = TryUse(() => thirst?.Drink(waterRestoreAmount));
                    else
                        used = TryUse(() => hunger?.Eat(foodRestoreAmount));
                    break;
                case ItemCategory.Medicine:
                    // Sprint 07 (A2, Section 24) — medicine also nudges injury/poison severity
                    // down modestly alongside the heal. Deliberately NOT a full instant cure of
                    // fractures/poison/trauma (see RegionalInjurySystem.ReduceAllInjurySeverity's
                    // doc comment) — TraumaArm/TraumaLeg still run out their own timer.
                    used = TryUse(() =>
                    {
                        health?.Heal(medicineHealAmount);
                        regionalInjury?.ReduceAllInjurySeverity(medicineInjurySeverityReduction);
                        regionalInjury?.ReducePoisonSeverity(medicinePoisonSeverityReduction);
                    });
                    break;
                case ItemCategory.Bandage:
                    // Section 23 — consumes on use, but the bleeding-stop effect lands after a short
                    // coroutine delay (ANIMATION_PLACEHOLDER — no real bandaging animation exists).
                    used = TryUse(() =>
                    {
                        PlayInventoryClip(bandageHandleClip, 0.55f);
                        StartCoroutine(BandageRoutine());
                    });
                    break;
                case ItemCategory.Battery:
                    // FlashlightController.TryReplaceBattery already removes the item itself —
                    // don't double-consume via TryRemoveItem below.
                    return flashlight != null && flashlight.TryReplaceBattery();
                default:
                    return false;
            }

            if (!used) return false;
            TryRemoveItem(item, 1);
            return true;
        }

        private static bool TryUse(Action apply)
        {
            apply?.Invoke();
            return true;
        }

        // ANIMATION_PLACEHOLDER — no real bandaging animation exists; a short timed delay stands
        // in for it, matching FirePoint/SleepInteraction's existing coroutine-based-timed-action
        // precedent.
        private IEnumerator BandageRoutine()
        {
            yield return new WaitForSeconds(bandageUseSeconds);
            regionalInjury?.TreatBleeding();
        }

        // Minimal binding until a real inventory UI exists: G drops the selected slot.
        private void HandleDropPressed() => DropSlot(SelectedSlotIndex);

        public bool HasItem(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return false;
            return CountOf(item) >= count;
        }

        /// <summary>Cheap check so IInteractable.InteractionPrompt can say "Inventory Full" without mutating state.</summary>
        public bool HasRoomFor(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return true;

            int remaining = count;
            if (item.stackable)
            {
                foreach (var slot in slots)
                {
                    if (slot.item != item) continue;
                    remaining -= Mathf.Max(0, item.maxStack - slot.count);
                    if (remaining <= 0) return true;
                }
            }

            foreach (var slot in slots)
            {
                if (!slot.IsEmpty) continue;
                remaining -= item.stackable ? item.maxStack : 1;
                if (remaining <= 0) return true;
            }

            return remaining <= 0;
        }

        public bool TryAddItem(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return false;

            int remaining = count;

            if (item.stackable)
            {
                for (int i = 0; i < slots.Length && remaining > 0; i++)
                {
                    if (slots[i].item != item) continue;
                    int space = item.maxStack - slots[i].count;
                    if (space <= 0) continue;
                    int add = Mathf.Min(space, remaining);
                    slots[i].count += add;
                    remaining -= add;
                }
            }

            while (remaining > 0)
            {
                int emptyIndex = FirstEmptySlot();
                if (emptyIndex < 0) break; // no room left, caller finds out via the false return below

                int add = item.stackable ? Mathf.Min(item.maxStack, remaining) : 1;
                slots[emptyIndex] = new InventorySlot { item = item, count = add };
                remaining -= add;
            }

            if (remaining > 0)
            {
                // Partial add would silently lose items if we kept it, so roll back entirely.
                // Simplest correct behavior per spec: full-or-nothing, no exceptions.
                RemoveUpTo(item, count - remaining);
                return false;
            }

            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool TryRemoveItem(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return false;
            if (CountOf(item) < count) return false;

            RemoveUpTo(item, count);
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>Drops one unit from the given slot into the world at the player's feet. No-op if the slot is empty.</summary>
        public void DropSlot(int index)
        {
            if (index < 0 || index >= slots.Length || slots[index].IsEmpty) return;

            ItemDefinition item = slots[index].item;
            slots[index].count--;
            if (slots[index].count <= 0) slots[index] = default;
            OnInventoryChanged?.Invoke();

            SpawnWorldPickup(item);
            PlayInventoryClip(backpackRustleClip, 0.4f);
        }

        private void PlayInventoryClip(AudioClip clip, float volume)
        {
            if (inventoryAudioSource != null && clip != null)
                inventoryAudioSource.PlayOneShot(clip, volume);
        }

        private void SpawnWorldPickup(ItemDefinition item)
        {
            Vector3 pos = transform.position + transform.forward * dropForwardOffset;

            GameObject go = dropPickupPrefab != null
                ? Instantiate(dropPickupPrefab, pos, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Cube); // future polish: real drop-item visual/prefab

            if (dropPickupPrefab == null)
            {
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.25f;
                go.name = "Dropped_" + item.itemId;
            }

            var pickup = go.GetComponent<PickupItem>();
            if (pickup == null) pickup = go.AddComponent<PickupItem>();
            pickup.Configure(item, 1);
        }

        /// <summary>Save/load restore only. Direct slot writes (bypassing TryAddItem's stacking
        /// search) so restore reproduces the exact saved layout instead of a re-packed one, and a
        /// single OnInventoryChanged at the end instead of one per slot.</summary>
        public void ClearAllSlots()
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = default;
        }

        public void RestoreSlot(int index, ItemDefinition item, int count)
        {
            if (index < 0 || index >= slots.Length) return;
            slots[index] = item != null && count > 0 ? new InventorySlot { item = item, count = count } : default;
        }

        public void RestoreComplete(int selectedSlotIndex)
        {
            SelectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, slots.Length - 1);
            OnInventoryChanged?.Invoke();
        }

        private int FirstEmptySlot()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].IsEmpty) return i;
            return -1;
        }

        private int CountOf(ItemDefinition item)
        {
            int total = 0;
            foreach (var slot in slots)
                if (slot.item == item) total += slot.count;
            return total;
        }

        private void RemoveUpTo(ItemDefinition item, int count)
        {
            int remaining = count;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].item != item) continue;
                int take = Mathf.Min(slots[i].count, remaining);
                slots[i].count -= take;
                remaining -= take;
                if (slots[i].count <= 0) slots[i] = default;
            }
        }
    }
}
