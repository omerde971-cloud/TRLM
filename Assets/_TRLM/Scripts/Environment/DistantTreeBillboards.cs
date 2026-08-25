using System.Collections.Generic;
using UnityEngine;

namespace TRLM.Environment
{
    /// <summary>
    /// Keeps the ~285 distant tree cards facing the camera (Y-axis only) so they stop reading
    /// as flat planes from off-angles. One manager for the whole set: cards register by being
    /// children of the roots assigned here (auto-found by name prefix as a fallback), and each
    /// frame only a slice of the list is rotated (round-robin), so the per-frame cost stays a
    /// few dozen transform writes regardless of tree count. Cards must not be static-batched
    /// (the setup pass clears their static flags) — rotation is exactly the property batching
    /// would freeze.
    /// </summary>
    public class DistantTreeBillboards : MonoBehaviour
    {
        [Tooltip("Parents whose children (recursively, by renderer) are treated as billboard cards.")]
        [SerializeField] private Transform[] cardRoots;
        [Tooltip("Name prefix used to auto-collect cards when no roots are assigned.")]
        [SerializeField] private string cardNamePrefix = "PF_TreeCard";
        [Tooltip("How many cards are updated per frame (round-robin through the whole set).")]
        [SerializeField] private int cardsPerFrame = 48;
        [Tooltip("Extra world-Y rotation applied after facing, matching the card art's authored front axis.")]
        [SerializeField] private float yawOffset;

        private readonly List<Transform> cards = new List<Transform>(320);
        private Camera cam;
        private int cursor;

        private void Start()
        {
            Collect();
            cam = Camera.main;
        }

        private void Collect()
        {
            cards.Clear();
            if (cardRoots != null && cardRoots.Length > 0)
            {
                foreach (var root in cardRoots)
                {
                    if (root == null) continue;
                    foreach (var t in root.GetComponentsInChildren<Transform>(false))
                        if (t.name.StartsWith(cardNamePrefix)) RegisterCard(t);
                }
            }

            if (cards.Count == 0 && !string.IsNullOrEmpty(cardNamePrefix))
            {
                // Fallback: scene-wide sweep once at startup (never per-frame).
                foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.StartsWith(cardNamePrefix)) RegisterCard(t);
            }
        }

        private void RegisterCard(Transform card)
        {
            cards.Add(card);
            // WindSway resets localRotation from a cached base every frame, which would fight
            // the billboard facing (and 285 per-frame sways on distant cards are wasted CPU) —
            // billboarding owns these transforms now.
            var sway = card.GetComponent<WindSway>();
            if (sway != null) sway.enabled = false;
        }

        private void LateUpdate()
        {
            if (cards.Count == 0) return;
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }

            Vector3 camPos = cam.transform.position;
            int steps = Mathf.Min(cardsPerFrame, cards.Count);
            for (int i = 0; i < steps; i++)
            {
                cursor = (cursor + 1) % cards.Count;
                var t = cards[cursor];
                if (t == null) continue;
                Vector3 dir = camPos - t.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1f) continue;
                t.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(dir).eulerAngles.y + yawOffset, 0f);
            }
        }
    }
}
