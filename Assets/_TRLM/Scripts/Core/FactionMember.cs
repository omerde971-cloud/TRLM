using UnityEngine;

namespace TRLM.Core
{
    /// <summary>
    /// Marks a GameObject's team. WeaponController checks this (via GetComponentInParent /
    /// GetComponentInChildren, same pattern as IDamageable lookups) before applying weapon
    /// damage: a PlayerTeam shooter never damages a PlayerTeam target (e.g. a companion).
    /// Everything else (Wildlife, HumanHostile, Environment, or no FactionMember at all) takes
    /// damage normally. Placed on PF_Player (PlayerTeam), PF_Wolf (Wildlife), and
    /// PF_Jonah_Companion (PlayerTeam).
    /// </summary>
    public class FactionMember : MonoBehaviour
    {
        public Faction faction = Faction.Environment;
    }
}
