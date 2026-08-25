using System;
using System.Collections.Generic;
using UnityEngine;

namespace TRLM.Dialogue
{
    /// <summary>
    /// Scene-persistent-free singleton (one per gameplay/cinematic scene, like GameplayHUD) that
    /// owns subtitle timing/priority/queueing. SubtitleUI is the only listener that needs to exist
    /// for lines to be readable — audio playback is opportunistic (plays if the line has a clip,
    /// silently timed otherwise), so this works today with every audioClip null and keeps working
    /// once ElevenLabs VO is dropped in later with zero code changes.
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        public static DialogueSystem Instance { get; private set; }

        [Header("Timing")]
        [Tooltip("Characters per second used to estimate a line's on-screen duration when it has no AudioClip and no durationOverride.")]
        [SerializeField] private float readingCharsPerSecond = 16f;
        [SerializeField] private float minLineSeconds = 1.5f;
        [SerializeField] private float maxLineSeconds = 12f;
        [Tooltip("Silent gap between two queued lines so subtitles don't visually run together.")]
        [SerializeField] private float gapBetweenLines = 0.35f;

        [SerializeField] private AudioSource voiceSource;

        public event Action<DialogueLine> OnLineStarted;
        public event Action<DialogueLine> OnLineEnded;

        public DialogueLine CurrentLine { get; private set; }
        public bool IsSpeaking => CurrentLine != null;

        private readonly List<DialogueLine> queue = new List<DialogueLine>();
        private readonly HashSet<string> playedOneShots = new HashSet<string>();

        /// <summary>Save support: ids of one-shot lines that already played. Enumerated at capture
        /// time; seeded back on restore so reloading doesn't replay story barks.</summary>
        public System.Collections.Generic.IEnumerable<string> PlayedOneShotIds => playedOneShots;

        /// <summary>Save support: re-mark previously played one-shot lines after a load.</summary>
        public void SeedPlayedOneShots(System.Collections.Generic.IEnumerable<string> ids)
        {
            if (ids == null) return;
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id)) playedOneShots.Add(id);
        }
        private float lineTimer;
        private float gapTimer;

        private void Awake()
        {
            Instance = this;
            if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f; // dialogue is a UI-adjacent concern, not positional, by default
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Queues (or immediately interrupts into) a line. Returns false only when the
        /// line is a one-shot that already played this session.</summary>
        public bool Play(DialogueLine line)
        {
            if (line == null) return false;
            if (line.oneShot && !string.IsNullOrEmpty(line.id) && playedOneShots.Contains(line.id)) return false;

            if (CurrentLine == null)
            {
                StartLine(line);
                return true;
            }

            if (line.subtitlePriority > CurrentLine.subtitlePriority)
            {
                // Higher-priority line interrupts — the interrupted line goes back to the front of
                // the queue only if it was itself mid-utterance and not one-shot-consumed yet.
                queue.Insert(0, CurrentLine);
                StartLine(line);
                return true;
            }

            queue.Add(line);
            return true;
        }

        public bool Play(IEnumerable<DialogueLine> lines)
        {
            bool any = false;
            foreach (var l in lines) any |= Play(l);
            return any;
        }

        /// <summary>Cuts the current line and drops the queue — for a cinematic Timeline that owns
        /// its own exact timing and doesn't want DialogueSystem's queue bleeding into the next beat.</summary>
        public void StopAndClear()
        {
            EndCurrentLine();
            queue.Clear();
            gapTimer = 0f;
        }

        private void Update()
        {
            if (CurrentLine != null)
            {
                lineTimer -= Time.deltaTime;
                if (lineTimer <= 0f) EndCurrentLine();
                return;
            }

            if (queue.Count == 0) return;

            if (gapTimer > 0f)
            {
                gapTimer -= Time.deltaTime;
                return;
            }

            var next = queue[0];
            queue.RemoveAt(0);
            StartLine(next);
        }

        private void StartLine(DialogueLine line)
        {
            CurrentLine = line;
            lineTimer = ResolveDuration(line);

            if (line.audioClip != null)
            {
                voiceSource.clip = line.audioClip;
                voiceSource.Play();
            }

            OnLineStarted?.Invoke(line);
        }

        private void EndCurrentLine()
        {
            if (CurrentLine == null) return;

            if (!string.IsNullOrEmpty(CurrentLine.id)) playedOneShots.Add(CurrentLine.id);
            if (voiceSource.isPlaying && voiceSource.clip == CurrentLine.audioClip) voiceSource.Stop();

            var ended = CurrentLine;
            CurrentLine = null;
            gapTimer = gapBetweenLines;
            OnLineEnded?.Invoke(ended);
        }

        private float ResolveDuration(DialogueLine line)
        {
            if (line.durationOverride > 0f) return line.durationOverride;
            if (line.audioClip != null) return line.audioClip.length;

            string text = string.IsNullOrEmpty(line.englishSubtitle) ? line.turkishText : line.englishSubtitle;
            float estimate = string.IsNullOrEmpty(text) ? minLineSeconds : text.Length / Mathf.Max(1f, readingCharsPerSecond);
            return Mathf.Clamp(estimate, minLineSeconds, maxLineSeconds);
        }
    }
}
