using System.Collections.Generic;
using UnityEngine;
using TRLM.Dialogue;

namespace TRLM.Characters
{
    /// <summary>
    /// Static lookup from DialogueSpeaker to the in-scene DialogueActor embodying that character.
    /// Exists so any actor can cheaply find "where is the current speaker's head" for listener
    /// look-at without scene searches. Duplicate registrations for the same speaker are tolerated
    /// (last one wins, with a warning) so a respawned/reloaded companion doesn't hard-break dialogue.
    /// </summary>
    public static class DialogueActorRegistry
    {
        private static readonly Dictionary<DialogueSpeaker, DialogueActor> bySpeaker =
            new Dictionary<DialogueSpeaker, DialogueActor>();

        // Kept as a flat list (not dictionary values) so per-frame nearest-other scans allocate nothing.
        private static readonly List<DialogueActor> all = new List<DialogueActor>();

        /// <summary>All currently registered actors. Do not cache across frames if actors can despawn.</summary>
        public static IReadOnlyList<DialogueActor> All => all;

        public static void Register(DialogueActor actor)
        {
            if (actor == null) return;

            if (bySpeaker.TryGetValue(actor.Speaker, out var existing) && existing != null && existing != actor)
            {
                Debug.LogWarning($"[DialogueActorRegistry] Duplicate DialogueActor for speaker '{actor.Speaker}': " +
                                 $"'{existing.name}' replaced by '{actor.name}' (last wins).", actor);
                all.Remove(existing);
            }

            bySpeaker[actor.Speaker] = actor;
            if (!all.Contains(actor)) all.Add(actor);
        }

        public static void Unregister(DialogueActor actor)
        {
            if (actor == null) return;
            if (bySpeaker.TryGetValue(actor.Speaker, out var existing) && existing == actor)
                bySpeaker.Remove(actor.Speaker);
            all.Remove(actor);
        }

        public static bool TryGet(DialogueSpeaker speaker, out DialogueActor actor)
        {
            return bySpeaker.TryGetValue(speaker, out actor) && actor != null;
        }

        /// <summary>Head transform of the actor voicing <paramref name="speaker"/>, or null when that
        /// speaker has no registered body in the scene (e.g. Narration).</summary>
        public static Transform GetSpeakerHead(DialogueSpeaker speaker)
        {
            return TryGet(speaker, out var actor) ? actor.HeadOrRoot : null;
        }
    }
}
