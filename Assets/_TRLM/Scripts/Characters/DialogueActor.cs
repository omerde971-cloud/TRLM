using System.Collections.Generic;
using UnityEngine;
using TRLM.Dialogue;

namespace TRLM.Characters
{
    /// <summary>
    /// Facial life for one speaking character: restrained viseme lip-sync (audio-amplitude driven
    /// when VO exists, procedural talk envelope when it doesn't), idle blinks/saccades, a subtle
    /// per-line emotion offset, and listener head look-at toward the current speaker. Layers on top
    /// of the Animator (blendshapes and bone rotations are written in LateUpdate) so authored body
    /// animation keeps playing underneath. All blendshape indices are discovered at runtime by name
    /// across every SkinnedMeshRenderer under the actor — Reallusion CC3+/CC4 naming is assumed but
    /// nothing is hardcoded and every shape is optional.
    /// </summary>
    public class DialogueActor : MonoBehaviour
    {
        [SerializeField] private DialogueSpeaker speaker = DialogueSpeaker.Unknown;

        [Header("Optional overrides (auto-found in children when empty)")]
        [SerializeField] private SkinnedMeshRenderer[] faceRenderers;
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform[] eyeBones;
        [Tooltip("AudioSource whose amplitude drives lip-sync. When empty, DialogueSystem's own voice source is used.")]
        [SerializeField] private AudioSource voiceSourceOverride;

        [Header("Lip-sync")]
        [Tooltip("Restrained cap on mouth-open blendshape weight (0..1). The brief forbids constant wide flapping.")]
        [Range(0.1f, 1f)] [SerializeField] private float maxOpenWeight = 0.7f;
        [SerializeField] private float amplitudeGain = 8f;
        [Tooltip("Seconds for the mouth to settle closed once the line ends.")]
        [SerializeField] private float mouthCloseTime = 0.1f;

        [Header("Look-at")]
        [Range(0f, 60f)] [SerializeField] private float maxHeadTurnDegrees = 35f;
        [SerializeField] private float lookWeightLerpSpeed = 3f;
        [SerializeField] private float lookRotationLerpSpeed = 5f;
        [Range(0f, 1f)] [SerializeField] private float eyeExtraWeight = 0.5f;

        /// <summary>One discovered blendshape channel on one renderer.</summary>
        private struct ShapeBinding
        {
            public SkinnedMeshRenderer renderer;
            public int index;
            public ShapeBinding(SkinnedMeshRenderer r, int i) { renderer = r; index = i; }
        }

        // Discovered shape groups (any may be empty — every write degrades gracefully).
        private ShapeBinding[] openShapes;      // V_Open
        private ShapeBinding[] jawShapes;       // Jaw_Open / Merged_Open_Mouth
        private ShapeBinding[] wideShapes;      // V_Wide
        private ShapeBinding[] tightOShapes;    // V_Tight_O
        private ShapeBinding[] blinkShapes;     // Eye_Blink / Eye_Blink_L / Eye_Blink_R
        private ShapeBinding[] browRaiseShapes; // Brow_Raise_Inner_*
        private ShapeBinding[] browDropShapes;  // Brow_Drop_*
        private ShapeBinding[] smileShapes;     // Mouth_Smile*

        // Runtime state (no per-frame allocation: everything below is preallocated).
        private readonly float[] audioBuffer = new float[512];
        private AudioSource systemVoiceSource;
        private bool subscribed;

        private DialogueLine observedLine;      // last CurrentLine reference we reacted to
        private bool isSpeaking;                // this actor owns the current line
        private float lineTime;                 // seconds since our line started
        private float lineSeed;                 // deterministic per-line variation
        private float mouthOpen;                // smoothed 0..1 openness driving visemes
        private float wideMix, tightMix;        // slow per-line viseme flavour

        private float blinkTimer;               // counts down to next blink
        private float blinkPhase = -1f;         // >=0 while a blink is in flight
        private const float BlinkDuration = 0.12f;

        private float saccadeTimer;
        private Vector3 saccadeOffset;          // small euler offset applied to eye bones
        private Vector3 saccadeCurrent;

        private float lookWeight;               // smoothed 0..1 look-at engagement
        private Quaternion lookDelta = Quaternion.identity; // smoothed world-space head aim delta

        private float emotionWeight;            // smoothed 0..1 for the emotion expression offset
        private DialogueEmotion activeEmotion = DialogueEmotion.Neutral;

        private Transform playerTransform;

        public DialogueSpeaker Speaker => speaker;

        /// <summary>Head bone when one was found, otherwise the actor root — always non-null while alive.</summary>
        public Transform HeadOrRoot => headBone != null ? headBone : transform;

        private void Awake()
        {
            AutoFindRig();
            DiscoverShapes();

            blinkTimer = Random.Range(2.5f, 6f);
            saccadeTimer = Random.Range(0.5f, 2f);

            // Player is an optional look-at fallback for a lone speaker; the tag may not exist in
            // this project, so probe defensively once.
            try
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }
            catch (UnityException) { /* tag not defined — fine */ }

            DialogueActorRegistry.Register(this);
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            DialogueActorRegistry.Unregister(this);
        }

        // ------------------------------------------------------------------ discovery

        private void AutoFindRig()
        {
            if (faceRenderers == null || faceRenderers.Length == 0)
            {
                var found = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var withShapes = new List<SkinnedMeshRenderer>(found.Length);
                foreach (var r in found)
                    if (r != null && r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0)
                        withShapes.Add(r);
                faceRenderers = withShapes.ToArray();
            }

            if (headBone == null || (eyeBones == null || eyeBones.Length == 0))
            {
                var eyes = new List<Transform>(2);
                var bones = GetComponentsInChildren<Transform>(true);
                foreach (var t in bones)
                {
                    if (t.GetComponent<Renderer>() != null) continue; // meshes named "Eye" are not bones
                    string n = t.name.ToLowerInvariant();
                    if (headBone == null && n.Contains("head") && !n.Contains("eye")) headBone = t;
                    if (n.Contains("eye") && !n.Contains("lash") && !n.Contains("lid") &&
                        !n.Contains("brow") && !n.Contains("occlusion")) eyes.Add(t);
                }
                if (eyeBones == null || eyeBones.Length == 0) eyeBones = eyes.ToArray();
            }
        }

        private void DiscoverShapes()
        {
            var open = new List<ShapeBinding>();
            var jaw = new List<ShapeBinding>();
            var wide = new List<ShapeBinding>();
            var tightO = new List<ShapeBinding>();
            var blink = new List<ShapeBinding>();
            var browR = new List<ShapeBinding>();
            var browD = new List<ShapeBinding>();
            var smile = new List<ShapeBinding>();

            if (faceRenderers != null)
            {
                foreach (var r in faceRenderers)
                {
                    if (r == null || r.sharedMesh == null) continue;
                    int count = r.sharedMesh.blendShapeCount;
                    for (int i = 0; i < count; i++)
                    {
                        string raw = r.sharedMesh.GetBlendShapeName(i);
                        // Strip a possible "MeshName." prefix so matching sees only the shape name.
                        int dot = raw.LastIndexOf('.');
                        string name = dot >= 0 ? raw.Substring(dot + 1) : raw;
                        var b = new ShapeBinding(r, i);

                        if (Is(name, "V_Open")) open.Add(b);
                        else if (Is(name, "Jaw_Open") || Is(name, "Merged_Open_Mouth")) jaw.Add(b);
                        else if (Is(name, "V_Wide")) wide.Add(b);
                        else if (Is(name, "V_Tight_O")) tightO.Add(b);
                        else if (Is(name, "Eye_Blink") || Is(name, "Eye_Blink_L") || Is(name, "Eye_Blink_R")) blink.Add(b);
                        else if (Has(name, "Brow_Raise_Inner")) browR.Add(b);
                        else if (Has(name, "Brow_Drop")) browD.Add(b);
                        else if (Is(name, "Mouth_Smile") || Is(name, "Mouth_Smile_L") || Is(name, "Mouth_Smile_R")) smile.Add(b);
                    }
                }
            }

            openShapes = open.ToArray();
            jawShapes = jaw.ToArray();
            wideShapes = wide.ToArray();
            tightOShapes = tightO.ToArray();
            blinkShapes = blink.ToArray();
            browRaiseShapes = browR.ToArray();
            browDropShapes = browD.ToArray();
            smileShapes = smile.ToArray();

            Debug.Log($"[DialogueActor:{speaker}] '{name}' discovered shapes — open:{openShapes.Length} " +
                      $"jaw:{jawShapes.Length} wide:{wideShapes.Length} tightO:{tightOShapes.Length} " +
                      $"blink:{blinkShapes.Length} browRaise:{browRaiseShapes.Length} browDrop:{browDropShapes.Length} " +
                      $"smile:{smileShapes.Length}; head:'{(headBone != null ? headBone.name : "none")}' " +
                      $"eyes:{(eyeBones != null ? eyeBones.Length : 0)} renderers:{(faceRenderers != null ? faceRenderers.Length : 0)}", this);
        }

        private static bool Is(string shapeName, string key)
        {
            return string.Equals(shapeName, key, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool Has(string shapeName, string key)
        {
            return shapeName.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ------------------------------------------------------------------ dialogue hookup

        private void TrySubscribe()
        {
            if (subscribed) return;
            var ds = DialogueSystem.Instance;
            if (ds == null) return; // Instance may arrive later — Update keeps trying

            ds.OnLineStarted += HandleLineStarted;
            ds.OnLineEnded += HandleLineEnded;
            systemVoiceSource = ds.GetComponent<AudioSource>(); // DialogueSystem guarantees one on itself
            subscribed = true;

            // Catch a line that started before we subscribed.
            if (ds.CurrentLine != null) HandleLineStarted(ds.CurrentLine);
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            var ds = DialogueSystem.Instance;
            if (ds != null)
            {
                ds.OnLineStarted -= HandleLineStarted;
                ds.OnLineEnded -= HandleLineEnded;
            }
            subscribed = false;
            isSpeaking = false;
            observedLine = null;
        }

        private void HandleLineStarted(DialogueLine line)
        {
            observedLine = line;
            if (line == null || line.speaker != speaker)
            {
                isSpeaking = false;
                return;
            }

            isSpeaking = true;
            lineTime = 0f;
            lineSeed = SeedFromId(line.id);
            activeEmotion = line.emotion;

            // Per-line viseme flavour so consecutive lines don't share one flapping pattern.
            wideMix = 0.15f + 0.25f * Frac(lineSeed * 0.731f);
            tightMix = 0.10f + 0.20f * Frac(lineSeed * 0.397f);
        }

        private void HandleLineEnded(DialogueLine line)
        {
            if (line != null && line.speaker == speaker) isSpeaking = false;
            if (observedLine == line) observedLine = null;
        }

        /// <summary>Deterministic per-line seed (FNV-1a) — believable variation without per-frame randomness.</summary>
        private static float SeedFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return 17.23f;
            uint hash = 2166136261u;
            for (int i = 0; i < id.Length; i++)
            {
                hash ^= id[i];
                hash *= 16777619u;
            }
            return (hash % 10000u) * 0.01f; // 0..100 range, stable per id
        }

        private static float Frac(float v) => v - Mathf.Floor(v);

        // ------------------------------------------------------------------ per-frame simulation

        private void Update()
        {
            if (!subscribed) TrySubscribe();

            float dt = Time.deltaTime;

            // Safety net: if the system was destroyed mid-line, stop talking.
            if (isSpeaking && (DialogueSystem.Instance == null || DialogueSystem.Instance.CurrentLine == null ||
                               DialogueSystem.Instance.CurrentLine.speaker != speaker))
                isSpeaking = false;

            UpdateMouthEnvelope(dt);
            UpdateBlink(dt);
            UpdateSaccade(dt);

            // Emotion offset fades in while we speak, out afterwards. Kept subtle.
            float emotionTarget = isSpeaking ? 1f : 0f;
            emotionWeight = Mathf.MoveTowards(emotionWeight, emotionTarget, dt * 2.5f);
        }

        private void UpdateMouthEnvelope(float dt)
        {
            if (!isSpeaking)
            {
                // Mouth must settle closed within ~mouthCloseTime once speech stops.
                mouthOpen = Mathf.MoveTowards(mouthOpen, 0f, dt / Mathf.Max(0.01f, mouthCloseTime));
                return;
            }

            lineTime += dt;
            float target;

            var src = ActiveVoiceSource();
            bool audioDriven = src != null && src.isPlaying && src.clip != null &&
                               observedLine != null && src.clip == observedLine.audioClip;
            if (audioDriven)
            {
                // Real VO: smoothed RMS amplitude of the playing voice.
                src.GetOutputData(audioBuffer, 0);
                float sum = 0f;
                for (int i = 0; i < audioBuffer.Length; i++) sum += audioBuffer[i] * audioBuffer[i];
                float rms = Mathf.Sqrt(sum / audioBuffer.Length);
                target = Mathf.Clamp01(rms * amplitudeGain);
            }
            else
            {
                // No VO yet: procedural talk envelope. Two incommensurate oscillators around 7–11 Hz
                // plus a slow syllable/phrase gate, phase-offset by the per-line seed, ramped in at
                // line start. Deterministic per line, believable enough until ElevenLabs VO lands.
                float t = lineTime;
                float f1 = 7f + 4f * Frac(lineSeed * 0.113f);            // 7..11 Hz core chatter
                float f2 = f1 * 0.63f;
                float p1 = lineSeed * 1.7f;
                float p2 = lineSeed * 3.1f;

                float chatter = Mathf.Abs(Mathf.Sin(t * f1 * Mathf.PI + p1)) * 0.7f
                              + Mathf.Abs(Mathf.Sin(t * f2 * Mathf.PI + p2)) * 0.3f;
                // Phrase gate ~1.5 Hz: dips toward closed between "words".
                float gate = 0.45f + 0.55f * Mathf.Clamp01(Mathf.Sin(t * 3f + lineSeed) * 0.5f + 0.6f);
                float rampIn = Mathf.Clamp01(t / 0.15f);
                target = Mathf.Clamp01(chatter * gate) * rampIn;
            }

            // Fast attack, slightly slower release, so consonant hits read but the jaw doesn't buzz.
            float rate = target > mouthOpen ? 18f : 10f;
            mouthOpen = Mathf.Lerp(mouthOpen, target, 1f - Mathf.Exp(-rate * dt));
        }

        private AudioSource ActiveVoiceSource()
        {
            if (voiceSourceOverride != null) return voiceSourceOverride;
            return systemVoiceSource;
        }

        private void UpdateBlink(float dt)
        {
            if (blinkPhase >= 0f)
            {
                blinkPhase += dt;
                if (blinkPhase >= BlinkDuration) blinkPhase = -1f;
            }
            else
            {
                blinkTimer -= dt;
                if (blinkTimer <= 0f)
                {
                    blinkPhase = 0f;
                    blinkTimer = Random.Range(2.5f, 6f);
                }
            }
        }

        private void UpdateSaccade(float dt)
        {
            saccadeTimer -= dt;
            if (saccadeTimer <= 0f)
            {
                saccadeTimer = Random.Range(0.6f, 2.5f);
                saccadeOffset = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-4f, 4f), 0f);
            }
            saccadeCurrent = Vector3.Lerp(saccadeCurrent, saccadeOffset, 1f - Mathf.Exp(-14f * dt));
        }

        // ------------------------------------------------------------------ apply (after Animator)

        private void LateUpdate()
        {
            ApplyFace();
            ApplyLookAt();
        }

        private void ApplyFace()
        {
            // --- lip-sync visemes (restrained: jaw capped, flavoured with wide/tight-O) ---
            float open01 = mouthOpen * maxOpenWeight;
            SetShapes(openShapes, open01 * 100f);
            SetShapes(jawShapes, open01 * 55f);                       // jaw at ~half the viseme weight
            SetShapes(wideShapes, mouthOpen * wideMix * 100f);
            SetShapes(tightOShapes, mouthOpen * tightMix * 100f);

            // --- blink (always alive, independent of speaking) ---
            float blinkWeight = blinkPhase >= 0f
                ? Mathf.Sin(Mathf.Clamp01(blinkPhase / BlinkDuration) * Mathf.PI) * 100f
                : 0f;
            SetShapes(blinkShapes, blinkWeight);

            // --- subtle emotion offset while speaking ---
            float smile = 0f, browRaise = 0f, browDrop = 0f;
            switch (activeEmotion)
            {
                case DialogueEmotion.Warm: smile = 18f; browRaise = 8f; break;
                case DialogueEmotion.Playful: smile = 22f; browRaise = 12f; break;
                case DialogueEmotion.Nervous: browDrop = 14f; browRaise = 10f; break;
                case DialogueEmotion.Uneasy: browDrop = 18f; browRaise = 6f; break;
                case DialogueEmotion.Focused: browDrop = 8f; break;
                case DialogueEmotion.Determined: browDrop = 12f; break;
                case DialogueEmotion.Urgent: browDrop = 14f; browRaise = 8f; break;
            }
            SetShapes(smileShapes, smile * emotionWeight);
            SetShapes(browRaiseShapes, browRaise * emotionWeight);
            SetShapes(browDropShapes, browDrop * emotionWeight);
        }

        private void SetShapes(ShapeBinding[] shapes, float weight)
        {
            if (shapes == null) return;
            for (int i = 0; i < shapes.Length; i++)
            {
                var s = shapes[i];
                if (s.renderer != null) s.renderer.SetBlendShapeWeight(s.index, weight);
            }
        }

        private void ApplyLookAt()
        {
            Transform head = headBone;
            if (head == null) return;

            Transform target = ResolveLookTarget();
            float weightTarget = target != null ? 1f : 0f;
            lookWeight = Mathf.MoveTowards(lookWeight, weightTarget, Time.deltaTime * lookWeightLerpSpeed);

            // Desired world-space delta: the rotation that swings the CHARACTER's facing toward the
            // target, clamped to a natural head-turn cone. Working from the root's forward (not the
            // head bone's local axes) keeps this correct on CC rigs whose head-bone axes are arbitrary.
            Quaternion targetDelta = Quaternion.identity;
            if (target != null)
            {
                Vector3 to = target.position - head.position;
                if (to.sqrMagnitude > 0.0001f)
                {
                    Quaternion full = Quaternion.FromToRotation(transform.forward, to.normalized);
                    targetDelta = Quaternion.RotateTowards(Quaternion.identity, full, maxHeadTurnDegrees);
                }
            }

            lookDelta = Quaternion.Slerp(lookDelta, targetDelta,
                1f - Mathf.Exp(-lookRotationLerpSpeed * Time.deltaTime));

            Quaternion applied = Quaternion.Slerp(Quaternion.identity, lookDelta, lookWeight);
            head.rotation = applied * head.rotation; // layered on top of the Animator's pose

            // Eyes lead the head a little, plus idle saccade jitter — always applied so eyes never go dead.
            if (eyeBones != null)
            {
                Quaternion eyeExtra = Quaternion.Slerp(Quaternion.identity, lookDelta, lookWeight * eyeExtraWeight);
                Quaternion saccade = Quaternion.Euler(saccadeCurrent);
                for (int i = 0; i < eyeBones.Length; i++)
                {
                    var e = eyeBones[i];
                    if (e == null) continue;
                    e.rotation = eyeExtra * e.rotation;
                    e.localRotation = e.localRotation * saccade;
                }
            }
        }

        /// <summary>Who this actor should look at right now: listeners watch the speaker; the speaker
        /// watches the nearest other actor (or the player); nobody talking → null (weight decays,
        /// the Animator pose wins again).</summary>
        private Transform ResolveLookTarget()
        {
            var ds = DialogueSystem.Instance;
            var line = ds != null ? ds.CurrentLine : null;
            if (line == null) return null;

            if (line.speaker != speaker)
            {
                var speakerHead = DialogueActorRegistry.GetSpeakerHead(line.speaker);
                // Narration or an unbodied speaker: no target — listeners stay in their animated pose.
                return speakerHead;
            }

            // We are the speaker: address the nearest other registered actor, falling back to the player.
            var all = DialogueActorRegistry.All;
            Transform nearest = null;
            float best = float.MaxValue;
            Vector3 myPos = transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == null || other == this) continue;
                float d = (other.transform.position - myPos).sqrMagnitude;
                if (d < best) { best = d; nearest = other.HeadOrRoot; }
            }
            if (nearest == null) nearest = playerTransform;
            return nearest;
        }
    }
}
