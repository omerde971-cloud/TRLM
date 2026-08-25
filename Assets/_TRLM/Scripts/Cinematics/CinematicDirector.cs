using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using TRLM.Dialogue;
using TRLM.Player;
using TRLM.Progression;
using TRLM.Story;
using TRLM.UI;

namespace TRLM.Cinematics
{
    /// <summary>
    /// Reusable per-beat cinematic driver (cave entrance, weapon discovery, notebook discovery...)
    /// — the general system the one-off OpeningCinematicController predates. One CinematicDirector
    /// per story beat; something (usually a CinematicTrigger volume, or any UnityEvent/script)
    /// calls Play().
    ///
    /// Camera handoff follows Cinemachine best practice: the gameplay camera and CinemachineBrain
    /// are never disabled — this component only RAISES the priority of its authored
    /// CinemachineCameras so the Brain blends to them, and lowers them back on completion so the
    /// Brain blends home. That guarantees no duplicate active cameras and a seamless return.
    ///
    /// Player control is cut the NotebookController way: disable PlayerInputHandler itself, so
    /// movement/look/interact all stop through the one input hub. The OnGUI GameplayHUD is hidden
    /// by disabling its component. Both are restored on ANY exit path — normal completion, Skip(),
    /// or the component being disabled/destroyed mid-play (scene unload) — so control always
    /// returns.
    ///
    /// Content can be a Timeline (completion = director.stopped, never a blind timer) or, before a
    /// Timeline is authored, a fallback coroutine: authored DialogueLines pushed through
    /// DialogueSystem (real subtitle timing) over fallbackDuration seconds. Play-once is backed by
    /// StoryFlags under cinematicId, which persists via StoryFlagsPersistence — reloads skip
    /// already-seen beats.
    /// </summary>
    public class CinematicDirector : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("StoryFlags id for play-once tracking (persisted). Example: cine_cave_entrance.")]
        [SerializeField] private string cinematicId;
        [SerializeField] private bool playOnce = true;

        [Header("Content")]
        [Tooltip("Optional Timeline. When set, completion is driven by director.stopped; when null, the fallback path (fallbackDuration + fallbackLines) runs instead.")]
        [SerializeField] private PlayableDirector director;
        [Tooltip("Fallback-path length in seconds when no PlayableDirector is set.")]
        [SerializeField] private float fallbackDuration = 6f;
        [Tooltip("Fallback-path subtitles/VO, queued through DialogueSystem with its real timing. A Timeline should use Signals instead.")]
        [SerializeField] private DialogueLine[] fallbackLines;

        [Header("Cameras (Cinemachine)")]
        [Tooltip("Cinemachine cameras for this beat. Their priority is raised during the cinematic and restored after — the gameplay camera/Brain is never disabled.")]
        [SerializeField] private CinemachineCamera[] cinematicCameras;
        [Tooltip("Priority applied to the cameras above while the cinematic runs. Must exceed the gameplay camera's priority.")]
        [SerializeField] private int activePriority = 100;

        [Header("Gameplay handoff")]
        [SerializeField] private bool disablePlayerControl = true;
        [SerializeField] private bool hideHud = true;
        [Tooltip("Auto-found when left null.")]
        [SerializeField] private PlayerInputHandler playerInput;
        [Tooltip("Auto-found when left null.")]
        [SerializeField] private GameplayHUD gameplayHud;

        [Header("On complete")]
        [SerializeField] private bool doAdvanceObjective;
        [SerializeField] private ObjectiveStep advanceObjectiveOnComplete;

        [Header("Events")]
        [SerializeField] private UnityEvent onCinematicStart;
        [SerializeField] private UnityEvent onCinematicEnd;

        public bool IsPlaying { get; private set; }

        /// <summary>True when playOnce is on and StoryFlags already has this beat's id —
        /// CinematicTrigger consults this to avoid pointless Play() calls after a reload.</summary>
        public bool HasAlreadyPlayed =>
            playOnce && StoryFlags.Instance != null && StoryFlags.Instance.Has(cinematicId);

        // Captured at Play() so every exit path restores EXACTLY the pre-cinematic state
        // (a camera left disabled in the scene stays disabled after, etc.).
        private int[] previousPriorities;
        private bool[] previousCameraActive;
        private bool playerInputWasEnabled;
        private bool hudWasEnabled;
        private bool gameplayStateCaptured;
        private Coroutine fallbackRoutine;

        private void Awake()
        {
            if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputHandler>();
            if (gameplayHud == null) gameplayHud = FindFirstObjectByType<GameplayHUD>();
        }

        /// <summary>Starts the beat. Idempotent while running; a play-once beat that already fired
        /// (this session or in a restored save) skips straight to onCinematicEnd with gameplay
        /// state untouched, so triggers can call this blindly after a reload.</summary>
        public void Play()
        {
            if (IsPlaying) return;

            if (HasAlreadyPlayed)
            {
                onCinematicEnd?.Invoke();
                return;
            }

            IsPlaying = true;
            CaptureAndSuspendGameplayState();
            RaiseCameras();
            onCinematicStart?.Invoke();

            if (director != null)
            {
                director.stopped += HandleDirectorStopped;
                director.Play();
            }
            else
            {
                fallbackRoutine = StartCoroutine(RunFallback());
            }
        }

        /// <summary>Ends the beat early (skip button, debug). Safe no-op while not playing.</summary>
        public void Skip()
        {
            if (!IsPlaying) return;

            if (director != null)
            {
                director.Stop(); // fires director.stopped -> Complete()
            }
            else
            {
                if (fallbackRoutine != null) StopCoroutine(fallbackRoutine);
                fallbackRoutine = null;
                Complete();
            }
        }

        // ---------------------------------------------------------------- Completion

        private void HandleDirectorStopped(PlayableDirector _) => Complete();

        private IEnumerator RunFallback()
        {
            if (fallbackLines != null && DialogueSystem.Instance != null)
            {
                for (int i = 0; i < fallbackLines.Length; i++)
                    DialogueSystem.Instance.Play(fallbackLines[i]);
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, fallbackDuration));

            // Let a subtitle queue longer than fallbackDuration finish reading before handoff —
            // Complete()'s StopAndClear would otherwise cut authored lines mid-sentence.
            while (DialogueSystem.Instance != null && DialogueSystem.Instance.IsSpeaking)
                yield return null;

            fallbackRoutine = null;
            Complete();
        }

        /// <summary>The single normal-exit path: seamless handoff back to gameplay, then the
        /// one-time consequences (flag, objective, end event).</summary>
        private void Complete()
        {
            if (!IsPlaying) return;
            IsPlaying = false;

            if (director != null) director.stopped -= HandleDirectorStopped;

            RestoreGameplayState();

            // Drop any still-queued cinematic lines so they don't bleed into gameplay barks.
            DialogueSystem.Instance?.StopAndClear();

            StoryFlags.Instance?.Set(cinematicId);
            if (doAdvanceObjective) ObjectiveSystem.Instance?.AdvanceTo(advanceObjectiveOnComplete);

            onCinematicEnd?.Invoke();
        }

        // ---------------------------------------------------------------- Gameplay state handoff

        private void CaptureAndSuspendGameplayState()
        {
            if (disablePlayerControl && playerInput != null)
            {
                playerInputWasEnabled = playerInput.enabled;
                playerInput.enabled = false; // NotebookController precedent: actions cancel, events go silent
            }

            if (hideHud && gameplayHud != null)
            {
                hudWasEnabled = gameplayHud.enabled;
                gameplayHud.enabled = false; // OnGUI stops rendering
            }

            gameplayStateCaptured = true;
        }

        private void RaiseCameras()
        {
            if (cinematicCameras == null || cinematicCameras.Length == 0) return;

            previousPriorities = new int[cinematicCameras.Length];
            previousCameraActive = new bool[cinematicCameras.Length];
            for (int i = 0; i < cinematicCameras.Length; i++)
            {
                var cam = cinematicCameras[i];
                if (cam == null) continue;
                previousPriorities[i] = cam.Priority;
                previousCameraActive[i] = cam.gameObject.activeSelf;
                cam.gameObject.SetActive(true);
                cam.Priority = activePriority; // Brain blends TO this camera
            }
        }

        /// <summary>Restores cameras/input/HUD exactly as found. Idempotent — every exit path
        /// (Complete, Skip, OnDisable, OnDestroy) funnels through here and the captured-state
        /// flags make repeat calls harmless.</summary>
        private void RestoreGameplayState()
        {
            if (cinematicCameras != null && previousPriorities != null)
            {
                for (int i = 0; i < cinematicCameras.Length; i++)
                {
                    var cam = cinematicCameras[i];
                    if (cam == null) continue;
                    cam.Priority = previousPriorities[i]; // Brain blends BACK to gameplay camera
                    cam.gameObject.SetActive(previousCameraActive[i]);
                }
                previousPriorities = null;
                previousCameraActive = null;
            }

            if (!gameplayStateCaptured) return;
            gameplayStateCaptured = false;

            if (disablePlayerControl && playerInput != null && playerInputWasEnabled)
                playerInput.enabled = true;

            if (hideHud && gameplayHud != null && hudWasEnabled)
                gameplayHud.enabled = true;
        }

        // Safety net: never leave the player inputless/HUD-less/camera-hijacked if this object is
        // disabled or destroyed mid-cinematic (scene unload, external Destroy). Deliberately does
        // NOT set the play-once flag or advance the objective — an interrupted beat may replay.
        private void OnDisable() => Abort();
        private void OnDestroy() => Abort();

        private void Abort()
        {
            if (!IsPlaying) return;
            IsPlaying = false;

            if (director != null)
            {
                director.stopped -= HandleDirectorStopped;
                director.Stop();
            }
            if (fallbackRoutine != null)
            {
                StopCoroutine(fallbackRoutine);
                fallbackRoutine = null;
            }

            RestoreGameplayState();
            DialogueSystem.Instance?.StopAndClear();
            onCinematicEnd?.Invoke();
        }
    }
}
