using System;
using System.Collections.Generic;
using UnityEngine;

namespace TRLM.Core
{
    /// <summary>
    /// Stable identity for authored objects whose state must survive a save/load — hand-placed
    /// loot, FirePoints, SafeHouseAreas, BurialZones. Deliberately NOT attached to every prop:
    /// only objects a save adapter actually needs to look up by id (see Sprint 10 Part I). The id
    /// is a GUID baked into the scene file at author time (via Reset/OnValidate in the Editor), not
    /// regenerated on play — GetInstanceID() is unusable for this since it changes every session.
    ///
    /// A static per-scene registry lets restore code do a cheap dictionary lookup instead of
    /// FindObjectsByType-scanning for every save-relevant object on every load.
    /// </summary>
    [DisallowMultipleComponent]
    public class PersistentObjectId : MonoBehaviour
    {
        [SerializeField] private string id;

        private static readonly Dictionary<string, PersistentObjectId> registry = new Dictionary<string, PersistentObjectId>();

        public string Id => id;

        public static PersistentObjectId Find(string persistentId)
        {
            if (string.IsNullOrEmpty(persistentId)) return null;
            registry.TryGetValue(persistentId, out var found);
            return found;
        }

        // Deliberately Awake/OnDestroy, not OnEnable/OnDisable: a collected PickupItem or an
        // otherwise-toggled object is expected to end up SetActive(false) as PART of the state this
        // component identifies — if the registry entry vanished whenever the GameObject deactivated,
        // WorldStatePersistence.Restore's PersistentObjectId.Find lookup would fail for exactly the
        // objects it most needs to find (already-collected loot). Awake still runs once even on an
        // object that starts active-in-scene and is later deactivated at runtime.
        private void Awake()
        {
            if (string.IsNullOrEmpty(id)) return;

            if (registry.TryGetValue(id, out var existing) && existing != this)
            {
                Debug.LogError($"[PersistentObjectId] Duplicate id '{id}' on '{name}' and '{existing.name}' — " +
                                "each authored persistent object needs its own unique id. Reset one in the Inspector.", this);
                return;
            }
            registry[id] = this;
        }

        private void OnDestroy()
        {
            if (!string.IsNullOrEmpty(id) && registry.TryGetValue(id, out var existing) && existing == this)
                registry.Remove(id);
        }

#if UNITY_EDITOR
        // Editor-only, authoring-time only — never regenerates an id a scene file already has, so
        // saved-game data referencing it stays valid across sessions. Reset() (component just
        // added) and OnValidate() (covers a duplicated GameObject, which duplicates the id string
        // and needs a fresh one) are the only paths that touch this field.
        private void Reset() => AssignIfEmpty();

        private void OnValidate()
        {
            AssignIfEmpty();
        }

        private void AssignIfEmpty()
        {
            if (!string.IsNullOrEmpty(id)) return;
            id = Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
