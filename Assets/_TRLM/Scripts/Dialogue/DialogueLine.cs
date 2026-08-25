using System;
using UnityEngine;

namespace TRLM.Dialogue
{
    public enum DialogueSpeaker { Elias, Mira, Jonah, Lena, Noah, Narration, Unknown }
    public enum DialogueEmotion { Neutral, Warm, Focused, Nervous, Playful, Determined, Uneasy, Urgent }

    /// <summary>Higher values interrupt lower ones already on screen; equal priority queues instead
    /// of interrupting. Cinematic/story beats should outrank incidental exploration banter.</summary>
    public enum SubtitlePriority { Ambient = 0, Banter = 10, Contextual = 20, Cinematic = 30, Critical = 40 }

    /// <summary>
    /// One authored spoken (or silently-subtitled) line. Deliberately a plain serializable class,
    /// not a ScriptableObject-per-line factory or a branching dialogue graph — TRLM Sprint 11 needs
    /// authored cinematic/exploration lines, not a dialogue-RPG framework. audioClip may be null
    /// (Turkish VO is generated externally via ElevenLabs after this sprint); DialogueSystem falls
    /// back to a reading-speed duration estimate from englishSubtitle when it's missing, so lines
    /// are fully playable/testable before any audio exists.
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        public string id;
        public DialogueSpeaker speaker = DialogueSpeaker.Unknown;
        [TextArea(1, 3)] public string turkishText;
        [TextArea(1, 3)] public string englishSubtitle;
        public DialogueEmotion emotion = DialogueEmotion.Neutral;
        [Tooltip("Short direction for future Turkish VO delivery, e.g. low, teasing, whisper, steady.")]
        public string delivery;
        [Tooltip("Scene key for VO manifests and later lookup. Example: 05_Neighborhood_Cinematic.")]
        public string scene;
        [Tooltip("Authored trigger/beat key. Example: opening_gear_load.")]
        public string trigger;
        public AudioClip audioClip;
        /// <summary>Seconds; 0 = auto (clip length, or reading-speed estimate if no clip).</summary>
        public float durationOverride = 0f;
        public SubtitlePriority subtitlePriority = SubtitlePriority.Contextual;
        /// <summary>If true, this line is skipped after its first successful play this session
        /// (further Play() calls for the same id become no-ops) — for beats that must not repeat,
        /// e.g. a first-sighting reaction line.</summary>
        public bool oneShot = false;
    }
}
