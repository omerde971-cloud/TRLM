using System;
using System.Collections.Generic;
using TRLM.Combat;
using TRLM.Companions;
using TRLM.Progression;
using TRLM.Weather;

namespace TRLM.Save
{
    /// <summary>
    /// Explicit, versioned gameplay persistence — never a scene/GameObject dump. Every field here
    /// is meaningful save-relevant state (Sprint 10 Part A/E-P); transient visuals (particles,
    /// coroutine progress, NavMeshAgent internals) are deliberately absent and reconstructed from
    /// this data instead. All DTOs are plain [Serializable] classes with only JsonUtility-safe
    /// members (no Dictionary, no nullable/interface fields, no Unity Object references) since
    /// JsonUtility is the serializer (see SaveManager remarks for why).
    /// </summary>
    [Serializable]
    public class SaveGameData
    {
        public int saveVersion = SaveManager.CurrentSaveVersion;
        public string sceneName;
        public long savedAtUnixSeconds;
        public float totalPlaytimeSeconds;

        public DifficultyLevel difficultyLevel;
        public DifficultyProfile difficultyProfile = new DifficultyProfile();

        public PlayerStateData player = new PlayerStateData();
        public InventoryData inventory = new InventoryData();
        public EquipmentData equipment = new EquipmentData();
        public TeamProvisionsData teamProvisions = new TeamProvisionsData();
        public List<CompanionStateData> companions = new List<CompanionStateData>();
        public WorldStateData world = new WorldStateData();
        public ProgressionData progression = new ProgressionData();
        public TimeWeatherData timeWeather = new TimeWeatherData();
        // Additive (save version stays 1): the Kehanet Defteri. Older saves without this field
        // deserialize it as the field initializer (empty lists) — an empty notebook, correct.
        public NotebookData notebook = new NotebookData();
        // Additive (save version stays 1): one-time story flags (StoryFlags — cinematics played,
        // cave discovered, generic unique beats). Older saves deserialize an empty list — correct.
        public StoryFlagsData storyFlags = new StoryFlagsData();
    }

    [Serializable]
    public class PlayerStateData
    {
        public float posX, posY, posZ;
        public float yawDegrees;

        public float health = 100f;
        public bool isDead;

        public float hunger = 100f;
        public float thirst = 100f;
        public float wetness;
        public float bodyTemperature = 100f;

        public List<InjuryEntry> injuries = new List<InjuryEntry>();
        public float bleedSeverity;
        public float poisonSeverity;

        public float sanityStability = 100f;

        public float flashlightBatteryPercent = 100f;
    }

    [Serializable]
    public class InjuryEntry
    {
        public BodyRegion region;
        public float severity;
    }

    [Serializable]
    public class InventorySlotData
    {
        public string itemId; // empty/null = empty slot
        public int count;
    }

    [Serializable]
    public class InventoryData
    {
        public List<InventorySlotData> slots = new List<InventorySlotData>();
        public int selectedSlotIndex;
    }

    [Serializable]
    public class EquippedWeaponData
    {
        public string weaponId; // empty/null = slot empty
        public int currentMagazine;
    }

    [Serializable]
    public class EquipmentData
    {
        public EquippedWeaponData sidearm = new EquippedWeaponData();
        public EquippedWeaponData longGunA = new EquippedWeaponData();
        public EquippedWeaponData longGunB = new EquippedWeaponData();
        public EquippedWeaponData melee = new EquippedWeaponData();
        // -1 = no active slot; otherwise an EquipmentSlotType index (JsonUtility can't serialize a
        // nullable enum, so a sentinel int stands in).
        public int activeSlotIndex = -1;
    }

    [Serializable]
    public class TeamProvisionsData
    {
        public float sharedFood = 300f;
        public float sharedWater = 300f;
        public int livingTeamMembers = 5;
    }

    [Serializable]
    public class CompanionStateData
    {
        public CompanionId id;
        public bool isAlive = true;
        public bool isBuried;
        // Only meaningful while isAlive — a dead companion's last position is where its corpse (or
        // grave, if buried) already is, reconstructed separately, not from this.
        public float posX, posY, posZ;
        public int commandStateIndex; // CompanionAI.State enum index — Follow by default
        public bool deathMoraleConsequenceApplied;
    }

    [Serializable]
    public class PersistentFlagEntry
    {
        public string persistentId;
    }

    [Serializable]
    public class BurialZoneEntry
    {
        public string persistentId;
        // Explicit grave-to-companion link (Sprint 10 gap-closing) — a scene with more than one
        // burial zone can now tell which companion each grave belongs to, instead of only knowing
        // "some zone was used" and inferring identity by cross-referencing missing scene objects.
        public CompanionId companionId;
    }

    [Serializable]
    public class WorldStateData
    {
        /// <summary>PersistentObjectId values of PickupItems already collected — do not respawn on load.</summary>
        public List<PersistentFlagEntry> collectedLoot = new List<PersistentFlagEntry>();
        /// <summary>PersistentObjectId values of FirePoints currently lit.</summary>
        public List<PersistentFlagEntry> litFires = new List<PersistentFlagEntry>();
        /// <summary>PersistentObjectId values of SafeHouseAreas the player has entered at least once.</summary>
        public List<PersistentFlagEntry> discoveredSafeHouses = new List<PersistentFlagEntry>();
        /// <summary>Used BurialZones, paired with the companion buried in each.</summary>
        public List<BurialZoneEntry> usedBurialZones = new List<BurialZoneEntry>();

        // Deliberately NOT persisted (Sprint 10 spec Parts O/P) — transient/procedural, documented
        // exclusions rather than an oversight: individual wolf/wildlife positions and spawn-manager
        // state (ecology respawns logically each load), in-flight rockfall rock Rigidbodies.
    }

    [Serializable]
    public class ProgressionData
    {
        public ObjectiveStep currentObjective;
        // Additive (save version stays 1): ids of one-shot dialogue lines that already played, so
        // reloading doesn't replay story barks. Older saves deserialize this as an empty list.
        public List<string> playedDialogueIds = new List<string>();
    }

    [Serializable]
    public class NotebookData
    {
        /// <summary>ProphecyPage.id values already in the Kehanet Defteri.</summary>
        public List<string> collectedPageIds = new List<string>();
        /// <summary>PersistentObjectId values of ProphecyPagePickups already taken — do not respawn on load.</summary>
        public List<string> collectedPickupIds = new List<string>();
    }

    [Serializable]
    public class StoryFlagsData
    {
        /// <summary>StoryFlags ids already set — cinematic-played, cave-discovered, and other
        /// one-time story events. Restored silently (no OnFlagSet re-fire).</summary>
        public List<string> flagIds = new List<string>();
    }

    [Serializable]
    public class TimeWeatherData
    {
        public float elapsedSeconds;
        public int dayCount = 1;
        public WeatherType currentWeather = WeatherType.Clear;
    }
}
