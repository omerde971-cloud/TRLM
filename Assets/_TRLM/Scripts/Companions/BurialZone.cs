using System;
using System.Collections;
using UnityEngine;
using TRLM.Interaction;
using TRLM.Survival;

namespace TRLM.Companions
{
    /// <summary>
    /// A single authored burial site. Requires the player to be carrying a corpse (via BodyCarry)
    /// to do anything useful. Interact() runs a short timed action with an OnGUI progress readout,
    /// then destroys the corpse, builds a simple runtime grave marker (two scaled cubes forming a
    /// cross — no new mesh asset), and fires OnBurialComplete as a documented hook for a future
    /// morale/sanity system (not implemented this sprint).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BurialZone : MonoBehaviour, IInteractable
    {
        [Header("Timing")]
        [SerializeField] private float burySeconds = 4f;

        [Header("Cost")]
        // StaminaSystem exposes no generic "flat cost" API beyond ConsumeJump's fixed jumpCost —
        // calling it a few times over the burial duration is the closest existing pattern without
        // modifying StaminaSystem.cs.
        [SerializeField] private int staminaConsumeTicks = 2;

        /// <summary>Fires with the buried companion's id (Sprint 10 — was parameterless; the only
        /// existing subscriber, PsychologicalState, already only used it as a signal). Save/world
        /// state needs to know WHICH companion was buried, not just that a burial happened.</summary>
        public static event Action<CompanionId> OnBurialComplete;

        private bool inProgress;
        private float progress;

        /// <summary>True once this specific zone has completed a burial — save/restore uses this
        /// (via a PersistentObjectId on the same GameObject) so a loaded game doesn't offer "Bury
        /// Body" at an already-used grave and doesn't duplicate the morale recovery.</summary>
        public bool HasBuried { get; private set; }

        /// <summary>Which companion this specific grave holds, once HasBuried is true — lets save
        /// data pair a grave's PersistentObjectId with a CompanionId instead of only recording "some
        /// zone was used," so the architecture already distinguishes grave identity per companion if
        /// a future scene adds more than one burial site.</summary>
        public CompanionId BuriedCompanionId { get; private set; }

        public string InteractionPrompt
        {
            get
            {
                var carry = FindNearestBodyCarry();
                return carry != null && carry.IsCarrying ? "Bury Body" : "Nothing to bury here";
            }
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        public void Interact(GameObject interactor)
        {
            if (inProgress) return;

            var carry = interactor.GetComponent<BodyCarry>();
            if (carry == null || !carry.IsCarrying) return;

            StartCoroutine(BuryRoutine(interactor, carry));
        }

        private IEnumerator BuryRoutine(GameObject interactor, BodyCarry carry)
        {
            inProgress = true;
            progress = 0f;

            var stamina = interactor.GetComponentInChildren<StaminaSystem>();
            int ticksLeft = staminaConsumeTicks;
            float tickInterval = burySeconds / Mathf.Max(1, staminaConsumeTicks);
            float tickTimer = 0f;

            var corpseGo = carry.Carried != null ? carry.Carried.gameObject : null;
            var identity = corpseGo != null ? corpseGo.GetComponent<CompanionIdentity>() : null;

            while (progress < burySeconds)
            {
                progress += Time.deltaTime;
                tickTimer += Time.deltaTime;
                if (tickTimer >= tickInterval && ticksLeft > 0)
                {
                    tickTimer = 0f;
                    ticksLeft--;
                    stamina?.ConsumeJump();
                }
                yield return null;
            }

            carry.ClearCarriedReference();
            if (corpseGo != null) Destroy(corpseGo);

            BuildGraveMarker();
            inProgress = false;
            HasBuried = true;
            if (identity != null)
            {
                BuriedCompanionId = identity.Id;
                OnBurialComplete?.Invoke(identity.Id);
            }
        }

        /// <summary>Save/load restore only — recreates the visual grave marker and marks this zone
        /// used, without re-running the timed interaction or firing OnBurialComplete again (the
        /// morale recovery already applied last session).</summary>
        public void RestoreBuried(CompanionId companionId)
        {
            BuriedCompanionId = companionId;
            if (HasBuried) return;
            HasBuried = true;
            BuildGraveMarker();
        }

        private void BuildGraveMarker()
        {
            var root = new GameObject("GraveMarker");
            root.transform.SetParent(transform, false);
            root.transform.position = transform.position;

            var vertical = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vertical.transform.SetParent(root.transform, false);
            vertical.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
            vertical.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            var horizontal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horizontal.transform.SetParent(root.transform, false);
            horizontal.transform.localScale = new Vector3(0.6f, 0.1f, 0.1f);
            horizontal.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        }

        private BodyCarry FindNearestBodyCarry()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponent<BodyCarry>() : null;
        }

        private void OnGUI()
        {
            if (!inProgress) return;

            string text = $"Burying... {progress:0.0}/{burySeconds:0.0}s";
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            style.normal.textColor = Color.white;
            Rect rect = new Rect((Screen.width - 300f) * 0.5f, Screen.height * 0.6f, 300f, 30f);
            GUI.Label(rect, text, style);
        }
    }
}
