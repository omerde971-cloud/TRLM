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
    /// Always-on gameplay HUD (IMGUI). Modernised vitals: rounded bars (via the IMGUI
    /// rounded-corner DrawTexture overload), animated smooth fills, a delayed "ghost/chip"
    /// damage layer on health, value-driven colour (green→amber→red), a low-health pulse and a
    /// subtle glossy gradient — no Canvas/sprite assets required. Objective line, ammo, injury,
    /// bleeding, notifications and the inventory list are unchanged.
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

        // Per-frame string caches (GC pass): OnGUI runs at least twice a frame.
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
        private GUIStyle barCaptionStyle;
        private GUIStyle barValueStyle;
        private GUIStyle bleedStyle;
        private GUIStyle notificationStyle;
        private GUIStyle objectiveStyle;
        private Texture2D whiteTex;
        private Texture2D glossTex; // vertical gradient for a glossy fill highlight

        // Animated, smoothed fill state (0..1) so bars glide instead of snapping.
        private float dispHp, ghostHp = 1f, dispSta = 1f, dispHunger = 1f, dispThirst = 1f;
        private bool statsInit;

        // Modern muted-survival palette.
        private static readonly Color PanelPlate    = new Color(0.05f, 0.06f, 0.07f, 0.62f);
        private static readonly Color PanelEdge      = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color BarTrack        = new Color(0.10f, 0.11f, 0.13f, 0.92f);
        private static readonly Color BarTrackEdge    = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color GhostColor      = new Color(0.85f, 0.32f, 0.22f, 0.55f); // health chip trail
        private static readonly Color HealthHigh      = new Color(0.36f, 0.72f, 0.36f, 1f);
        private static readonly Color HealthMid       = new Color(0.86f, 0.72f, 0.28f, 1f);
        private static readonly Color HealthLow       = new Color(0.82f, 0.26f, 0.20f, 1f);
        private static readonly Color StaminaColor    = new Color(0.80f, 0.68f, 0.32f, 1f);
        private static readonly Color HungerColor      = new Color(0.64f, 0.47f, 0.27f, 1f);
        private static readonly Color ThirstColor      = new Color(0.34f, 0.58f, 0.72f, 1f);
        private static readonly Color CaptionColor     = new Color(0.86f, 0.87f, 0.85f, 0.85f);
        private static readonly Color ValueColor       = new Color(1f, 1f, 1f, 0.95f);

        private void OnEnable()
        {
            if (input != null)
            {
                input.InventoryPressed += ToggleInventory;
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

            // Smooth the vitals toward their real values so the bars glide.
            float hp = health  != null ? Mathf.Clamp01(health.CurrentHealth   / Mathf.Max(1f, health.MaxHealth))    : 0f;
            float st = stamina != null ? Mathf.Clamp01(stamina.CurrentStamina / Mathf.Max(1f, stamina.MaxStamina))  : 0f;
            float hu = hunger  != null ? Mathf.Clamp01(hunger.Hunger / 100f) : 0f;
            float th = thirst  != null ? Mathf.Clamp01(thirst.Thirst / 100f) : 0f;

            if (!statsInit) { dispHp = ghostHp = hp; dispSta = st; dispHunger = hu; dispThirst = th; statsInit = true; }

            float k = 1f - Mathf.Exp(-10f * Time.deltaTime); // frame-rate independent lerp
            dispHp     = Mathf.Lerp(dispHp, hp, k);
            dispSta    = Mathf.Lerp(dispSta, st, k);
            dispHunger = Mathf.Lerp(dispHunger, hu, k);
            dispThirst = Mathf.Lerp(dispThirst, th, k);

            // Ghost trails behind on damage, snaps forward on heal.
            if (ghostHp > dispHp) ghostHp = Mathf.Lerp(ghostHp, dispHp, 1f - Mathf.Exp(-3f * Time.deltaTime));
            else ghostHp = dispHp;
        }

        private static Color HealthGradient(float f)
        {
            if (f >= 0.6f) return Color.Lerp(HealthMid, HealthHigh, (f - 0.6f) / 0.4f);
            if (f >= 0.3f) return Color.Lerp(HealthLow, HealthMid, (f - 0.3f) / 0.3f);
            return HealthLow;
        }

        /// <summary>Draws a filled rounded rect using the IMGUI rounded-corner overload.</summary>
        private void RoundRect(Rect r, Color color, float radius)
        {
            GUI.DrawTexture(r, whiteTex, ScaleMode.StretchToFill, true, 0f, color, Vector4.zero,
                new Vector4(radius, radius, radius, radius));
        }

        private void RoundBorder(Rect r, Color color, float radius, float width)
        {
            GUI.DrawTexture(r, whiteTex, ScaleMode.StretchToFill, true, 0f, color,
                new Vector4(width, width, width, width), new Vector4(radius, radius, radius, radius));
        }

        /// <summary>Modern bar: track + ghost + gradient fill + gloss + border + caption/value.</summary>
        private void DrawModernBar(float x, float y, float width, float height,
            float fraction, float ghost, Color fill, string caption, string value, bool pulse)
        {
            float r = height * 0.5f;
            Rect track = new Rect(x, y, width, height);
            // Track
            RoundRect(track, BarTrack, r);
            RoundBorder(track, BarTrackEdge, r, 1f);

            float inset = 2f;
            float innerW = width - inset * 2f;
            float innerH = height - inset * 2f;
            float ir = innerH * 0.5f;

            // Ghost (delayed damage) layer under the fill.
            if (ghost > fraction + 0.001f)
            {
                float gw = Mathf.Max(innerH, innerW * ghost);
                RoundRect(new Rect(x + inset, y + inset, gw, innerH), GhostColor, ir);
            }

            // Main fill.
            if (fraction > 0.001f)
            {
                float fw = Mathf.Max(innerH, innerW * fraction);
                Rect fr = new Rect(x + inset, y + inset, fw, innerH);
                RoundRect(fr, fill, ir);
                // Glossy top highlight (upper half, subtle white gradient).
                var gloss = new Color(1f, 1f, 1f, 0.14f);
                GUI.DrawTexture(new Rect(fr.x, fr.y, fr.width, fr.height * 0.5f), glossTex,
                    ScaleMode.StretchToFill, true, 0f, gloss, Vector4.zero, new Vector4(ir, ir, 0f, 0f));
            }

            // Low-health pulse: a soft red rim.
            if (pulse)
            {
                float p = Mathf.Sin(Time.unscaledTime * 6f) * 0.5f + 0.5f;
                RoundBorder(track, new Color(0.9f, 0.2f, 0.15f, 0.25f + p * 0.5f), r, 2f);
            }

            // Caption (left) + value (right).
            if (!string.IsNullOrEmpty(caption))
                GUI.Label(new Rect(x + 10f, y - 1f, width - 20f, height + 2f), caption, barCaptionStyle);
            if (!string.IsNullOrEmpty(value))
                GUI.Label(new Rect(x, y - 1f, width - 10f, height + 2f), value, barValueStyle);
        }

        private void EnsureResources()
        {
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            barCaptionStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = CaptionColor }
            };
            barValueStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight,
                normal = { textColor = ValueColor }
            };
            objectiveStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.87f, 0.83f, 0.68f, 0.95f) }
            };
            if (whiteTex == null)
            {
                whiteTex = new Texture2D(1, 1);
                whiteTex.SetPixel(0, 0, Color.white);
                whiteTex.Apply();
            }
            if (glossTex == null)
            {
                glossTex = new Texture2D(1, 2);
                glossTex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0f));   // bottom transparent
                glossTex.SetPixel(0, 1, new Color(1f, 1f, 1f, 1f));   // top white
                glossTex.wrapMode = TextureWrapMode.Clamp;
                glossTex.Apply();
            }
        }

        private void OnGUI()
        {
            EnsureResources();

            // ---- Persistent current-objective line, top-left (rounded plate) --------------------
            if (objectiveSystem != null)
            {
                string objLine = "OBJECTIVE   " + ObjectiveLabel(objectiveSystem.Current);
                float ow = Mathf.Min(objectiveStyle.CalcSize(new GUIContent(objLine)).x + 28f, 500f);
                RoundRect(new Rect(16, 16, ow, 28), PanelPlate, 8f);
                RoundBorder(new Rect(16, 16, ow, 28), PanelEdge, 8f, 1f);
                GUI.Label(new Rect(28, 17, ow - 20, 26), objLine, objectiveStyle);
            }

            const int lineHeight = 20;
            const float barWidth = 230f;
            const float barHeight = 15f;
            const float barGap = 7f;

            // ---- Vitals block, bottom-left (rounded panel) --------------------------------------
            int barCount = (health != null ? 1 : 0) + (stamina != null ? 1 : 0)
                         + (hunger != null ? 1 : 0) + (thirst != null ? 1 : 0);
            float pad = 12f;
            float blockHeight = barCount * (barHeight + barGap) - barGap + pad * 2f;
            float bx = 22f;
            float by = Screen.height - blockHeight - 22f;

            RoundRect(new Rect(bx - pad, by - pad, barWidth + pad * 2f, blockHeight), PanelPlate, 10f);
            RoundBorder(new Rect(bx - pad, by - pad, barWidth + pad * 2f, blockHeight), PanelEdge, 10f, 1f);

            float cursorY = by;
            if (health != null)
            {
                int hp = Mathf.RoundToInt(health.CurrentHealth);
                if (hp != cachedHp) { cachedHp = hp; hpCaption = hp.ToString(); }
                bool low = dispHp < 0.28f;
                DrawModernBar(bx, cursorY, barWidth, barHeight, dispHp, ghostHp, HealthGradient(dispHp), "HEALTH", hpCaption, low);
                cursorY += barHeight + barGap;
            }
            if (stamina != null)
            {
                int sta = Mathf.RoundToInt(stamina.CurrentStamina);
                if (sta != cachedSta) { cachedSta = sta; staCaption = sta.ToString(); }
                DrawModernBar(bx, cursorY, barWidth, barHeight, dispSta, 0f, StaminaColor, "STAMINA", staCaption, false);
                cursorY += barHeight + barGap;
            }
            if (hunger != null)
            {
                DrawModernBar(bx, cursorY, barWidth, barHeight, dispHunger, 0f, HungerColor,
                    hunger.Hunger <= hungerThirstWarning ? "FOOD  (LOW)" : "FOOD", Mathf.RoundToInt(hunger.Hunger).ToString(),
                    hunger.Hunger <= hungerThirstWarning);
                cursorY += barHeight + barGap;
            }
            if (thirst != null)
            {
                DrawModernBar(bx, cursorY, barWidth, barHeight, dispThirst, 0f, ThirstColor,
                    thirst.Thirst <= hungerThirstWarning ? "WATER  (LOW)" : "WATER", Mathf.RoundToInt(thirst.Thirst).ToString(),
                    thirst.Thirst <= hungerThirstWarning);
                cursorY += barHeight + barGap;
            }

            // ---- Contextual text lines above the vitals block ------------------------------
            int y = (int)(by - pad) - 26;

            if (flashlight != null && (flashlight.IsOn || flashlight.BatteryPercent < 20f))
            {
                int bat = Mathf.RoundToInt(flashlight.BatteryPercent);
                if (bat != cachedBattery) { cachedBattery = bat; batteryCaption = "Battery: " + bat + "%"; }
                GUI.Label(new Rect(22, y, 260, lineHeight), batteryCaption, labelStyle);
                y -= lineHeight;
            }

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
                    GUI.Label(new Rect(22, y, 260, lineHeight), ammoCaption, labelStyle);
                    y -= lineHeight;
                }
            }

            if (injurySystem != null && injurySystem.HasAnyInjury())
            {
                foreach (var kvp in injurySystem.AllSeverities())
                {
                    if (kvp.Value <= 0f) continue;
                    GUI.Label(new Rect(22, y, 260, lineHeight), $"Injured: {kvp.Key}", labelStyle);
                    y -= lineHeight;
                }
            }

            if (statusEffects != null && statusEffects.HasEffect("Bleeding"))
            {
                bleedStyle ??= new GUIStyle(labelStyle) { normal = { textColor = new Color(0.85f, 0.25f, 0.2f) } };
                GUI.Label(new Rect(22, y, 260, lineHeight), "BLEEDING", bleedStyle);
                y -= lineHeight;
            }

            if (notificationTimer > 0f && !string.IsNullOrEmpty(notificationText))
            {
                notificationStyle ??= new GUIStyle(labelStyle) { fontSize = 18, alignment = TextAnchor.UpperCenter };
                float a = Mathf.Clamp01(notificationTimer / Mathf.Max(0.01f, notificationSeconds));
                float w = 420f;
                RoundRect(new Rect((Screen.width - w) * 0.5f, 18, w, 34), new Color(0.05f, 0.06f, 0.07f, 0.6f * a), 9f);
                var prev = notificationStyle.normal.textColor;
                notificationStyle.normal.textColor = new Color(1f, 0.96f, 0.85f, a);
                GUI.Label(new Rect((Screen.width - w) * 0.5f, 24, w, 28f), notificationText, notificationStyle);
                notificationStyle.normal.textColor = prev;
            }

            if (inventoryOpen && inventory != null)
            {
                float panelWidth = 270f;
                float panelHeight = 40f + inventory.Slots.Count * lineHeight;
                Rect panel = new Rect(Screen.width - panelWidth - 22, 22, panelWidth, panelHeight);
                RoundRect(panel, PanelPlate, 10f);
                RoundBorder(panel, PanelEdge, 10f, 1f);
                GUI.Label(new Rect(panel.x + 12, panel.y + 8, panelWidth - 24, lineHeight),
                    "INVENTORY  ([ ] select · LMB use · G drop · I close)", barCaptionStyle);

                int slotY = 34;
                for (int i = 0; i < inventory.Slots.Count; i++)
                {
                    var slot = inventory.Slots[i];
                    bool sel = i == inventory.SelectedSlotIndex;
                    if (sel) RoundRect(new Rect(panel.x + 8, panel.y + slotY - 1, panelWidth - 16, lineHeight), new Color(1f, 1f, 1f, 0.06f), 5f);
                    string marker = sel ? "▸ " : "   ";
                    string line = slot.IsEmpty ? "— empty —" : $"{slot.item.displayName}  ×{slot.count}";
                    GUI.Label(new Rect(panel.x + 14, panel.y + slotY, panelWidth - 28, lineHeight), marker + line, labelStyle);
                    slotY += lineHeight;
                }
            }
        }
    }
}
