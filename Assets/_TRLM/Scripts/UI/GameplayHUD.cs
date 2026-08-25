using UnityEngine;
using TRLM.Player;
using TRLM.Survival;
using TRLM.Inventory;
using TRLM.Equipment;
using TRLM.Progression;
using TRLM.Combat;

namespace TRLM.UI
{
    /// <summary>
    /// Minimal always-on gameplay HUD, OnGUI-based to match DebugHUD's style. Shows survival
    /// stats only when they're worth showing (not full / below a warning threshold), a toggled
    /// inventory list, and short-lived objective notifications. Interaction prompting is already
    /// owned by InteractionPromptUI — this class does not duplicate it.
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HealthSystem health;
        [SerializeField] private StaminaSystem stamina;
        [SerializeField] private HungerSystem hunger;
        [SerializeField] private ThirstSystem thirst;
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private ObjectiveSystem objectiveSystem;
        [SerializeField] private TRLM.Equipment.PlayerEquipment playerEquipment; // Sprint 07 — ammo readout only
        [SerializeField] private RegionalInjurySystem injurySystem; // Sprint 07 (A2) — injury/bleeding readout
        [SerializeField] private StatusEffectController statusEffects;

        [Header("Thresholds")]
        [SerializeField] private float hungerThirstWarning = 40f;
        [SerializeField] private float notificationSeconds = 4f;

        private bool inventoryOpen;
        private string notificationText;
        private float notificationTimer;

        // Per-frame string caches (Sprint 2 GC pass): OnGUI runs at least twice a frame and the
        // interpolated captions below were allocating every pass. Rebuild only on value change.
        private int cachedHp = int.MinValue;
        private string hpCaption = "";
        private int cachedSta = int.MinValue;
        private string staCaption = "";
        private int cachedBattery = int.MinValue;
        private string batteryCaption = "";
        private int cachedMag = int.MinValue;
        private bool cachedReloading;
        private string ammoCaption = "";
        private GUIStyle labelStyle;
        private GUIStyle barLabelStyle;
        private GUIStyle bleedStyle;
        private GUIStyle notificationStyle;
        private GUIStyle objectiveStyle; // Sprint 3 — small persistent current-objective line
        private Texture2D whiteTex;

        // Muted survival palette — desaturated so the HUD reads as instrumentation, not arcade UI.
        private static readonly Color BarBackplate = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color BarTrack = new Color(0.12f, 0.13f, 0.14f, 0.85f);
        private static readonly Color HealthColor = new Color(0.62f, 0.22f, 0.18f, 0.95f);
        private static readonly Color StaminaColor = new Color(0.72f, 0.66f, 0.45f, 0.95f);
        private static readonly Color HungerColor = new Color(0.58f, 0.44f, 0.26f, 0.95f);
        private static readonly Color ThirstColor = new Color(0.30f, 0.46f, 0.56f, 0.95f);

        private void OnEnable()
        {
            if (input != null)
            {
                input.InventoryPressed += ToggleInventory;
                // "Use" is FirePressed (left mouse) reused ONLY while the inventory panel is
                // open — gated by inventoryOpen below so it never conflicts with a future actual
                // weapon-fire use of the same event; documented per sprint instructions rather
                // than adding a new raw keybind.
                input.FirePressed += HandleUsePressed;
            }
            if (objectiveSystem != null) objectiveSystem.OnObjectiveChanged += HandleObjectiveChanged;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.InventoryPressed -= ToggleInventory;
                input.FirePressed -= HandleUsePressed;
            }
            if (objectiveSystem != null) objectiveSystem.OnObjectiveChanged -= HandleObjectiveChanged;
        }

        /// <summary>Sprint 07 — lets WeaponController gate weapon-fire input off while the
        /// inventory panel is open, so LMB doesn't fire a weapon and use an inventory item in
        /// the same click.</summary>
        public bool InventoryOpen => inventoryOpen;

        private void ToggleInventory() => inventoryOpen = !inventoryOpen;

        private void HandleUsePressed()
        {
            if (!inventoryOpen || inventory == null) return;
            inventory.UseSelectedItem();
        }

        private void HandleObjectiveChanged(ObjectiveStep step)
        {
            notificationText = $"New objective: {ObjectiveLabel(step)}";
            notificationTimer = notificationSeconds;
        }

        // Sprint 3 — short player-facing objective text for the persistent HUD line (UI is English).
        private static string ObjectiveLabel(ObjectiveStep step)
        {
            switch (step)
            {
                case ObjectiveStep.PreparationComplete: return "Prepare to leave";
                case ObjectiveStep.RowToIsland: return "Row to the island";
                case ObjectiveStep.ReachLandingZone: return "Reach the shore";
                case ObjectiveStep.EnterCoastalForest: return "Enter the coastal forest";
                case ObjectiveStep.ReachAbandonedHouse: return "Find the abandoned house";
                case ObjectiveStep.SearchHouse: return "Search the house";
                case ObjectiveStep.AcquireEssentialLoot: return "Gather food and water";
                case ObjectiveStep.NightBegins: return "Survive the night";
                case ObjectiveStep.WolfThreat: return "A wolf is near — stay alert";
                case ObjectiveStep.ReachSafeHouse: return "Reach the safe house";
                case ObjectiveStep.LightFire: return "Light the fire";
                case ObjectiveStep.Sleep: return "Rest until morning";
                case ObjectiveStep.WakeNextMorning: return "A new day begins";
                case ObjectiveStep.SliceComplete: return "Head for the mountain";
                case ObjectiveStep.ReachCaveEntrance: return "Reach the cave entrance";
                case ObjectiveStep.EnterCave: return "Enter the cave";
                case ObjectiveStep.RecoverFirstProphecyPage: return "Search the cave for the prophecy";
                case ObjectiveStep.CaveThresholdComplete: return "Press deeper into the mountain";
                default: return step.ToString();
            }
        }

        private void Update()
        {
            if (notificationTimer > 0f) notificationTimer -= Time.deltaTime;
        }

        /// <summary>Draws one instrumentation bar: dark track, muted fill, small caption on top.</summary>
        private void DrawBar(float x, float y, float width, float height, float fraction, Color fill, string caption)
        {
            GUI.color = BarTrack;
            GUI.DrawTexture(new Rect(x, y, width, height), whiteTex);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(x + 1, y + 1, (width - 2) * Mathf.Clamp01(fraction), height - 2), whiteTex);
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(caption))
                GUI.Label(new Rect(x + 4, y - 1, width - 8, height + 2), caption, barLabelStyle);
        }

        private void OnGUI()
        {
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            barLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.92f, 0.90f, 0.95f) }
            };
            if (whiteTex == null)
            {
                whiteTex = new Texture2D(1, 1);
                whiteTex.SetPixel(0, 0, Color.white);
                whiteTex.Apply();
            }

            // ---- Sprint 3: small persistent current-objective line, top-left --------------------
            if (objectiveSystem != null)
            {
                objectiveStyle ??= new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.87f, 0.83f, 0.68f, 0.95f) }
                };
                string objLine = "Objective:  " + ObjectiveLabel(objectiveSystem.Current);
                float ow = Mathf.Min(objectiveStyle.CalcSize(new GUIContent(objLine)).x + 20f, 480f);
                GUI.color = BarBackplate;
                GUI.DrawTexture(new Rect(16, 16, ow, 24), whiteTex);
                GUI.color = Color.white;
                GUI.Label(new Rect(24, 17, ow - 14, 22), objLine, objectiveStyle);
            }

            const int lineHeight = 20;
            const float barWidth = 220f;
            const float barHeight = 16f;
            const float barGap = 5f;

            // ---- Persistent vitals block, bottom-left --------------------------------------
            int barCount = (health != null ? 1 : 0) + (stamina != null ? 1 : 0)
                         + (hunger != null ? 1 : 0) + (thirst != null ? 1 : 0);
            float blockHeight = barCount * (barHeight + barGap) + 14f;
            float bx = 20f;
            float by = Screen.height - blockHeight - 18f;

            GUI.color = BarBackplate;
            GUI.DrawTexture(new Rect(bx - 8, by - 7, barWidth + 16, blockHeight), whiteTex);
            GUI.color = Color.white;

            float cursorY = by;
            if (health != null)
            {
                int hp = Mathf.RoundToInt(health.CurrentHealth);
                if (hp != cachedHp) { cachedHp = hp; hpCaption = "HP  " + hp; }
                DrawBar(bx, cursorY, barWidth, barHeight, health.CurrentHealth / Mathf.Max(1f, health.MaxHealth), HealthColor, hpCaption);
                cursorY += barHeight + barGap;
            }
            if (stamina != null)
            {
                int sta = Mathf.RoundToInt(stamina.CurrentStamina);
                if (sta != cachedSta) { cachedSta = sta; staCaption = "STA " + sta; }
                DrawBar(bx, cursorY, barWidth, barHeight, stamina.CurrentStamina / Mathf.Max(1f, stamina.MaxStamina), StaminaColor, staCaption);
                cursorY += barHeight + barGap;
            }
            if (hunger != null)
            {
                DrawBar(bx, cursorY, barWidth, barHeight, hunger.Hunger / 100f, HungerColor,
                        hunger.Hunger <= hungerThirstWarning ? "FOOD — LOW" : "FOOD");
                cursorY += barHeight + barGap;
            }
            if (thirst != null)
            {
                DrawBar(bx, cursorY, barWidth, barHeight, thirst.Thirst / 100f, ThirstColor,
                        thirst.Thirst <= hungerThirstWarning ? "WATER — LOW" : "WATER");
                cursorY += barHeight + barGap;
            }

            // ---- Contextual text lines above the vitals block ------------------------------
            int y = (int)by - 26;

            if (flashlight != null && (flashlight.IsOn || flashlight.BatteryPercent < 20f))
            {
                int bat = Mathf.RoundToInt(flashlight.BatteryPercent);
                if (bat != cachedBattery) { cachedBattery = bat; batteryCaption = "Battery: " + bat + "%"; }
                GUI.Label(new Rect(20, y, 260, lineHeight), batteryCaption, labelStyle);
                y -= lineHeight;
            }

            // Sprint 07 — small additive ammo readout for whichever weapon is currently drawn.
            if (playerEquipment != null && playerEquipment.ActiveSlot.HasValue)
            {
                var def = playerEquipment.GetActiveDefinition();
                var state = playerEquipment.GetActiveRuntimeState();
                if (def != null && state != null && def.category != TRLM.Equipment.WeaponCategory.Melee)
                {
                    if (state.currentMagazine != cachedMag || state.isReloading != cachedReloading)
                    {
                        cachedMag = state.currentMagazine;
                        cachedReloading = state.isReloading;
                        ammoCaption = def.displayName + ": " + state.currentMagazine + "/" + def.magazineCapacity
                                      + (state.isReloading ? " (Reloading...)" : "");
                    }
                    GUI.Label(new Rect(20, y, 260, lineHeight), ammoCaption, labelStyle);
                    y -= lineHeight;
                }
            }

            // Sprint 07 (A2, Section 30) — small additive injury/bleeding status block, gated the
            // same threshold-based way the rest of this HUD already is (no permanent clutter).
            if (injurySystem != null && injurySystem.HasAnyInjury())
            {
                foreach (var kvp in injurySystem.AllSeverities())
                {
                    if (kvp.Value <= 0f) continue;
                    GUI.Label(new Rect(20, y, 260, lineHeight), $"Injured: {kvp.Key}", labelStyle);
                    y -= lineHeight;
                }
            }

            if (statusEffects != null && statusEffects.HasEffect("Bleeding"))
            {
                bleedStyle ??= new GUIStyle(labelStyle) { normal = { textColor = new Color(0.85f, 0.25f, 0.2f) } };
                GUI.Label(new Rect(20, y, 260, lineHeight), "BLEEDING", bleedStyle);
                y -= lineHeight;
            }

            if (notificationTimer > 0f && !string.IsNullOrEmpty(notificationText))
            {
                notificationStyle ??= new GUIStyle(labelStyle) { fontSize = 18, alignment = TextAnchor.UpperCenter };
                GUI.Label(new Rect((Screen.width - 400f) * 0.5f, 20, 400f, 30f), notificationText, notificationStyle);
            }

            if (inventoryOpen && inventory != null)
            {
                float panelWidth = 260f;
                float panelHeight = 24f + inventory.Slots.Count * lineHeight;
                Rect panel = new Rect(Screen.width - panelWidth - 20, 20, panelWidth, panelHeight);
                GUI.Box(panel, "Inventory ([ ] select, LMB use, G drop, I close)");

                int slotY = 44;
                for (int i = 0; i < inventory.Slots.Count; i++)
                {
                    var slot = inventory.Slots[i];
                    string marker = i == inventory.SelectedSlotIndex ? "> " : "  ";
                    string line = slot.IsEmpty ? "-- empty --" : $"{slot.item.displayName} x{slot.count}";
                    GUI.Label(new Rect(panel.x + 10, panel.y + slotY, panelWidth - 20, lineHeight), marker + line, labelStyle);
                    slotY += lineHeight;
                }
            }
        }
    }
}
