using System;
using UnityEngine;
using TRLM.Boat;
using TRLM.World;
using TRLM.Inventory;

namespace TRLM.Progression
{
    /// <summary>
    /// Single vertical-slice objective tracker. One instance per scene (WorldSystems), exposed
    /// as a light static Instance the same way WildlifeSpawnManager is — every system that wants
    /// to advance the objective needs it, and it's the only place in the sprint this pattern is
    /// used besides the existing wildlife manager.
    ///
    /// Auto-wired triggers (Sprint 05): LandingZone.OnLanded -> ReachLandingZone; DayNightSystem
    /// going night -> NightBegins; SafeHouseArea player-enter -> ReachSafeHouse; FirePoint.OnLit
    /// -> LightFire; SleepInteraction completing -> WakeNextMorning.
    ///
    /// Auto-wired triggers (Sprint 06): RegionEntryTrigger components placed in the scene ->
    /// EnterCoastalForest / ReachAbandonedHouse; PickupItem.OnAnyItemPickedUp (any successful
    /// pickup, a documented simplification — see class remarks on PickupItem) -> SearchHouse;
    /// PlayerInventory.OnInventoryChanged, checked against waterItem/foodItem -> AcquireEssentialLoot;
    /// WolfThreatObjectiveWatcher polling real WolfAI.CurrentState -> WolfThreat.
    ///
    /// Still AdvanceTo/Advance-only, no automatic trigger: Sleep, SliceComplete, PreparationComplete,
    /// RowToIsland — PreparationComplete is driven by PreparationSequence in the cinematic scene,
    /// the rest are expected to be called directly by a future scene script.
    /// </summary>
    public class ObjectiveSystem : MonoBehaviour
    {
        public static ObjectiveSystem Instance { get; private set; }

        [SerializeField] private ObjectiveStep current = ObjectiveStep.PreparationComplete;

        [Header("Sprint 06: AcquireEssentialLoot check")]
        [SerializeField] private ItemDefinition waterItem;
        [SerializeField] private ItemDefinition foodItem;

        private PlayerInventory trackedInventory;

        public event Action<ObjectiveStep> OnObjectiveChanged;

        public ObjectiveStep Current => current;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ObjectiveSystem] Multiple instances in scene — keeping the first.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            LandingZone.OnLanded += HandleLanded;
            PickupItem.OnAnyItemPickedUp += HandleItemPickedUp;

            // FirePoint.OnLit is per-instance, not static — subscribe to every fire already in the
            // scene at startup (this sprint doesn't spawn new FirePoints at runtime).
            foreach (var fire in FindObjectsByType<FirePoint>(FindObjectsSortMode.None))
                fire.OnLit += HandleFireLit;

            // PlayerInventory is likewise per-instance; there's exactly one (PF_Player) in a
            // vertical-slice scene, so a scene-wide find at enable time is fine.
            trackedInventory = FindFirstObjectByType<PlayerInventory>();
            if (trackedInventory != null)
            {
                trackedInventory.OnInventoryChanged += HandleInventoryChanged;
                HandleInventoryChanged(); // covers "already had water+food before this fired" (e.g. picked up outdoors first)
            }
        }

        private void OnDisable()
        {
            LandingZone.OnLanded -= HandleLanded;
            PickupItem.OnAnyItemPickedUp -= HandleItemPickedUp;
            foreach (var fire in FindObjectsByType<FirePoint>(FindObjectsSortMode.None))
                fire.OnLit -= HandleFireLit;

            if (trackedInventory != null)
                trackedInventory.OnInventoryChanged -= HandleInventoryChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleLanded() => AdvanceTo(ObjectiveStep.ReachLandingZone);
        private void HandleFireLit() => AdvanceTo(ObjectiveStep.LightFire);

        // Any successful pickup counts as "searched the house" for the vertical slice — a
        // deliberate simplification rather than scoping to house-specific loot points. Gated so a
        // beach/forest pickup can't leapfrog the route steps (EnterCoastalForest/ReachAbandonedHouse).
        private void HandleItemPickedUp(PickupItem _) => AdvanceToInOrder(ObjectiveStep.SearchHouse, ObjectiveStep.ReachAbandonedHouse);

        private void HandleInventoryChanged()
        {
            if (trackedInventory == null || waterItem == null || foodItem == null) return;
            if (trackedInventory.HasItem(waterItem) && trackedInventory.HasItem(foodItem))
                AdvanceToInOrder(ObjectiveStep.AcquireEssentialLoot, ObjectiveStep.SearchHouse);
        }

        public void Advance()
        {
            int next = (int)current + 1;
            int max = Enum.GetValues(typeof(ObjectiveStep)).Length - 1;
            SetCurrent((ObjectiveStep)Mathf.Min(next, max));
        }

        /// <summary>Jump directly to a step. Used by systems that can't guarantee strict ordering
        /// (e.g. the player could reach the safe house before every earlier step fired).</summary>
        public void AdvanceTo(ObjectiveStep step)
        {
            if (step <= current) return;
            SetCurrent(step);
        }

        /// <summary>Advance to <paramref name="step"/> only once progression has already reached
        /// <paramref name="requiredMinimum"/>. Use for event-driven steps (pickups, time-of-day,
        /// wildlife) that would otherwise leapfrog the authored route order when they happen early.
        /// Still idempotent like AdvanceTo.</summary>
        public void AdvanceToInOrder(ObjectiveStep step, ObjectiveStep requiredMinimum)
        {
            if (current < requiredMinimum) return;
            AdvanceTo(step);
        }

        private void SetCurrent(ObjectiveStep step)
        {
            if (current == step) return;
            current = step;
            OnObjectiveChanged?.Invoke(current);
        }
    }
}
