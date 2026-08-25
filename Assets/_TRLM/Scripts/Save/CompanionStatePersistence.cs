using System.Collections.Generic;
using UnityEngine;
using TRLM.Companions;
using TRLM.Survival;

namespace TRLM.Save
{
    /// <summary>
    /// Captures/restores per-companion state (Mira/Jonah/Lena/Noah). CRITICAL invariant (Sprint 10
    /// Part H): a dead companion must never come back alive, and a buried companion must never
    /// re-appear as an active AI companion — see RestoreCompanions' ordering and its use of
    /// PsychologicalState's Mark*AlreadyProcessed guards before re-applying death/burial.
    /// </summary>
    public static class CompanionStatePersistence
    {
        public static List<CompanionStateData> Capture()
        {
            var list = new List<CompanionStateData>();

            foreach (var ai in Object.FindObjectsByType<CompanionAI>(FindObjectsSortMode.None))
            {
                var identity = ai.GetComponent<CompanionIdentity>();
                if (identity == null) continue;

                // Buried companions have their whole GameObject destroyed as part of BurialZone's
                // corpse handling, so they simply won't be found by FindObjectsByType at all by the
                // time Capture() runs post-burial — isBuried is always false here and corrected by
                // AddMissingAsBuried below using the world state's used-burial-zone list instead.
                list.Add(new CompanionStateData
                {
                    id = identity.Id,
                    isAlive = !ai.IsDead,
                    isBuried = false, // corrected by SaveOrchestrator against WorldStateData.usedBurialZones
                    posX = ai.transform.position.x,
                    posY = ai.transform.position.y,
                    posZ = ai.transform.position.z,
                    commandStateIndex = (int)ai.CurrentState,
                    deathMoraleConsequenceApplied = ai.IsDead, // if it's dead in-scene, the death event already fired this session
                });
            }

            return list;
        }

        /// <summary>Companions whose GameObject no longer exists at all (buried) don't show up in a
        /// live scene scan — call this with the saved list AFTER Capture() to add an entry for any
        /// saved companion id missing from the scan, marked buried, so a save taken after a burial
        /// doesn't silently forget that companion existed.</summary>
        public static void AddMissingAsBuried(List<CompanionStateData> captured, IEnumerable<CompanionId> allKnownIds, ISet<CompanionId> buriedIds)
        {
            var present = new HashSet<CompanionId>();
            foreach (var c in captured) present.Add(c.id);

            foreach (var id in allKnownIds)
            {
                if (present.Contains(id)) continue;
                if (!buriedIds.Contains(id)) continue;

                captured.Add(new CompanionStateData
                {
                    id = id,
                    isAlive = false,
                    isBuried = true,
                    deathMoraleConsequenceApplied = true,
                });
            }
        }

        /// <summary>Restore order matters: seed PsychologicalState's dedup guards BEFORE re-killing
        /// a companion, so OnDeath firing again (a real, desired side effect — CompanionAI disables
        /// its own NavMeshAgent etc.) does not also re-apply the morale hit. Buried companions are
        /// simply left destroyed/absent — there is nothing to "restore" for them beyond the grave
        /// marker WorldStatePersistence already rebuilds.</summary>
        public static void Restore(List<CompanionStateData> saved, PsychologicalState psych)
        {
            if (saved == null) return;

            var byId = new Dictionary<CompanionId, CompanionAI>();
            foreach (var ai in Object.FindObjectsByType<CompanionAI>(FindObjectsSortMode.None))
            {
                var identity = ai.GetComponent<CompanionIdentity>();
                if (identity != null) byId[identity.Id] = ai;
            }

            foreach (var data in saved)
            {
                if (data.isBuried) continue; // already destroyed in-scene; grave marker handled separately

                if (!byId.TryGetValue(data.id, out var ai)) continue;

                if (!data.isAlive)
                {
                    if (data.deathMoraleConsequenceApplied) psych?.MarkCompanionDeathAlreadyProcessed(data.id);
                    var health = ai.GetComponent<HealthSystem>();
                    health?.RestoreState(0f, true);
                    continue;
                }

                ai.transform.position = new Vector3(data.posX, data.posY, data.posZ);
                switch ((CompanionAI.State)data.commandStateIndex)
                {
                    case CompanionAI.State.Wait: ai.CommandWait(); break;
                    default: ai.CommandFollow(); break;
                }
            }
        }
    }
}
