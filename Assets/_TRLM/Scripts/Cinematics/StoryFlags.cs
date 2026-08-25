using System;
using System.Collections.Generic;
using UnityEngine;

namespace TRLM.Story
{
    /// <summary>
    /// Generic one-time story event registry — a scene singleton (ObjectiveSystem/DialogueSystem
    /// pattern) holding string flag ids that must fire at most once per playthrough: "cinematic X
    /// already played", "cave discovered", "first weapon found", and any future unique story beat.
    /// Deliberately just a HashSet with an event, not a quest graph — systems that need richer
    /// state (ObjectiveSystem, ProphecyNotebook) already own it; this covers the long tail of
    /// booleans that would otherwise each grow their own ad-hoc persistence.
    /// Persisted via StoryFlagsPersistence (world/progression save phase); Seed() restores
    /// silently so reloads never re-fire OnFlagSet side effects.
    /// </summary>
    public class StoryFlags : MonoBehaviour
    {
        public static StoryFlags Instance { get; private set; }

        /// <summary>Fired only when a flag is NEWLY set at gameplay time (never during Seed()).</summary>
        public event Action<string> OnFlagSet;

        private readonly HashSet<string> flags = new HashSet<string>();

        /// <summary>All currently set flag ids — enumerated by StoryFlagsPersistence at capture time.</summary>
        public IEnumerable<string> All => flags;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[StoryFlags] Multiple instances in scene — keeping the first.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Sets a flag. Returns true (and fires OnFlagSet) only when the flag was newly
        /// set — false for a null/empty id or an id already set, so callers can gate one-time side
        /// effects on the return value alone.</summary>
        public bool Set(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!flags.Add(id)) return false;
            OnFlagSet?.Invoke(id);
            return true;
        }

        /// <summary>True if the flag has been set (this session or via a restored save).</summary>
        public bool Has(string id)
        {
            return !string.IsNullOrEmpty(id) && flags.Contains(id);
        }

        /// <summary>Save-restore seeding: marks flags WITHOUT firing OnFlagSet, mirroring
        /// DialogueSystem.SeedPlayedOneShots / ProphecyNotebook.SeedCollected — a reload must not
        /// replay the side effects that set these flags last session.</summary>
        public void Seed(IEnumerable<string> ids)
        {
            if (ids == null) return;
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id)) flags.Add(id);
        }

        /// <summary>Drops every flag (New Game / pre-restore reset). Silent, like Seed().</summary>
        public void Clear()
        {
            flags.Clear();
        }
    }
}
