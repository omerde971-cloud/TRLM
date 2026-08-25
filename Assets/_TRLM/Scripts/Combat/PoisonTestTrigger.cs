using UnityEngine;

namespace TRLM.Combat
{
    /// <summary>
    /// TEST-ONLY debug source for PoisonEffect (Section 26) — no snake AI exists yet, and none is
    /// required per the brief. Attach to a trigger volume in a test scene, or call ApplyPoisonTo
    /// directly (e.g. via eval) for repeatable testing. Clearly a stand-in, not real content.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PoisonTestTrigger : MonoBehaviour
    {
        [SerializeField] private float poisonSeverity = 2f;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var injury = other.GetComponentInParent<RegionalInjurySystem>()
                ?? other.GetComponentInChildren<RegionalInjurySystem>();
            injury?.ApplyPoison(poisonSeverity);
        }

        public void ApplyPoisonTo(RegionalInjurySystem target) => target?.ApplyPoison(poisonSeverity);
    }
}
