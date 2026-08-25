using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TRLM.Dialogue;
using TRLM.Progression;

namespace TRLM.Notebook
{
    /// <summary>
    /// The Kehanet Defteri's data owner: the full authored page catalog (including not-yet-found
    /// pages, so the UI can show locked slots) plus the runtime set of collected ids. One instance
    /// per scene exposed as a light static Instance — the same pattern as ObjectiveSystem /
    /// DialogueSystem, and for the same reason: pickups and the save adapter both need it without
    /// per-caller wiring. Pure data + events; presentation lives in NotebookController/NotebookUI.
    /// </summary>
    public class ProphecyNotebook : MonoBehaviour
    {
        public static ProphecyNotebook Instance { get; private set; }

        [Tooltip("Every authored ProphecyPage, found or not — the notebook shows undiscovered ones as locked slots.")]
        [SerializeField] private List<ProphecyPage> allPages = new List<ProphecyPage>();

        [Header("Key-prophecy objective hook (optional)")]
        [Tooltip("When on, collecting a page marked isKeyProphecy calls ObjectiveSystem.AdvanceTo(keyProphecyObjective).")]
        [SerializeField] private bool advanceObjectiveOnKeyProphecy = false;
        [SerializeField] private ObjectiveStep keyProphecyObjective = ObjectiveStep.SliceComplete;

        [Tooltip("Inspector-wirable reaction to any successful collect (SFX, VFX, HUD flash...).")]
        [SerializeField] private UnityEvent onAnyPageCollected = new UnityEvent();

        private readonly HashSet<string> collectedIds = new HashSet<string>();
        // Cached orderIndex-sorted view of allPages — rebuilt only when the catalog could have
        // changed (never at runtime in practice), so UI paging never sorts or allocates per open.
        private readonly List<ProphecyPage> orderedPages = new List<ProphecyPage>();
        private bool orderedDirty = true;

        public event Action<ProphecyPage> OnPageCollected;

        public int CollectedCount => collectedIds.Count;
        public int TotalPageCount => allPages.Count;
        public IEnumerable<string> CollectedIds => collectedIds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ProphecyNotebook] Multiple instances in scene — keeping the first.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>All authored pages sorted by orderIndex — the notebook's fixed slot order.
        /// Returns a cached list; callers must not mutate it.</summary>
        public IReadOnlyList<ProphecyPage> OrderedPages
        {
            get
            {
                if (orderedDirty)
                {
                    orderedPages.Clear();
                    foreach (var p in allPages)
                        if (p != null) orderedPages.Add(p);
                    orderedPages.Sort((a, b) => a.orderIndex.CompareTo(b.orderIndex));
                    orderedDirty = false;
                }
                return orderedPages;
            }
        }

        public bool HasPage(string id) => !string.IsNullOrEmpty(id) && collectedIds.Contains(id);

        /// <summary>Adds the page to the collection. Returns false (and fires nothing) when null,
        /// id-less, or already collected — safe to call repeatedly from pickups.</summary>
        public bool Collect(ProphecyPage page)
        {
            if (page == null || string.IsNullOrEmpty(page.id)) return false;
            if (!collectedIds.Add(page.id)) return false;

            // Discovery reaction: an authored line (per-page, oneShot-friendly) through the
            // existing subtitle pipeline — never during a save restore, which seeds silently
            // via SeedCollected instead of coming through here.
            if (page.HasDiscoveryLine && DialogueSystem.Instance != null)
                DialogueSystem.Instance.Play(page.discoveryLine);

            if (advanceObjectiveOnKeyProphecy && page.isKeyProphecy && ObjectiveSystem.Instance != null)
                ObjectiveSystem.Instance.AdvanceTo(keyProphecyObjective);

            OnPageCollected?.Invoke(page);
            onAnyPageCollected?.Invoke();
            return true;
        }

        /// <summary>Save restore: re-mark previously collected pages without firing discovery
        /// reactions/events — mirrors DialogueSystem.SeedPlayedOneShots.</summary>
        public void SeedCollected(IEnumerable<string> ids)
        {
            if (ids == null) return;
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id)) collectedIds.Add(id);
        }

        /// <summary>New Game / full reset support.</summary>
        public void ClearCollected() => collectedIds.Clear();
    }
}
