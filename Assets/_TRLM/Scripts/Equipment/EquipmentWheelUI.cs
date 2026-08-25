using UnityEngine;
using UnityEngine.InputSystem;
using TRLM.Player;

namespace TRLM.Equipment
{
    /// <summary>
    /// OnGUI-based equipment wheel, opened by holding Tab (PlayerInputHandler.EquipmentWheelHeld
    /// — polled, same pattern as other polled bools in the codebase). While open, sets
    /// Time.timeScale = 0f to pause wildlife AI, companion AI, world time, and survival ticks
    /// WITHOUT touching WolfAI.cs / CompanionAI.cs / DayNightSystem.cs — all of those key off
    /// Time.deltaTime / Time.time, so a global timescale pause satisfies the brief's pause
    /// requirement with zero changes to any of those files. OnGUI and Mouse.current input both
    /// keep working under timeScale = 0, so the wheel itself stays responsive while paused.
    ///
    /// Mouse position is read directly via UnityEngine.InputSystem.Mouse.current.position — a
    /// scoped, documented exception for wheel-selection purposes only, matching the precedent
    /// already set by CompanionCommandInput's number-key bindings and PlayerInventory's
    /// [ ]-bracket slot cycling (PlayerInputHandler exposes no absolute-screen-position action).
    /// </summary>
    public class EquipmentWheelUI : MonoBehaviour
    {
        private enum Category { EmptyHands, Sidearm, LongGunA, LongGunB, Melee, Flashlight, SymbolBook }

        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private FlashlightController flashlight;

        private bool wasHeldLastFrame;
        private float previousTimeScale = 1f;
        private Category hoveredCategory;

        public bool IsOpen { get; private set; }

        private void Update()
        {
            if (input == null) return;

            bool held = input.EquipmentWheelHeld;
            if (held && !wasHeldLastFrame) Open();
            else if (!held && wasHeldLastFrame) Close(applySelection: true);

            if (IsOpen) UpdateHoveredCategory();

            wasHeldLastFrame = held;
        }

        private void Open()
        {
            IsOpen = true;
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Close(bool applySelection)
        {
            IsOpen = false;
            Time.timeScale = previousTimeScale;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (applySelection) ApplyCategory(hoveredCategory);
        }

        private void UpdateHoveredCategory()
        {
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = mouse - center;

            if (dir.sqrMagnitude < 400f) // dead zone at the very center = Empty Hands
            {
                hoveredCategory = Category.EmptyHands;
                return;
            }

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            int wedge = Mathf.FloorToInt(angle / 60f) % 6; // 6 wedges, 60 degrees each
            Category candidate = wedge switch
            {
                0 => Category.Sidearm,
                1 => Category.LongGunA,
                2 => Category.LongGunB,
                3 => Category.Melee,
                4 => Category.Flashlight,
                _ => Category.SymbolBook,
            };

            hoveredCategory = IsEnabled(candidate) ? candidate : Category.EmptyHands;
        }

        private bool IsEnabled(Category category)
        {
            switch (category)
            {
                case Category.EmptyHands: return true;
                case Category.Sidearm: return equipment != null && equipment.IsSlotFilled(EquipmentSlotType.Sidearm);
                case Category.LongGunA: return equipment != null && equipment.IsSlotFilled(EquipmentSlotType.LongGunA);
                case Category.LongGunB: return equipment != null && equipment.IsSlotFilled(EquipmentSlotType.LongGunB);
                case Category.Melee: return equipment != null && equipment.IsSlotFilled(EquipmentSlotType.Melee);
                case Category.Flashlight: return flashlight != null;
                case Category.SymbolBook: return false; // reserved slot, not implemented this sprint
                default: return false;
            }
        }

        private void ApplyCategory(Category category)
        {
            switch (category)
            {
                case Category.EmptyHands: equipment?.SetActive(null); break;
                case Category.Sidearm: equipment?.SetActive(EquipmentSlotType.Sidearm); break;
                case Category.LongGunA: equipment?.SetActive(EquipmentSlotType.LongGunA); break;
                case Category.LongGunB: equipment?.SetActive(EquipmentSlotType.LongGunB); break;
                case Category.Melee: equipment?.SetActive(EquipmentSlotType.Melee); break;
                case Category.Flashlight:
                    // Delegates to FlashlightController's existing private toggle handler via
                    // SendMessage (Unity resolves SendMessage against private methods too) rather
                    // than duplicating on/off logic here or editing FlashlightController.cs,
                    // which this sprint's brief marks off-limits.
                    if (flashlight != null)
                        flashlight.SendMessage("HandleFlashlightPressed", SendMessageOptions.DontRequireReceiver);
                    break;
                case Category.SymbolBook:
                    break; // reserved, no content this sprint
            }
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            GUI.Box(new Rect(Screen.width * 0.5f - 220, Screen.height * 0.5f - 220, 440, 440), "Equipment Wheel");

            DrawCategoryLabel(Category.Sidearm, "Sidearm", 0);
            DrawCategoryLabel(Category.LongGunA, "Long Gun A", 1);
            DrawCategoryLabel(Category.LongGunB, "Long Gun B", 2);
            DrawCategoryLabel(Category.Melee, "Melee", 3);
            DrawCategoryLabel(Category.Flashlight, "Flashlight", 4);
            DrawCategoryLabel(Category.SymbolBook, "Symbol Book", 5);

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            GUI.Label(new Rect(Screen.width * 0.5f - 100, Screen.height * 0.5f - 12, 200, 24), $"Selecting: {hoveredCategory}", style);
        }

        private void DrawCategoryLabel(Category category, string label, int wedgeIndex)
        {
            float angleDeg = wedgeIndex * 60f + 30f;
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 150f;

            bool enabled = IsEnabled(category);
            bool isHovered = hoveredCategory == category;

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = isHovered ? 16 : 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = enabled ? (isHovered ? Color.yellow : Color.white) : Color.gray }
            };

            GUI.Box(new Rect(pos.x - 60, pos.y - 16, 120, 32), label, style);
        }
    }
}
