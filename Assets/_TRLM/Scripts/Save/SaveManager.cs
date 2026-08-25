using System;
using System.IO;
using UnityEngine;
using TRLM.Progression;

namespace TRLM.Save
{
    public enum SaveResult { Success, NotFound, CorruptData, UnsupportedVersion, IoError }

    public readonly struct SaveLoadOutcome
    {
        public readonly SaveResult Result;
        public readonly SaveGameData Data;
        public readonly string Message;

        public SaveLoadOutcome(SaveResult result, SaveGameData data, string message)
        {
            Result = result;
            Data = data;
            Message = message;
        }

        public bool Success => Result == SaveResult.Success;
    }

    /// <summary>
    /// File-IO and slot-management core. Owns reading/writing/deleting/listing save files ONLY —
    /// it has no idea what a companion or an inventory slot is; gathering/restoring gameplay state
    /// is the persistence adapters' job (PlayerStatePersistence, CompanionStatePersistence,
    /// WorldStatePersistence, ProgressionStatePersistence), called by SaveOrchestrator. Splitting it
    /// this way is the whole point of Sprint 10 Part A's "no giant SaveManager" instruction.
    ///
    /// Format: JsonUtility (built into Unity, zero new package dependency, "reasonably" human
    /// debuggable via JsonUtility.ToJson(data, true)). Every DTO in SaveGameData.cs is deliberately
    /// JsonUtility-safe (no Dictionary, no nullable enums, no Unity Object refs) — see that file's
    /// header comment.
    ///
    /// Write strategy: write to "<slot>.tmp" first, verify it parses back, THEN File.Replace/Copy
    /// onto the real "<slot>.json" — a mid-write crash or full disk leaves the previous good save
    /// untouched instead of a half-written corrupt file.
    /// </summary>
    public static class SaveManager
    {
        public const int CurrentSaveVersion = 1;
        public const int ManualSlotCount = 5;
        private const string AutosaveId = "autosave";

        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        public static string ManualSlotId(int slotNumber1To5) => $"manual_{Mathf.Clamp(slotNumber1To5, 1, ManualSlotCount)}";

        private static string DataPath(string slotId) => Path.Combine(SaveDirectory, slotId + ".json");
        private static string MetaPath(string slotId) => Path.Combine(SaveDirectory, slotId + ".meta.json");
        private static string TempPath(string path) => path + ".tmp";

        public static SaveLoadOutcome WriteSave(string slotId, SaveGameData data, SaveSlotMetadata meta)
        {
            try
            {
                Directory.CreateDirectory(SaveDirectory);

                string dataJson = JsonUtility.ToJson(data, true);
                string metaJson = JsonUtility.ToJson(meta, true);

                if (!AtomicWrite(DataPath(slotId), dataJson)) return new SaveLoadOutcome(SaveResult.IoError, null, "Failed writing save data.");
                if (!AtomicWrite(MetaPath(slotId), metaJson)) return new SaveLoadOutcome(SaveResult.IoError, null, "Failed writing save metadata.");

                return new SaveLoadOutcome(SaveResult.Success, data, "Saved.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] WriteSave('{slotId}') failed: {e}");
                return new SaveLoadOutcome(SaveResult.IoError, null, e.Message);
            }
        }

        /// <summary>tmp-write, parse-back verify, then replace — see class remarks.</summary>
        private static bool AtomicWrite(string finalPath, string json)
        {
            string tmp = TempPath(finalPath);
            try
            {
                File.WriteAllText(tmp, json);

                // Verify before committing — a truncated/garbled tmp write must never overwrite a good file.
                string readBack = File.ReadAllText(tmp);
                if (readBack != json) { File.Delete(tmp); return false; }

                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(tmp, finalPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Atomic write to '{finalPath}' failed: {e}");
                if (File.Exists(tmp)) { try { File.Delete(tmp); } catch { /* best-effort cleanup */ } }
                return false;
            }
        }

        public static SaveLoadOutcome ReadSave(string slotId)
        {
            string path = DataPath(slotId);
            if (!File.Exists(path))
                return new SaveLoadOutcome(SaveResult.NotFound, null, $"No save at slot '{slotId}'.");

            string json;
            try { json = File.ReadAllText(path); }
            catch (Exception e) { return new SaveLoadOutcome(SaveResult.IoError, null, e.Message); }

            if (string.IsNullOrWhiteSpace(json))
                return new SaveLoadOutcome(SaveResult.CorruptData, null, "Save file is empty.");

            SaveGameData data;
            try { data = JsonUtility.FromJson<SaveGameData>(json); }
            catch (Exception e) { return new SaveLoadOutcome(SaveResult.CorruptData, null, $"Malformed save: {e.Message}"); }

            if (data == null)
                return new SaveLoadOutcome(SaveResult.CorruptData, null, "Save parsed to nothing.");

            if (data.saveVersion <= 0)
                return new SaveLoadOutcome(SaveResult.CorruptData, null, "Save has no valid version.");

            if (data.saveVersion > CurrentSaveVersion)
                return new SaveLoadOutcome(SaveResult.UnsupportedVersion, null,
                    $"Save version {data.saveVersion} is newer than this build supports ({CurrentSaveVersion}).");

            // data.saveVersion < CurrentSaveVersion: an older-but-readable save. No migration
            // framework exists yet (deliberately, per spec) — versions are additive-only so far,
            // so an old save loads as-is; a future migration step would go here.

            return new SaveLoadOutcome(SaveResult.Success, data, "Loaded.");
        }

        public static SaveSlotMetadata ReadMetadata(string slotId)
        {
            string path = MetaPath(slotId);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<SaveSlotMetadata>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] ReadMetadata('{slotId}') failed: {e.Message}");
                return null;
            }
        }

        public static bool SlotExists(string slotId) => File.Exists(DataPath(slotId));

        /// <summary>Never deletes autosave — DeleteManualSlot refuses on the autosave id.</summary>
        public static bool DeleteManualSlot(string slotId)
        {
            if (slotId == AutosaveId)
            {
                Debug.LogWarning("[SaveManager] Refusing to delete the autosave slot via DeleteManualSlot.");
                return false;
            }

            try
            {
                if (File.Exists(DataPath(slotId))) File.Delete(DataPath(slotId));
                if (File.Exists(MetaPath(slotId))) File.Delete(MetaPath(slotId));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] DeleteManualSlot('{slotId}') failed: {e}");
                return false;
            }
        }

        public static bool HasContinueSave() => SlotExists(AutosaveId) || AnyManualSlotExists();

        private static bool AnyManualSlotExists()
        {
            for (int i = 1; i <= ManualSlotCount; i++)
                if (SlotExists(ManualSlotId(i))) return true;
            return false;
        }

        /// <summary>Newest valid autosave-or-manual save by timestamp, for a future Main Menu's
        /// "Continue" button. Reads metadata only (cheap) — returns null if nothing valid exists.</summary>
        public static string GetMostRecentContinueSave()
        {
            string bestSlot = null;
            long bestTime = long.MinValue;

            void Consider(string slotId)
            {
                var meta = ReadMetadata(slotId);
                if (meta == null) return;
                if (meta.savedAtUnixSeconds <= bestTime) return;
                bestTime = meta.savedAtUnixSeconds;
                bestSlot = slotId;
            }

            Consider(AutosaveId);
            for (int i = 1; i <= ManualSlotCount; i++) Consider(ManualSlotId(i));

            return bestSlot;
        }

        public static string AutosaveSlotId => AutosaveId;
    }
}
