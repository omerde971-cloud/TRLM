using UnityEngine;
using TRLM.Dialogue;

namespace TRLM.Companions
{
    /// <summary>
    /// Humanoid head/eye attention for companions, via Animator.SetLookAtPosition (all
    /// companion rigs are humanoid CC3 avatars). Picks one gaze target from a small
    /// priority list — threat, current dialogue speaker, hurt ally, own activity focus,
    /// the player when nearby, or an occasional glance at a squadmate — and eases the IK
    /// weight so heads never snap. Requires "IK Pass" on the Animator's base layer
    /// (enabled by the Sprint 2 setup pass on AC_Human_Base).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CompanionLookAt : MonoBehaviour
    {
        [SerializeField] private float maxLookDistance = 18f;
        [Tooltip("How quickly gaze weight fades in/out. Human glances take ~0.3-0.5s.")]
        [SerializeField] private float weightLerpSpeed = 2.5f;
        [Tooltip("Head-only bias: body barely rotates from gaze (0.1), head does most of the work.")]
        [SerializeField, Range(0f, 1f)] private float bodyWeight = 0.08f;
        [SerializeField, Range(0f, 1f)] private float headWeight = 0.75f;
        [SerializeField, Range(0f, 1f)] private float eyesWeight = 1f;
        [SerializeField] private Vector2 idleGlanceIntervalRange = new Vector2(4f, 9f);
        [SerializeField] private float idleGlanceDuration = 2.2f;

        private Animator animator;
        private CompanionAI ai;
        private CompanionAwareness awareness;
        private CompanionIdentity identity;

        private float currentWeight;
        private Vector3 currentLookPoint;
        private bool hasTarget;

        private Transform dialogueSpeaker;
        private float glanceTimer;
        private float nextGlanceAt;
        private Transform glanceTarget;
        private float glanceRemaining;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ai = GetComponent<CompanionAI>();
            awareness = GetComponent<CompanionAwareness>();
            identity = GetComponent<CompanionIdentity>();
            nextGlanceAt = Random.Range(idleGlanceIntervalRange.x, idleGlanceIntervalRange.y);
        }

        private void OnEnable()
        {
            var dialogue = DialogueSystem.Instance;
            if (dialogue != null)
            {
                dialogue.OnLineStarted += HandleLineStarted;
                dialogue.OnLineEnded += HandleLineEnded;
            }
        }

        private void OnDisable()
        {
            var dialogue = DialogueSystem.Instance;
            if (dialogue != null)
            {
                dialogue.OnLineStarted -= HandleLineStarted;
                dialogue.OnLineEnded -= HandleLineEnded;
            }
        }

        private void HandleLineStarted(DialogueLine line)
        {
            dialogueSpeaker = ResolveSpeaker(line.speaker);
        }

        private void HandleLineEnded(DialogueLine line) => dialogueSpeaker = null;

        /// <summary>Map a dialogue speaker to a scene transform: the matching companion, or the
        /// player for Elias (protagonist). Narration/Unknown → nobody.</summary>
        private Transform ResolveSpeaker(DialogueSpeaker speaker)
        {
            switch (speaker)
            {
                case DialogueSpeaker.Elias:
                    return ai != null ? ai.FollowTarget : null;
                case DialogueSpeaker.Narration:
                case DialogueSpeaker.Unknown:
                    return null;
                default:
                    var allCompanions = CompanionAI.All;
                    for (int i = 0; i < allCompanions.Count; i++)
                    {
                        var c = allCompanions[i];
                        if (c == null || c == ai) continue;
                        var id = c.GetComponent<CompanionIdentity>();
                        if (id != null && id.Id.ToString() == speaker.ToString())
                            return c.transform;
                    }
                    return null;
            }
        }

        private void Update()
        {
            // Idle glance scheduling (cheap; the actual IK happens in OnAnimatorIK).
            glanceTimer += Time.deltaTime;
            if (glanceRemaining > 0f)
            {
                glanceRemaining -= Time.deltaTime;
                if (glanceRemaining <= 0f) glanceTarget = null;
            }
            else if (glanceTimer >= nextGlanceAt)
            {
                glanceTimer = 0f;
                nextGlanceAt = Random.Range(idleGlanceIntervalRange.x, idleGlanceIntervalRange.y);
                glanceTarget = PickGlanceTarget();
                glanceRemaining = idleGlanceDuration;
            }

            ResolveLookTarget();
        }

        private Transform PickGlanceTarget()
        {
            // Mostly the nearest squadmate, sometimes the player — small social glances.
            var allCompanions = CompanionAI.All;
            Transform nearest = null;
            float bestSqr = 10f * 10f;
            for (int i = 0; i < allCompanions.Count; i++)
            {
                var c = allCompanions[i];
                if (c == null || c == ai || c.IsDead) continue;
                float sqr = (c.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; nearest = c.transform; }
            }
            if (nearest != null && Random.value < 0.65f) return nearest;
            return ai != null ? ai.FollowTarget : null;
        }

        private void ResolveLookTarget()
        {
            hasTarget = false;

            // 1. Active threat.
            if (awareness != null && awareness.HasThreat)
            {
                Vector3 threat = awareness.ThreatTransform != null
                    ? awareness.ThreatTransform.position + Vector3.up * 0.5f
                    : awareness.ThreatPosition + Vector3.up * 0.5f;
                SetLookPoint(threat);
                return;
            }

            // 2. Recent gunfire direction.
            if (awareness != null && awareness.HasRecentLoudNoise && ai != null && ai.CurrentActivity == CompanionAI.IdleActivity.WatchNoise)
            {
                SetLookPoint(awareness.LastLoudNoisePosition + Vector3.up * 1.5f);
                return;
            }

            // 3. Whoever is speaking.
            if (dialogueSpeaker != null && dialogueSpeaker != transform)
            {
                SetLookPoint(dialogueSpeaker.position + Vector3.up * 1.6f);
                return;
            }

            // 4. Hurt ally being checked on.
            if (ai != null && ai.CurrentActivity == CompanionAI.IdleActivity.CheckAlly &&
                awareness != null && awareness.InjuredAlly != null)
            {
                SetLookPoint(awareness.InjuredAlly.position + Vector3.up * 1.5f);
                return;
            }

            // 5. Occasional social glance.
            if (glanceTarget != null)
            {
                SetLookPoint(glanceTarget.position + Vector3.up * 1.6f);
                return;
            }
        }

        private void SetLookPoint(Vector3 point)
        {
            if ((point - transform.position).sqrMagnitude > maxLookDistance * maxLookDistance) return;
            // Ignore points essentially behind the head — IK twisting necks 180° looks broken.
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (Vector3.Angle(transform.forward, flat) > 100f) return;
            currentLookPoint = point;
            hasTarget = true;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            float target = hasTarget ? 1f : 0f;
            currentWeight = Mathf.MoveTowards(currentWeight, target, weightLerpSpeed * Time.deltaTime);
            if (currentWeight <= 0.001f) return;

            animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight, eyesWeight, 0.6f);
            animator.SetLookAtPosition(currentLookPoint);
        }
    }
}
