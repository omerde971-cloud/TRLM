using UnityEngine;
using UnityEngine.SceneManagement;

namespace TRLM.Flow
{
    /// <summary>
    /// Single production scene-transition gate. This keeps scene loads traceable, debounced, and
    /// constrained to authored flow instead of scattered direct SceneManager calls.
    /// </summary>
    public static class SceneFlow
    {
        public const string MainMenuScene = "00_MainMenu";
        public const string IslandScene = "20_Island_Blockout";
        public const string RetiredNeighborhoodOpeningScene = "05_Neighborhood_Cinematic";

        private static bool transitionInProgress;
        private static int transitionCounter;
        private static string currentTransitionId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            transitionInProgress = false;
            transitionCounter = 0;
            currentTransitionId = null;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        public static bool RequestLoad(string sceneName, string reason, Object caller = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Log("INVALID", sceneName, reason, caller, "Rejected empty scene name.");
                return false;
            }

            string from = SceneManager.GetActiveScene().name;
            if (transitionInProgress)
            {
                Log(from, sceneName, reason, caller, "Rejected: transition already in progress.");
                return false;
            }

            if (sceneName == from)
            {
                Log(from, sceneName, reason, caller, "Ignored: target is already active.");
                return false;
            }

            if (sceneName == RetiredNeighborhoodOpeningScene)
            {
                Log(from, sceneName, reason, caller, "Rejected: retired opening scene is not part of production flow.");
                return false;
            }

            transitionInProgress = true;
            currentTransitionId = $"TR-{++transitionCounter:0000}";
            Log(from, sceneName, reason, caller, "Requested.");
            SceneManager.LoadScene(sceneName);
            return true;
        }

        private static void HandleActiveSceneChanged(Scene from, Scene to)
        {
            Log(from.name, to.name, "ActiveSceneChanged", null, "Completed.");
            transitionInProgress = false;
            currentTransitionId = null;
        }

        private static void Log(string from, string to, string reason, Object caller, string status)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string callerName = caller != null ? $"{caller.GetType().Name}:{caller.name}" : "none";
            string id = string.IsNullOrEmpty(currentTransitionId) ? "none" : currentTransitionId;
            Debug.Log($"[TRLM SceneFlow]\nFROM: {from}\nTO: {to}\nREASON: {reason}\nCALLER / TRANSITION ID: {callerName} / {id}\nTIMESTAMP: {Time.realtimeSinceStartup:0.000}\nSTATUS: {status}");
#endif
        }
    }
}
