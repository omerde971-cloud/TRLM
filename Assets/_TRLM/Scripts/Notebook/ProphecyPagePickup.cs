using UnityEngine;
using TRLM.Core;
using TRLM.Interaction;

namespace TRLM.Notebook
{
    /// <summary>
    /// A loose predecessor page lying in the world. Press E -> the page joins the Kehanet Defteri
    /// and the world object deactivates. Persistence follows the PickupItem convention exactly:
    /// "collected" == the GameObject is inactive, identified by its PersistentObjectId — captured
    /// by NotebookStatePersistence scanning inactive pickups, restored by SetActive(false). A
    /// belt-and-braces Start() check also self-deactivates when the notebook already has the page
    /// (covers a duplicate-placed pickup of the same page asset).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PersistentObjectId))]
    public class ProphecyPagePickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ProphecyPage page;
        [Tooltip("Open the notebook focused on the new page right after collecting — the 'read what you just found' beat.")]
        [SerializeField] private bool openNotebookOnCollect = true;

        private string cachedPrompt; // built once — InteractionOrigin reads InteractionPrompt often

        public ProphecyPage Page => page;

        private void Awake()
        {
            // Turkish-first prompt per project text convention.
            cachedPrompt = page != null && !string.IsNullOrEmpty(page.titleTurkish)
                ? $"Sayfayı Al — {page.titleTurkish}"
                : "Sayfayı Al (Take Page)";
        }

        private void Start()
        {
            if (page != null && ProphecyNotebook.Instance != null && ProphecyNotebook.Instance.HasPage(page.id))
                gameObject.SetActive(false);
        }

        public string InteractionPrompt => cachedPrompt;

        public void Interact(GameObject interactor)
        {
            if (page == null)
            {
                Debug.LogWarning($"[ProphecyPagePickup] '{name}' has no ProphecyPage assigned.", this);
                return;
            }

            var notebook = ProphecyNotebook.Instance;
            if (notebook == null)
            {
                Debug.LogWarning("[ProphecyPagePickup] No ProphecyNotebook in scene — page not collected.", this);
                return;
            }

            notebook.Collect(page); // false = already had it; either way the world copy goes away

            // Inactive-as-collected: NotebookStatePersistence captures this via PersistentObjectId,
            // and PersistentObjectId's registry deliberately survives deactivation (see its remarks).
            gameObject.SetActive(false);

            if (openNotebookOnCollect && NotebookController.Instance != null)
                NotebookController.Instance.Open(page);
        }
    }
}
