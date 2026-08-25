using UnityEngine;
using TRLM.Dialogue;

namespace TRLM.Notebook
{
    /// <summary>
    /// One authored page/fragment left behind by the predecessors — a loose leaf the team folds
    /// into the Kehanet Defteri. Deliberately a ScriptableObject (authored content, shared by the
    /// world pickup and the notebook UI), NOT a scene object: the same page asset is referenced by
    /// exactly one ProphecyPagePickup in the world and by ProphecyNotebook's master catalog.
    /// Follows the project's Turkish-first / English-subtitle text convention.
    /// </summary>
    [CreateAssetMenu(menuName = "TRLM/Prophecy Page", fileName = "ProphecyPage_")]
    public class ProphecyPage : ScriptableObject
    {
        [Tooltip("Stable unique id — referenced by save data, never rename after shipping a save.")]
        public string id;

        [Header("Title")]
        public string titleTurkish;
        public string titleEnglish;

        [Header("Body (Turkish first, English translation below — project convention)")]
        [TextArea(3, 8)] public string bodyTurkish;
        [TextArea(3, 8)] public string bodyEnglish;

        [Header("Presentation")]
        [Tooltip("Optional symbol/drawing shown above the text. Null = text-only page.")]
        public Sprite illustration;
        [Tooltip("Position in the notebook — pages render sorted by this, not by discovery order.")]
        public int orderIndex;
        [Tooltip("Marks the page as a central prophecy fragment (can gate objective advancement).")]
        public bool isKeyProphecy;

        [Header("Discovery reaction (optional)")]
        [Tooltip("Played through DialogueSystem the first time this page is collected. Leave text empty for none.")]
        public DialogueLine discoveryLine;

        /// <summary>True when the optional discovery line actually has content to speak/subtitle.</summary>
        public bool HasDiscoveryLine =>
            discoveryLine != null &&
            (!string.IsNullOrEmpty(discoveryLine.turkishText) || !string.IsNullOrEmpty(discoveryLine.englishSubtitle));

#if UNITY_EDITOR
        // Authoring guard only — never regenerates an id an asset already has (save data references it).
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString("N");
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
