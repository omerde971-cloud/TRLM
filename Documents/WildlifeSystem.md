# TRLM Wildlife System

**Sprint:** World Gameplay Sprint 03 (2026-08-22)
**Scripts:** `Assets/_TRLM/Scripts/AI/` (`Wildlife/`, `Wolf/`, `Perception/`), `Assets/_TRLM/Scripts/World/` (time source)

---

## Architecture Overview

```
WildlifeSpawnManager (one per scene)
  - tracks active-count-per-species (global cap)
  - holds the scene's IWorldTimeSource + Player reference
        │
WildlifeSpawnZone (one per habitat marker)
  - species profile reference, radius, live population list
        │
WildlifeSpawner (same GameObject as the zone)
  - timed spawn decision: cooldown, player-distance, day/night multiplier, global cap
  - seeds initial population on scene Start()
        │
   Instantiate(species.animalPrefab) ──▶ WolfAI (or future species AI)
        │
WildlifeDespawnWatcher (auto-added to every spawned animal)
  - removes it once the player has been far away for a while
```

`WildlifeSpeciesProfile` (ScriptableObject, `Assets/_TRLM/ScriptableObjects/Wildlife/`) holds every
tunable the sprint brief asked for: species, min/max population, spawn chance, spawn cooldown,
respawn delay, min distance from player, max active globally, day/night/rain multipliers,
aggression modifier, despawn distance, preferred patrol radius. Zones reference a profile
instead of duplicating numbers — balancing one species means editing one asset.

5 profiles exist: `SpeciesProfile_Wolf` (fully wired to `PF_Wolf.prefab`), and
`SpeciesProfile_Bear/Boar/Snake/MountainGoat` (data-only — see "Other Species" below).

---

## Day/Night Hook

`TRLM.World.IWorldTimeSource` is a two-member interface (`IsNight`, `NormalizedTimeOfDay`).
`DebugWorldTimeSource` is the only implementation right now — an Inspector checkbox
(`forceNight`) that a future `DayNightSystem` will replace without any AI code changing,
since `WildlifeSpawnManager` and `WolfAI` only ever talk to the interface. Each gameplay
scene needs exactly one `DebugWorldTimeSource` (or its future real replacement) assigned to
the `WildlifeSpawnManager.timeSourceBehaviour` field.

`WildlifeSpeciesProfile.dayActivityMultiplier`/`nightActivityMultiplier` scale spawn chance
per species — wolves are set to 0.8 day / 1.6 night (noticeably more active at night, per
spec), not an absurd multiplier.

---

## Wolf — First Real Animal

### Honest asset limitation (report per sprint instructions, not silently worked around)

`Assets/ThirdParty/Animals/Wolf_CC0` has **no rig, no bones, no animation clips at all**
(confirmed originally in the P0 audit, re-confirmed this sprint before building the AI).
`WolfAI` therefore drives a plain `Transform` via `NavMeshAgent` — the wolf moves, turns,
and reaches all 9 states correctly, but it **slides rather than visually walks/runs/attacks**.
This is a real, pre-existing asset gap, not a bug in this sprint's AI. Fixing it needs either
rigging this mesh from scratch (Blender + Mixamo-style retarget) or sourcing a rigged wolf —
both are new-asset-acquisition work, explicitly out of scope for a gameplay sprint.

### State machine

`WolfAI.State`: `Idle → Roam → Investigate → Alert → Stalk → Chase → Attack → Retreat → ReturnToTerritory`

| State | Trigger in | Trigger out |
|---|---|---|
| Idle | scene start / returned home | timer expires → Roam; sees/hears player → Alert/Investigate |
| Roam | idle timer done | reached destination → Idle; sees player → Alert; hears noise → Investigate |
| Investigate | heard a noise | sees player → Alert; timeout or arrived → Roam |
| Alert | just spotted player | sustained sight for `alertDuration` → Stalk; loses sight → Roam |
| Stalk | alert confirmed | closes to `stalkDistance`, aggression roll, or nearby ally → Chase; loses sight → Retreat |
| Chase | stalk escalates | in `attackRange` (and pack attacker slot free) → Attack; loses target, exceeds leash distance from territory, or `maxChaseSeconds` exceeded → Retreat |
| Attack | chase closes the distance | windup → damage tick → cooldown loop; target leaves range → Chase |
| Retreat | disengagement condition met | timer done or reached a fall-back point → ReturnToTerritory |
| ReturnToTerritory | retreat finished | arrives at territory center → Idle |

**Abandon-pursuit conditions** (all implemented, matches spec): player escapes the zone's
leash radius (`leashDistanceFromTerritory`, default 70m from the territory center), wolf
loses sight and can't close the last-known-position gap, chase timer exceeds
`maxChaseSeconds` (20s default). There is no dedicated "safe structure" detector yet — the
brief's "reaches a safe structure" condition is currently only satisfied indirectly (walls
block line-of-sight, which ends the chase via the sight-loss path); a real safe-house
interior check would need the safe-house system this sprint didn't build.

### Perception (`WolfPerception`)

- **Sight**: distance (`sightRange`, 22m) + field-of-view (`sightAngleDegrees`, 140°) +
  `Physics.Linecast` so a wall/rock genuinely blocks vision.
- **Sound**: subscribes to the static `TRLM.AI.Perception.NoiseEvents` bus.
  `PlayerNoiseEmitter` (attached to `PF_Player`) converts the *existing, unmodified*
  `FirstPersonController`'s speed/crouch/grounded state into noise pulses — walking is
  quiet (6m), sprinting is loud (18m), crouching multiplies whatever radius by 0.35, and
  landing a jump fires a one-off 12m pulse. No gunshot exists yet (no weapons this sprint)
  but the bus is generic — a future weapon fires `NoiseEvents.Raise(pos, veryLargeRadius)`
  with zero new plumbing.

### Pack behavior (lightweight, not AAA)

- A wolf entering **Chase** calls `AlertNearbyPack()`, which nudges any non-engaged wolf
  within `packAlertRadius` (30m) into **Investigate** at the last-known player position —
  so nearby pack members can join, but don't teleport-aggro from across the map.
- `PackFlankOffset()` gives each wolf a deterministic angular offset (based on its index in
  the shared wolf registry) around the player during Chase, so a pack doesn't stack on one
  point.
- `maxSimultaneousAttackers` (default 2, per-wolf field) gates entry into **Attack** via a
  shared static `HashSet<WolfAI>` — a 3rd+ wolf in Chase range simply waits instead of
  piling on, directly satisfying "the player should not be stun-locked by 5 wolves."

### Damage integration

Wolf attack calls the **existing** `TRLM.Core.IDamageable`/`TRLM.Survival.HealthSystem` —
no new health system was created. Attack has a readable windup (`attackWindupSeconds`,
0.5s) before the hit registers, a cooldown (`attackCooldownSeconds`, 1.6s) so it can't hit
every frame, a range re-check at the moment of impact (not just at windup start, so
stepping back mid-windup avoids the hit), and a `Physics.Linecast` so a wall between wolf
and player blocks the damage. **Real bug found and fixed during Play Mode verification**:
the first working version used `target.GetComponentInParent<IDamageable>()`, but
`HealthSystem` lives on `PF_Player/Systems`, a *child* of the player root — `GetComponentInParent`
only searches the root and its ancestors, so damage silently never landed. Fixed to
`GetComponentInChildren<IDamageable>()`. Verified live afterward: player health dropped
100 → 45 across a multi-wolf engagement.

The wolf itself also implements `IDamageable` (via its own `HealthSystem`, 60 HP) — nothing
can hurt it yet (no weapons), but rockfall/future combat can reuse this immediately.

---

## Navigation

`Unity.AI.Navigation.NavMeshSurface` (Collider-based geometry, not Render-Mesh-based —
deliberately, so the ~520 collider-less decorative trees never affect walkability and only
real obstacles like rocks/terrain/house floor do). One surface baked per scene:
`20_Island_Blockout.unity` and `90_Test_AI.unity`.

**Validated this sprint** (see WorldDesign.md → Route Audit): the entire 8-segment primary
route (Sea → Coast → Settlement → Deep Forest → Rock Belt → Mountain Pass → Summit → Cave
staging) returns `NavMeshPathStatus.PathComplete` end-to-end — no broken segments.

**Process pitfall worth recording**: `NavMeshSurface.BuildNavMesh()` results are only
persisted once the scene itself is saved — building it, then switching to a different scene
via `create_scene`/`open_scene` *without saving first*, silently discards the bake (the
`Navigation` GameObject and its data simply aren't in the last-saved scene file). This
caused an early false alarm this sprint (looked like "wolves never spawn" — actually
"NavMesh doesn't exist in the saved scene yet"). Always save immediately after baking.

### Debug visualization (editor-only)

`WolfAI.OnDrawGizmosSelected`: yellow sphere (approximate sight range), cyan sphere
(territory leash radius), magenta line (current NavMeshAgent destination).
`WildlifeSpawnZone.OnDrawGizmos`: orange wire sphere (habitat radius). `WorldMarker`
(Sprint 02) still draws its own colored sphere per category. All Gizmos-only, zero runtime
cost.

---

## Other Species (Bear / Boar / Snake / Mountain Goat)

Per sprint instructions ("do not spend this sprint rebuilding the cast" applies equally
here — no time was spent sourcing new animal models this sprint), these 4 species have
**data-only** `WildlifeSpeciesProfile` assets (population, spawn chance, day/night
multipliers, aggression, etc. all filled in per the sprint's ecological rules — bears rare
& isolated, boars muddy/shrub-loving, snakes small-radius/rock-and-ruin, goats
passive/high-slope/flee-from-player) but **no `animalPrefab` assigned** — there is no 3D
asset for any of them yet. `WildlifeSpawner` checks for a missing prefab in `Start()` and
disables itself with a clear warning rather than erroring, so the architecture is fully
demonstrated (create a zone, drop in a profile, wire a prefab, done) without inventing fake
animals.

---

## Performance Rules Applied This Sprint

- **Trees**: ~520 total, split ~55%/45% between cheap background billboard cards and full
  trunk+branch meshes. Only 28 (near player routes/settlement/wildlife zones, within a 22m
  corridor) received a `CapsuleCollider` — the rest remain collider-free by design.
- **Rocks**: 213 instances. 97 with >200 triangles had their `MeshCollider` replaced with a
  `BoxCollider` sized to the mesh bounds; 116 low-poly ones (≤200 tris) kept their
  `MeshCollider` since the cost is negligible.
- **Materials**: GPU Instancing enabled on all 17 environment materials (tree/rock/grass/terrain).
- **LODGroups**: deliberately **not** created. The tree pack's "background atlas" cards and
  the full trunk+branch trees are different assets, not detail levels of the same mesh —
  same for the 11 distinct rock meshes. Faking an `LODGroup` by swapping between unrelated
  meshes would look like a visible pop, not a real LOD transition, so per sprint instructions
  ("do not fake a bad LOD system") this was documented as a gap instead: **real LOD chains
  need generated lower-poly variants of the same meshes** (a mesh-decimation pass), not
  currently available from either asset pack.
- **Wildlife**: max ~9-12 wolves active across 4 zones at once (each zone caps at 1-3, global
  cap ~12) — not 40 scattered active animals.

---

## Day/Night Hook — Sprint 05 Update

The placeholder `DebugWorldTimeSource` referenced in the section above has
been replaced in the production scene by a real `TRLM.World.DayNightSystem`
(day ≈ 8 min, night ≈ 10 min, both Inspector-configurable), which implements
the same `IWorldTimeSource` interface. `WildlifeSpawnManager.timeSourceBehaviour`
was re-pointed to it — verified live that `WildlifeSpawnManager.TimeSource`
resolves correctly and `IsNight` propagates through to spawn/behavior logic.
No AI code changed. See `Documents/GameplayIntegration.md` for full detail.

A new `WolfFireAvoidance` component (separate file, `WolfAI.cs` untouched)
gives wolves a soft NavMesh-destination nudge away from any currently-lit
`FirePoint` — but only outside Chase/Attack state, so it never overrides
active combat behavior.

## Known Gaps / Follow-ups

- No visual animation on the wolf (see "Honest asset limitation" above) — highest-priority
  follow-up if wolf combat needs to feel real rather than functional.
- No dedicated "reached a safe structure" detector for wolf disengagement — currently only
  indirect via line-of-sight blocking.
- Bear/Boar/Snake/MountainGoat need actual 3D assets before their profiles do anything.
- No LOD chains (see Performance Rules above) — needs mesh generation, not code.
- Wolf navigation was validated for "does a path exist" (NavMesh connectivity) but not
  stress-tested for "does a fleeing/stuck wolf ever get physically wedged against a rock
  cluster" over a long play session.
