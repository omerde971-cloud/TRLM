using UnityEngine;
using TRLM.Companions;

namespace TRLM.World
{
    /// <summary>
    /// Keeps the companion squad OUTSIDE the cave during the player's solo discovery beat: it issues
    /// the EXISTING "wait" command when the player enters the mouth (so the squad holds at the entrance
    /// instead of piling into the sealed rock interior — the exact stuck/stacking failure Sprint 2
    /// flagged), and re-issues "follow" if the player steps back out. Pure use of the existing
    /// <see cref="CompanionCommandInput"/> API — companion AI is untouched. This is the intentional
    /// design choice "some companions remain outside", implemented deliberately rather than left to
    /// accidental navigation limits.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CompanionCaveGate : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";
        private CompanionCommandInput _input;
        private CompanionCommandInput Input => _input != null ? _input : (_input = FindFirstObjectByType<CompanionCommandInput>());

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            Input?.IssueWait(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            Input?.IssueFollow(true);
        }
    }
}
