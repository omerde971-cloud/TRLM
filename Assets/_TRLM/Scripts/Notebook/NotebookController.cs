using UnityEngine;
using UnityEngine.InputSystem;
using TRLM.Player;

namespace TRLM.Notebook
{
    /// <summary>
    /// Opens/closes the Kehanet Defteri. Pause approach copies EquipmentWheelUI's precedent:
    /// Time.timeScale = 0 freezes AI/survival/world time with zero edits to those systems, while
    /// the notebook UI animates on unscaled time. Player control is cut by disabling
    /// PlayerInputHandler itself — its InputActions cancel on disable (MoveInput/LookInput zero
    /// out) and InteractPressed stops firing, so movement, camera and E-interactions all stop
    /// through the one input hub instead of touching each consumer. The toggle key is polled
    /// directly from Keyboard.current (the documented scoped exception pattern — see
    /// EquipmentWheelUI / CompanionCommandInput remarks) since it must work while that hub is off.
    /// Robust re-entry: prior timeScale and cursor state are stored exactly at Open and restored
    /// exactly at Close, so opening during an already-paused/cinematic state round-trips cleanly.
    /// </summary>
    [RequireComponent(typeof(NotebookUI))]
    public class NotebookController : MonoBehaviour
    {
        public static NotebookController Instance { get; private set; }

        [Tooltip("Toggle key. J is unbound in PlayerInputHandler (WASD/shift/ctrl/space/E/F/I/Tab/R/G/Esc are taken).")]
        [SerializeField] private Key toggleKey = Key.J;
        [SerializeField] private PlayerInputHandler playerInput;
        [Tooltip("Minimum unscaled seconds between toggles — blocks re-toggle spam / same-frame double fires.")]
        [SerializeField] private float toggleCooldown = 0.2f;

        private NotebookUI ui;
        private float lastToggleUnscaledTime = -999f;
        private float previousTimeScale = 1f;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool playerInputWasEnabled;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            Instance = this;
            ui = GetComponent<NotebookUI>();
            if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputHandler>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Time.unscaledTime keeps ticking at timeScale 0, so the cooldown works while paused.
            if (keyboard[toggleKey].wasPressedThisFrame && Time.unscaledTime - lastToggleUnscaledTime >= toggleCooldown)
            {
                lastToggleUnscaledTime = Time.unscaledTime;
                if (IsOpen) Close();
                else Open();
            }

            if (!IsOpen) return;

            // While open: Esc also closes (reads naturally as "put the book down"), arrows/A-D turn pages.
            if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) ui.StepPage(+1);
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) ui.StepPage(-1);
        }

        public void Open() => Open(null);

        /// <summary>Opens the notebook, optionally focused on a specific page (pickup flow).</summary>
        public void Open(ProphecyPage focus)
        {
            if (IsOpen)
            {
                if (focus != null) ui.FocusPage(focus);
                return;
            }
            IsOpen = true;

            // Store the EXACT prior state (even timeScale 0 / an unlocked cursor from a cinematic)
            // so Close() puts the world back precisely as it found it.
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (playerInput != null)
            {
                playerInputWasEnabled = playerInput.enabled;
                playerInput.enabled = false; // actions cancel -> move/look zero, events silent
            }

            ui.Show(focus);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;

            if (playerInput != null && playerInputWasEnabled)
                playerInput.enabled = true;

            ui.Hide();
        }

        // Safety: never leave the game frozen/inputless if this object dies while open
        // (scene unload mid-read, external Destroy).
        private void OnDisable()
        {
            if (IsOpen) Close();
        }
    }
}
