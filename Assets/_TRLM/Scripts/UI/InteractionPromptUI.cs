using UnityEngine;
using TRLM.Interaction;

namespace TRLM.UI
{
    /// <summary>Minimal "E — Interact" prompt, shown only while looking at a valid IInteractable.</summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private InteractionOrigin interactionOrigin;

        private GUIStyle style;
        private string cachedPrompt;
        private string cachedText = "";

        private void OnGUI()
        {
            if (interactionOrigin == null || !interactionOrigin.HasTarget) return;

            style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                normal = { textColor = Color.white }
            };

            // Rebuild only when the prompt changes — OnGUI runs 2+ times per frame.
            if (!ReferenceEquals(interactionOrigin.CurrentPrompt, cachedPrompt))
            {
                cachedPrompt = interactionOrigin.CurrentPrompt;
                cachedText = "E — " + cachedPrompt;
            }
            string text = cachedText;
            float width = 300f;
            Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.65f, width, 30f);
            GUI.Label(rect, text, style);
        }
    }
}
