using System;
using System.Collections.Generic;
using UnityEngine;
using TRLM.Companions;
using TRLM.Equipment;
using TRLM.Progression;
using TRLM.Survival;
using TRLM.World;

namespace TRLM.Save
{
    /// <summary>
    /// The one composition point: gathers a SaveGameData from every persistence adapter, writes it
    /// via SaveManager, and does the reverse on load — in the order Sprint 10 Part T specifies
    /// (global systems -> player -> inventory/equipment -> companions -> world/progression ->
    /// weather/time) so no restore step reads state another step hasn't set up yet. Owns playtime
    /// tracking and the manual-save/checkpoint gating query; does NOT know how any individual
    /// system stores its own data — that's each adapter's job (see Part A's "no giant SaveManager").
    /// </summary>
    public class SaveOrchestrator : MonoBehaviour
    {
        public static SaveOrchestrator Instance { get; private set; }

        [SerializeField] private PlayerStatePersistence playerPersistence;
        [SerializeField] private DayNightSystem dayNight;
        [SerializeField] private TeamProvisions teamProvisions;
        [SerializeField] private PsychologicalState psych; // player's — used for restore-guard seeding

        private float playtimeSeconds;
        private readonly HashSet<CompanionId> buriedCompanions = new HashSet<CompanionId>();

        /// <summary>True for the duration of a Restore() call — nothing in this codebase currently
        /// checks it, but it's exposed so a future system with a real "don't react during restore"
        /// problem (e.g. a VO trigger on OnTierChanged) has a clean single flag to consult instead
        /// of each system needing its own guard.</summary>
        public bool IsRestoring { get; private set; }

        public float TotalPlaytimeSeconds => playtimeSeconds;

        private void Awake()
        {
            Instance = this;
            if (playerPersistence == null) playerPersistence = FindFirstObjectByType<PlayerStatePersistence>();
            if (dayNight == null) dayNight = FindFirstObjectByType<DayNightSystem>();
            if (teamProvisions == null) teamProvisions = FindFirstObjectByType<TeamProvisions>();
            if (psych == null) psych = FindFirstObjectByType<PsychologicalState>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Consumes a Main Menu Continue/Load Game/New Game request left in PendingLoad
        /// (see that class) — runs in Start rather than Awake so every other system's Awake (which
        /// Restore()/NewGame() depend on, e.g. playerPersistence's own Awake wiring) has already run.</summary>
        private void Start()
        {
            if (PendingLoad.NewGameRequested)
            {
                NewGame(PendingLoad.NewGameDifficulty);
                PendingLoad.ClearAll();
            }
            else if (PendingLoad.RequestedSlotId != null)
            {
                string slotId = PendingLoad.RequestedSlotId;
                PendingLoad.ClearAll();
                Load(slotId);
            }
        }

        private void OnEnable() => BurialZone.OnBurialComplete += HandleBurialComplete;
        private void OnDisable() => BurialZone.OnBurialComplete -= HandleBurialComplete;
        private void HandleBurialComplete(CompanionId id) => buriedCompanions.Add(id);

        // Time.deltaTime is already scaled by Time.timeScale, and EquipmentWheelUI sets
        // timeScale=0 while open — so playtime naturally excludes wheel-open time with no extra
        // pause-state tracking needed. A future real pause menu would work the same way.
        private void Update() => playtimeSeconds += Time.deltaTime;

        // ---------------------------------------------------------------- Manual save gating

        public bool CanManualSave(out string reason)
        {
            if (psych != null && psych.gameObject.TryGetComponent<HealthSystem>(out var health) && health.IsDead)
            {
                reason = "Player is dead";
                return false;
            }

            var wheel = FindFirstObjectByType<EquipmentWheelUI>();
            if (wheel != null && wheel.IsOpen)
            {
                reason = "Equipment wheel is open";
                return false;
            }

            if (!ManualSaveZone.PlayerInAnyZone)
            {
                reason = "Not in a safe area";
                return false;
            }

            reason = null;
            return true;
        }

        // ---------------------------------------------------------------- Save

        public SaveLoadOutcome SaveManual(int slotNumber1To5) => Save(SaveManager.ManualSlotId(slotNumber1To5), SaveType.Manual);
        public SaveLoadOutcome SaveAutosave() => Save(SaveManager.AutosaveSlotId, SaveType.Autosave);

        private SaveLoadOutcome Save(string slotId, SaveType type)
        {
            var data = new SaveGameData
            {
                sceneName = gameObject.scene.name,
                savedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                totalPlaytimeSeconds = playtimeSeconds,
                difficultyLevel = DifficultySettings.CurrentLevel,
                difficultyProfile = DifficultySettings.CurrentProfile,
            };

            if (playerPersistence != null)
            {
                data.player = playerPersistence.CapturePlayer();
                data.inventory = playerPersistence.CaptureInventory();
                data.equipment = playerPersistence.CaptureEquipment();
            }

            data.teamProvisions = ProgressionStatePersistence.CaptureTeamProvisions(teamProvisions);
            data.companions = CompanionStatePersistence.Capture();
            CompanionStatePersistence.AddMissingAsBuried(data.companions, (CompanionId[])Enum.GetValues(typeof(CompanionId)), buriedCompanions);
            data.world = WorldStatePersistence.Capture();
            data.notebook = TRLM.Notebook.NotebookStatePersistence.Capture();
            data.storyFlags = TRLM.Story.StoryFlagsPersistence.Capture();
            data.progression = ProgressionStatePersistence.CaptureProgression();
            data.timeWeather = ProgressionStatePersistence.CaptureTimeWeather(dayNight);

            var meta = BuildMetadata(slotId, type, data);
            return SaveManager.WriteSave(slotId, data, meta);
        }

        private SaveSlotMetadata BuildMetadata(string slotId, SaveType type, SaveGameData data)
        {
            var meta = new SaveSlotMetadata
            {
                slotId = slotId,
                saveType = type,
                savedAtUnixSeconds = data.savedAtUnixSeconds,
                totalPlaytimeSeconds = data.totalPlaytimeSeconds,
                regionName = RegionTracker.CurrentRegionName,
                sceneName = data.sceneName,
                dayCount = data.timeWeather.dayCount,
                difficultyLevel = data.difficultyLevel,
                currentObjective = data.progression.currentObjective.ToString(),
            };

            var ids = (CompanionId[])Enum.GetValues(typeof(CompanionId));
            meta.companionIds = ids;
            meta.livingCompanions = new bool[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                foreach (var c in data.companions)
                {
                    if (c.id != ids[i]) continue;
                    meta.livingCompanions[i] = c.isAlive;
                    break;
                }
            }

            return meta;
        }

        // ---------------------------------------------------------------- Load

        public SaveLoadOutcome LoadManual(int slotNumber1To5) => Load(SaveManager.ManualSlotId(slotNumber1To5));
        public SaveLoadOutcome LoadAutosave() => Load(SaveManager.AutosaveSlotId);

        private SaveLoadOutcome Load(string slotId)
        {
            var outcome = SaveManager.ReadSave(slotId);
            if (!outcome.Success)
            {
                Debug.LogWarning($"[SaveOrchestrator] Load('{slotId}') failed: {outcome.Result} — {outcome.Message}");
                return outcome;
            }

            Restore(outcome.Data);
            return outcome;
        }

        /// <summary>Restore order per Sprint 10 Part T: difficulty/global first, then player, then
        /// inventory/equipment, then companions (which need PsychologicalState's guards seeded
        /// first — see below), then world/progression, then weather/time last (purely visual/logic
        /// reconstruction, no other system depends on it being ready first).</summary>
        public void Restore(SaveGameData data)
        {
            if (data == null) return;
            IsRestoring = true;

            DifficultySettings.ApplyPreset(data.difficultyLevel, data.difficultyLevel == DifficultyLevel.Custom ? data.difficultyProfile : null);

            playtimeSeconds = data.totalPlaytimeSeconds;

            if (playerPersistence != null)
            {
                playerPersistence.RestorePlayer(data.player);
                playerPersistence.RestoreInventory(data.inventory);
                playerPersistence.RestoreEquipment(data.equipment);
            }

            ProgressionStatePersistence.RestoreTeamProvisions(data.teamProvisions, teamProvisions);

            // Seed dedup guards for anything marked buried in the save BEFORE companion/world
            // restore touches BurialZone/HealthSystem, so a re-fired OnDeath/grave-rebuild doesn't
            // re-apply morale changes that already happened last session.
            foreach (var c in data.companions)
            {
                if (c.isBuried) buriedCompanions.Add(c.id);
                if (c.deathMoraleConsequenceApplied) psych?.MarkCompanionDeathAlreadyProcessed(c.id);
            }
            foreach (var entry in data.world.usedBurialZones)
            {
                var pid = TRLM.Core.PersistentObjectId.Find(entry.persistentId);
                // Can't know which companion without decoding usedBurialZones against
                // data.companions' isBuried flags — already handled by the loop above; this only
                // needs to mark the burial-recovery guard, which has no per-zone id, so nothing
                // further to do here. Kept as an explicit no-op branch (not deleted) so the
                // ordering intent stays documented rather than silently relying on the loop above.
                _ = pid;
            }

            CompanionStatePersistence.Restore(data.companions, psych);
            WorldStatePersistence.Restore(data.world);
            // World phase too: seeds ProphecyNotebook silently and deactivates already-taken page
            // pickups — after WorldStatePersistence (same phase), before progression, so a future
            // progression step gated on notebook contents would read restored state.
            TRLM.Notebook.NotebookStatePersistence.Restore(data.notebook);
            // World phase, notebook precedent: seeds StoryFlags silently (Clear + Seed, no
            // OnFlagSet re-fire) so already-played cinematics / discovered-cave beats don't replay.
            TRLM.Story.StoryFlagsPersistence.Restore(data.storyFlags);
            ProgressionStatePersistence.RestoreProgression(data.progression);
            ProgressionStatePersistence.RestoreTimeWeather(data.timeWeather, dayNight);

            IsRestoring = false;
        }

        // ---------------------------------------------------------------- Slot queries (passthrough)

        public bool HasContinueSave() => SaveManager.HasContinueSave();
        public string GetMostRecentContinueSave() => SaveManager.GetMostRecentContinueSave();
        public bool DeleteManualSlot(int slotNumber1To5) => SaveManager.DeleteManualSlot(SaveManager.ManualSlotId(slotNumber1To5));

        // ---------------------------------------------------------------- New Game

        /// <summary>Deterministic fresh-run initialization (Part S) — does not rely on whatever a
        /// previously loaded save left lying around: resets playtime/burial tracking here, and
        /// applies the chosen difficulty preset before any gameplay system reads it.</summary>
        public void NewGame(DifficultyLevel level, DifficultyProfile customProfile = null)
        {
            playtimeSeconds = 0f;
            buriedCompanions.Clear();
            DifficultySettings.ApplyPreset(level, customProfile);
        }
    }
}
