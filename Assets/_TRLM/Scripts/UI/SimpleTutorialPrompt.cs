using UnityEngine;

namespace TRLM.UI
{
    /// <summary>
    /// Tiny reusable "SPACE — Row" style tutorial banner, OnGUI-based to match DebugHUD's style.
    /// Exposes a static ShowGlobal helper so other systems (RowboatController, FlashlightController,
    /// etc.) can trigger a prompt without needing a scene reference wired up — mirrors the light
    /// singleton-style access WildlifeSpawnManager/ObjectiveSystem already use in this codebase.
    /// </summary>
    public class SimpleTutorialPrompt : MonoBehaviour
    {
        private static SimpleTutorialPrompt instance;

        private string text;
        private float remaining;
        private GUIStyle style;

        private void Awake()
        {
            if (instance == null) instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void Show(string message, float duration)
        {
            text = message;
            remaining = duration;
        }

        public static void ShowGlobal(string message, float duration)
        {
            instance?.Show(message, duration);
        }

        private void Update()
        {
            if (remaining > 0f) remaining -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (remaining <= 0f || string.IsNullOrEmpty(text)) return;

            style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                normal = { textColor = Color.yellow }
            };

            Rect rect = new Rect((Screen.width - 400f) * 0.5f, Screen.height * 0.75f, 400f, 30f);
            GUI.Label(rect, text, style);
        }
    }
}
