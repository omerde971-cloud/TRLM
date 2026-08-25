using UnityEngine;
using UnityEngine.UI;
using TRLM.Dialogue;

namespace TRLM.UI
{
    /// <summary>
    /// Production subtitle presentation: lower-center, cinematic-safe, restrained dark backing so
    /// text stays readable over any background. Purely a view over DialogueSystem's events — it
    /// owns no timing or queue logic itself, so cinematics/exploration/banter all render identically.
    /// Speaker name is shown only when the line has a resolvable one (Narration/Unknown are omitted).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SubtitleUI : MonoBehaviour
    {
        [SerializeField] private DialogueSystem dialogueSystem;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text lineText;
        [SerializeField] private Image backing;
        [SerializeField] private float fadeSeconds = 0.2f;

        private CanvasGroup group;
        private float fadeTarget;
        private float fadeVelocity;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private void OnEnable()
        {
            if (dialogueSystem == null) dialogueSystem = DialogueSystem.Instance ?? FindFirstObjectByType<DialogueSystem>();
            if (dialogueSystem == null) return;
            dialogueSystem.OnLineStarted += HandleLineStarted;
            dialogueSystem.OnLineEnded += HandleLineEnded;
        }

        private void OnDisable()
        {
            if (dialogueSystem == null) return;
            dialogueSystem.OnLineStarted -= HandleLineStarted;
            dialogueSystem.OnLineEnded -= HandleLineEnded;
        }

        private void Update()
        {
            group.alpha = Mathf.SmoothDamp(group.alpha, fadeTarget, ref fadeVelocity, fadeSeconds);
        }

        private void HandleLineStarted(DialogueLine line)
        {
            bool showSpeaker = line.speaker != DialogueSpeaker.Narration && line.speaker != DialogueSpeaker.Unknown;
            if (speakerText != null)
            {
                speakerText.gameObject.SetActive(showSpeaker);
                speakerText.text = showSpeaker ? line.speaker.ToString().ToUpperInvariant() : string.Empty;
            }

            if (lineText != null)
                lineText.text = string.IsNullOrEmpty(line.englishSubtitle) ? line.turkishText : line.englishSubtitle;

            fadeTarget = 1f;
        }

        private void HandleLineEnded(DialogueLine line)
        {
            fadeTarget = 0f;
        }
    }
}
