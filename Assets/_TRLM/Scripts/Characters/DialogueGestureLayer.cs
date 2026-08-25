using UnityEngine;
using TRLM.Dialogue;

namespace TRLM.Characters
{
    /// <summary>
    /// Optional, fully guarded bridge from dialogue lines to a restrained Animator gesture layer.
    /// When this actor starts a line it fires a "Gesture" trigger and a "GestureIntensity" float
    /// (0..1, from the line's emotion) — but ONLY if the Animator actually declares those
    /// parameters. With no Animator, no parameters, or no upper-body layer, the whole component is
    /// a silent no-op, so it is safe to drop on every companion today and author the layer later.
    /// </summary>
    [RequireComponent(typeof(DialogueActor))]
    public class DialogueGestureLayer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string triggerParam = "Gesture";
        [SerializeField] private string intensityParam = "GestureIntensity";

        private DialogueActor actor;
        private bool hasTrigger;
        private bool hasIntensity;
        private bool subscribed;

        private void Awake()
        {
            actor = GetComponent<DialogueActor>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            CacheParameters();
        }

        private void CacheParameters()
        {
            hasTrigger = false;
            hasIntensity = false;
            if (animator == null || animator.runtimeAnimatorController == null) return;

            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerParam) hasTrigger = true;
                else if (p.type == AnimatorControllerParameterType.Float && p.name == intensityParam) hasIntensity = true;
            }
        }

        private void OnEnable() { TrySubscribe(); }

        private void Update()
        {
            if (!subscribed) TrySubscribe();
        }

        private void OnDisable()
        {
            var ds = DialogueSystem.Instance;
            if (subscribed && ds != null) ds.OnLineStarted -= HandleLineStarted;
            subscribed = false;
        }

        private void TrySubscribe()
        {
            if (subscribed) return;
            var ds = DialogueSystem.Instance;
            if (ds == null) return;
            ds.OnLineStarted += HandleLineStarted;
            subscribed = true;
        }

        private void HandleLineStarted(DialogueLine line)
        {
            if (line == null || actor == null || line.speaker != actor.Speaker) return;
            if (animator == null || !animator.isActiveAndEnabled) return;

            if (hasIntensity) animator.SetFloat(intensityParam, IntensityFor(line.emotion));
            if (hasTrigger) animator.SetTrigger(triggerParam);
        }

        /// <summary>Restrained intensity mapping: calm emotions barely gesture, urgent ones a bit more.</summary>
        private static float IntensityFor(DialogueEmotion emotion)
        {
            switch (emotion)
            {
                case DialogueEmotion.Urgent: return 0.9f;
                case DialogueEmotion.Determined: return 0.7f;
                case DialogueEmotion.Playful: return 0.6f;
                case DialogueEmotion.Nervous:
                case DialogueEmotion.Uneasy: return 0.5f;
                case DialogueEmotion.Warm: return 0.4f;
                case DialogueEmotion.Focused: return 0.3f;
                default: return 0.25f;
            }
        }
    }
}
