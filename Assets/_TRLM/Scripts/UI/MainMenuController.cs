using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TRLM.Save;
using TRLM.Progression;
using TRLM.Flow;

namespace TRLM.UI
{
    /// <summary>
    /// Drives the Stitch "Obsidian Path" main menu (00_MainMenu.unity). Continue/Load Game read
    /// SaveManager slot metadata directly — no SaveOrchestrator exists in this scene, since that
    /// component needs gameplay-scene references (PlayerStatePersistence etc.). Restoration happens
    /// via PendingLoad + SaveOrchestrator.Start in whichever scene the save/new game targets.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Nav Buttons")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private CanvasGroup loadGamePanel;
        [SerializeField] private CanvasGroup settingsPanel;
        [SerializeField] private CanvasGroup creditsPanel;
        [SerializeField] private Button loadGameBackButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private Button creditsBackButton;

        [Header("Load Game Slots (Autosave, Manual 1..5 — must match SaveManager slot order)")]
        [SerializeField] private Transform slotRowsContainer;

        [Header("Flow")]
        [SerializeField] private string newGameSceneName = SceneFlow.IslandScene;
        [SerializeField] private string fallbackContinueSceneName = "20_Island_Blockout";

        private readonly string[] slotIds = new string[6];
        private SlotRow[] rows;

        private class SlotRow
        {
            public Button button;
            public Text label;
            public Text detail;
        }

        private void Awake()
        {
            slotIds[0] = SaveManager.AutosaveSlotId;
            for (int i = 1; i <= 5; i++) slotIds[i] = SaveManager.ManualSlotId(i);

            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(OnOpenLoadGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnOpenSettings);
            if (creditsButton != null) creditsButton.onClick.AddListener(OnOpenCredits);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

            if (loadGameBackButton != null) loadGameBackButton.onClick.AddListener(() => ClosePanel(loadGamePanel, loadGameButton));
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(() => ClosePanel(settingsPanel, settingsButton));
            if (creditsBackButton != null) creditsBackButton.onClick.AddListener(() => ClosePanel(creditsPanel, creditsButton));

            BuildSlotRows();
        }

        private void Start()
        {
            bool hasContinue = SaveManager.HasContinueSave();
            if (continueButton != null)
            {
                continueButton.interactable = hasContinue;
                var colors = continueButton.colors;
                colors.disabledColor = new Color(1f, 1f, 1f, 0.25f);
                continueButton.colors = colors;
            }

            SetPanel(loadGamePanel, false);
            SetPanel(settingsPanel, false);
            SetPanel(creditsPanel, false);
            SelectFirstMenuButton(hasContinue);
        }

        // ---------------------------------------------------------------- Nav actions

        private void OnContinue()
        {
            string slot = SaveManager.GetMostRecentContinueSave();
            if (slot == null) return;
            LoadSlot(slot);
        }

        private void OnNewGame()
        {
            PendingLoad.NewGameRequested = true;
            PendingLoad.NewGameDifficulty = DifficultyLevel.Normal;
            SceneFlow.RequestLoad(newGameSceneName, "NewGameFromMainMenu", this);
        }

        private void OnOpenLoadGame()
        {
            RefreshSlotRows();
            SetPanel(loadGamePanel, true);
            SelectFirstAvailableLoadRow();
        }

        private void OnOpenSettings()
        {
            SetPanel(settingsPanel, true);
            SelectButton(settingsBackButton);
        }

        private void OnOpenCredits()
        {
            SetPanel(creditsPanel, true);
            SelectButton(creditsBackButton);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void LoadSlot(string slotId)
        {
            var meta = SaveManager.ReadMetadata(slotId);
            string scene = (meta != null && !string.IsNullOrEmpty(meta.sceneName)) ? meta.sceneName : fallbackContinueSceneName;
            if (scene == SceneFlow.RetiredNeighborhoodOpeningScene)
                scene = fallbackContinueSceneName;
            PendingLoad.RequestedSlotId = slotId;
            SceneFlow.RequestLoad(scene, $"LoadSlot:{slotId}", this);
        }

        // ---------------------------------------------------------------- Load Game panel

        private void BuildSlotRows()
        {
            if (slotRowsContainer == null) return;
            int count = Mathf.Min(slotRowsContainer.childCount, slotIds.Length);
            rows = new SlotRow[count];

            for (int i = 0; i < count; i++)
            {
                var child = slotRowsContainer.GetChild(i);
                var row = new SlotRow
                {
                    button = child.GetComponent<Button>(),
                    label = child.Find("Label")?.GetComponent<Text>(),
                    detail = child.Find("Detail")?.GetComponent<Text>(),
                };
                rows[i] = row;

                string slotId = slotIds[i];
                if (row.button != null) row.button.onClick.AddListener(() => LoadSlot(slotId));
            }
        }

        private void RefreshSlotRows()
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;

                var meta = SaveManager.ReadMetadata(slotIds[i]);
                bool exists = meta != null;

                if (row.button != null) row.button.interactable = exists;

                string kind = slotIds[i] == SaveManager.AutosaveSlotId ? "Autosave" : $"Slot {i}";
                string detail = exists
                    ? $"Day {meta.dayCount} — {FormatPlaytime(meta.totalPlaytimeSeconds)} — {(string.IsNullOrEmpty(meta.regionName) ? "Unknown region" : meta.regionName)}"
                    : "Empty";

                if (row.detail != null)
                {
                    if (row.label != null) row.label.text = kind;
                    row.detail.text = detail;
                }
                else if (row.label != null)
                {
                    // No separate detail Text in this row's hierarchy — fold both into the one label.
                    row.label.text = $"{kind} — {detail}";
                }
            }
        }

        private static string FormatPlaytime(float seconds)
        {
            int totalMinutes = Mathf.FloorToInt(seconds / 60f);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return $"{hours}h {minutes:00}m";
        }

        // ---------------------------------------------------------------- Panels

        private void SetPanel(CanvasGroup panel, bool visible)
        {
            if (panel == null) return;
            panel.alpha = visible ? 1f : 0f;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
            panel.gameObject.SetActive(visible);
        }

        private void ClosePanel(CanvasGroup panel, Button returnFocus)
        {
            SetPanel(panel, false);
            SelectButton(returnFocus);
        }

        private void SelectFirstMenuButton(bool hasContinue)
        {
            SelectButton(hasContinue ? continueButton : newGameButton);
        }

        private void SelectFirstAvailableLoadRow()
        {
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    if (row?.button != null && row.button.interactable)
                    {
                        SelectButton(row.button);
                        return;
                    }
                }
            }

            SelectButton(loadGameBackButton);
        }

        private static void SelectButton(Button button)
        {
            if (button == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
