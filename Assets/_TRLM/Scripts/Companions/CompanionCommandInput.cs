using UnityEngine;
using UnityEngine.InputSystem;

namespace TRLM.Companions
{
    /// <summary>
    /// Vertical-slice-only companion command binding. PlayerInputHandler intentionally has no
    /// companion-command keys and is not modified here — instead this ONE component reads
    /// Keyboard.current directly, as a deliberate, scoped exception documented at the point of
    /// use rather than a silent violation of the "never read Keyboard.current directly" rule.
    /// Bindings: 1 = Follow, 2 = Wait, 3 = Come Here (to the player's position) — number row,
    /// unlikely to collide with anything else this sprint. Holding Shift targets every companion
    /// in range instead of just the nearest one — this is the minimal "command all" hook Sprint
    /// 08B+ squad UX can build on; no command-wheel or targeting UI is added here.
    /// </summary>
    public class CompanionCommandInput : MonoBehaviour
    {
        [SerializeField] private float commandRange = 25f;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            bool all = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

            if (kb.digit1Key.wasPressedThisFrame) IssueFollow(all);
            else if (kb.digit2Key.wasPressedThisFrame) IssueWait(all);
            else if (kb.digit3Key.wasPressedThisFrame) IssueComeHere(all);
        }

        public void IssueFollow(bool all = false)
        {
            foreach (var c in TargetedCompanions(all)) c.CommandFollow();
        }

        public void IssueWait(bool all = false)
        {
            foreach (var c in TargetedCompanions(all)) c.CommandWait();
        }

        public void IssueComeHere(bool all = false)
        {
            foreach (var c in TargetedCompanions(all)) c.CommandComeHere(transform.position);
        }

        private System.Collections.Generic.IEnumerable<CompanionAI> TargetedCompanions(bool all)
        {
            if (!all)
            {
                var nearest = NearestCompanion();
                if (nearest != null) yield return nearest;
                yield break;
            }

            foreach (var companion in FindObjectsByType<CompanionAI>(FindObjectsSortMode.None))
            {
                if (companion.IsDead) continue;
                if (Vector3.Distance(companion.transform.position, transform.position) <= commandRange)
                    yield return companion;
            }
        }

        private CompanionAI NearestCompanion()
        {
            CompanionAI best = null;
            float bestDist = commandRange;

            foreach (var companion in FindObjectsByType<CompanionAI>(FindObjectsSortMode.None))
            {
                if (companion.IsDead) continue;
                float dist = Vector3.Distance(companion.transform.position, transform.position);
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    best = companion;
                }
            }
            return best;
        }
    }
}
