using UnityEngine;

namespace TRLM.Companions
{
    /// <summary>
    /// Stamps a companion GameObject with its stable identity and formation slot. Deliberately
    /// data-only — no behavior, no personality. Sprint 08B+ (personality, threat reactions,
    /// rescue, morale) reads Id off this component instead of adding new per-character branches
    /// to CompanionAI.
    /// </summary>
    public class CompanionIdentity : MonoBehaviour
    {
        [SerializeField] private CompanionId id;
        [SerializeField] private string displayName;

        public CompanionId Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id.ToString() : displayName;
    }
}
