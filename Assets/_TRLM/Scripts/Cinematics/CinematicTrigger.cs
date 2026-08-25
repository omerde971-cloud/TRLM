using UnityEngine;

namespace TRLM.Cinematics
{
    /// <summary>
    /// Player-enter trigger volume that fires a CinematicDirector.Play() once — the standard way a
    /// walk-into-it story beat (cave entrance, discovery spots) starts. Tag-gated like
    /// PreparationSequence/RegionEntryTrigger. Needs no PersistentObjectId of its own: the
    /// session-local fired flag stops same-session double-fires (re-entering the volume), and
    /// across saves the director's own playOnce + StoryFlags (persisted) makes a repeat Play()
    /// call a harmless skip — so this component checks director.HasAlreadyPlayed purely to avoid
    /// even that no-op after a reload.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CinematicTrigger : MonoBehaviour
    {
        [SerializeField] private CinematicDirector cinematic;
        [SerializeField] private string playerTag = "Player";
        [Tooltip("Deactivate this trigger object after firing so the collider stops testing overlaps.")]
        [SerializeField] private bool deactivateAfterFire = true;

        private bool fired;

        private void Reset()
        {
            // Authoring convenience only — runtime never mutates the collider.
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (fired) return;
            if (cinematic == null) return;
            if (!other.CompareTag(playerTag)) return;

            fired = true;

            // Already seen in a previous session (StoryFlags-persisted)? Retire quietly without
            // even the skip-path onCinematicEnd invoke a blind Play() would produce.
            if (!cinematic.HasAlreadyPlayed)
                cinematic.Play();

            if (deactivateAfterFire) gameObject.SetActive(false);
        }
    }
}
