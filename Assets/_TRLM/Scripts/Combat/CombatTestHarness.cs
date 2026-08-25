using UnityEngine;

namespace TRLM.Combat
{
    /// <summary>
    /// TEST-ONLY harness for repeatable injury/bleed/poison testing in 92_Test_Combat without
    /// building dedicated UI buttons — call these public methods from an eval command or a debug
    /// key. Not part of the shipped combat architecture.
    /// </summary>
    public class CombatTestHarness : MonoBehaviour
    {
        [SerializeField] private RegionalInjurySystem injurySystem;

        private void Awake()
        {
            if (injurySystem == null) injurySystem = GetComponentInChildren<RegionalInjurySystem>();
        }

        public void ForceInjury(BodyRegion region, float severity) => injurySystem?.DebugForceInjury(region, severity);
        public void ForceBleed(float severity) => injurySystem?.DebugForceBleed(severity);
        public void ForcePoison(float severity) => injurySystem?.DebugForcePoison(severity);
    }
}
