using System.Collections;
using UnityEngine;

namespace TRLM.Dialogue
{
    [RequireComponent(typeof(Collider))]
    public class DialogueSequenceTrigger : MonoBehaviour
    {
        [SerializeField] private DialogueLine[] lines;
        [SerializeField] private bool triggerOnEnter = true;
        [SerializeField] private bool oneShot = true;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private float gapSeconds = 0.25f;

        private bool fired;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnEnter || !other.CompareTag(playerTag)) return;
            Fire();
        }

        public void Fire()
        {
            if (oneShot && fired) return;
            fired = true;
            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            if (lines == null || DialogueSystem.Instance == null) yield break;

            foreach (var line in lines)
            {
                if (line == null) continue;
                DialogueSystem.Instance.Play(line);
                float duration = line.durationOverride > 0f
                    ? line.durationOverride
                    : Mathf.Clamp((line.englishSubtitle?.Length ?? 0) / 14f + 0.75f, 2f, 6f);
                yield return new WaitForSeconds(duration + gapSeconds);
            }
        }
    }
}
