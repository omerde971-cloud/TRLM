# TRLM Gameplay Integration

## Sprint 07 Update (Combat/Equipment/Injury)

Full detail in `Documents/CombatSystem.md` / `Documents/InjurySystem.md`. Summary:
firearms (pistol + shotgun), melee (knife), equipment wheel (Tab-held, pauses via
`Time.timeScale`), regional injury/bleeding/poison/trauma built on Sprint 05's
existing status-effect foundation, friendly-fire faction filtering, Layer
Collision Matrix finally configured (Sprint 06 deferred this). One real
correctness bug found and fixed during integration: firearm/melee raycasts were
hitting invisible trigger volumes by default (`QueryTriggerInteraction.Ignore`
added to both). All weapon visuals are primitive placeholders — zero weapon 3D
assets exist in the project.


**Sprint:** Gameplay Integration Sprint 05 (2026-08-22)
**Goal:** turn separate technical systems (movement, wildlife, materials/animation,
world) into one mechanically-connected vertical slice:
Preparation → Row → Land → Forest → House → Loot → Night → Wolf → Safe House →
Fire → Sleep → Morning.

---

## Multi-agent execution record

Per the sprint brief, this sprint used 3 delegated agents plus this main session
as orchestrator/integrator:

| Agent | Model | Scope |
|---|---|---|
| Sub-Agent B1 | Sonnet 5 (session default) | Items #4, #6, #7, #8, #9, #15, #17, #18, #19 — inventory, loot, survival stats, fire, wetness/cold, status-effect foundation, rockfall damage |
| Sub-Agent B2 | Sonnet 5 (session default) | Items #10, #11, #13, #14, #16, #20, #21, #24-28, #29, #30, #31, #32 — flashlight, day/night, safe house, sleep, fire/wildlife hook, rowboat, landing, companion, body carry, burial, HUD, objectives, neighborhood hooks, tutorial |
| Sub-Agent A | Haiku 4.5 | Scene wiring: loot spawn point placement/table assignment, hierarchy cleanup, HUD reference verification, singleton de-duplication |
| Main Claude (this session) | Sonnet 5 | Planning, dependency sequencing, prompt authoring, integration review, Play Mode verification, performance check, this documentation |

B1 and B2 ran **sequentially, not in parallel** — this project has no git repo,
so there is no worktree isolation available, and both agents needed live,
exclusive access to the single running Unity Editor via MCP `eval`/scene
tools. Running them concurrently risked racing compiles and scene-save
conflicts. Sub-Agent A ran after both code agents finished and the project
was confirmed compiling clean.

**Integration conflicts encountered**: Sub-Agent A initially used the wrong
serialized field name (`lootTable` instead of the actual `table` field on
`LootSpawnPoint`) for its first several attempts — visible as repeated
`set_serialized_field` errors in the console, self-corrected once it
inspected the actual field name. Verified directly afterward (via `eval`,
reading each `LootSpawnPoint`'s `table` reference through
`SerializedObject`) that all 5 loot points ended up correctly assigned
despite the console noise. Sub-Agent A also created and then correctly
cleaned up 2 duplicate `PF_Jonah_Companion_Build` leftover instances before
finishing. No other integration conflicts were found.

---

## Inventory Architecture (`TRLM.Inventory`)

`ItemDefinition` (ScriptableObject) + `InventorySlot` (item + count) +
`PlayerInventory` (10-slot cap, on `PF_Player`) + `PickupItem`
(`IInteractable`, adds to inventory on `E`, stays in world if full rather
than being silently lost). `LootTable` (weighted rolls + guaranteed items)
+ `LootSpawnPoint` (rolls once on `Start()`, spawns primitive-placeholder
pickups — no real pickup meshes yet, documented as a future art pass).

6 item assets exist: `BottledWater`, `FoodRation`, `Battery`,
`BasicMedicine`, `BetterMedicine`, `Wood`
(`Assets/_TRLM/ScriptableObjects/Items/`). 3 loot tables exist
(`Assets/_TRLM/ScriptableObjects/Loot/`): `LootTable_House` (guarantees
water + food, so the first house reliably has enough to demonstrate the
loop per the brief's explicit requirement), `LootTable_Storage`,
`LootTable_Outdoors` (sparse).

5 pre-existing `WorldMarker(type=LootPoint)` markers from Sprint 02 were
turned functional this sprint by adding a `LootSpawnPoint` sibling
component to each (no changes to `WorldMarker.cs` itself): the settlement
cabinets and the coastal bags got `House`/`Outdoors` tables respectively,
the storage shed and medical checkpoint got `Storage`, the soldier
checkpoint got `Outdoors`.

Drop is intentionally minimal this sprint (per brief: no full inventory UI):
`G` drops one unit of inventory slot 0 only — a placeholder binding, not a
real multi-slot selection UI.

---

## Survival Architecture (`TRLM.Survival`)

`TeamProvisions` (own GameObject under `WorldSystems`, not on the player) —
shared food/water pool sized to conceptually cover ~5-6 in-game days at
default drain rates (Inspector-configurable, not hardcoded to real-world
time).

`HungerSystem` / `ThirstSystem` (both on `PF_Player/Systems`) — slow
configurable depletion, `Eat()`/`Drink()` restore methods for future
item-use wiring, low-threshold stamina penalty, critical-threshold periodic
health drain (timer-based, not per-frame). Thirst depletes faster than
Hunger per the brief. `ThirstSystem.DrinkSeaWater()` (wired to a new
`SeaWaterSource : IInteractable` for shoreline placement) gives a small
thirst amount but applies a worsening penalty + minor health cost —
deliberately a bad trade, not an infinite-hydration exploit.

`WetnessSystem` / `ColdExposureSystem` (both on `PF_Player/Systems`) —
Wetness has a public `AddWetness()` hook for a future rain/ocean-exposure
caller (none exists yet — this sprint didn't add rain). Cold reads
`IWorldTimeSource.IsNight` and `WetnessSystem.Wetness` to drain body
temperature faster at night/when wet; dries faster near a lit `FirePoint`
or inside a `SafeHouseArea` (checked via `WorldMarker.SafeHouse` proximity
directly in `WetnessSystem`, not via a separate hook into `SafeHouseArea`).

**Stamina regen multiplier stacking**: Hunger/Thirst/Cold each want to slow
stamina recovery under duress. Rather than multiplying 3 penalties together
(which could zero out regen too aggressively when only mildly hungry AND
mildly cold at once), a small `StaminaRegenModifier` component takes the
**worst single active penalty** and applies only that to
`StaminaSystem.RegenMultiplier` (the one additive property added to
`StaminaSystem.cs` this sprint — everything else in that file is
untouched).

`StatusEffectController` + `IStatusEffect` — architecture only, not full
content. One proof-of-concept `MinorBleedEffect` exists purely to verify
the interface works end-to-end; Bleeding/Poison/Infection/Trauma/
Hypothermia as real content are explicitly future work.

---

## World-Time Architecture (`TRLM.World`)

`DayNightSystem` (on `WorldSystems`) implements the existing
`IWorldTimeSource` interface (`IsNight`, `NormalizedTimeOfDay`) as a
drop-in replacement for the old `DebugWorldTimeSource` placeholder —
**no AI code changed**, only the scene-level reference on
`WildlifeSpawnManager.timeSourceBehaviour` and `ColdExposureSystem`'s
time-source field were re-pointed. Day ≈ 8 minutes, night ≈ 10 minutes
(both Inspector-configurable, matching the brief's targets), lerps the
scene's Directional Light between day/night presets. `SkipToMorning()` is
the public hook `SleepInteraction` calls.

Verified live: `WildlifeSpawnManager.TimeSource` now resolves to
`DayNightSystem`, and forcing night state through it correctly flips
`IsNight` as read by the wolf spawn/behavior code — confirmed via direct
`eval` inspection in Play Mode, not just by reading the code.

---

## Fire (`TRLM.World.FirePoint`)

`IInteractable`, costs Wood from inventory to light, maintains a static
`ActiveLitFires` registry so Wetness/Cold recovery and the wolf-avoidance
hook (below) can query nearby fires cheaply instead of scanning the scene
every frame. One additive hook was added this sprint on top of B1's
original version: `public event Action OnLit;`, invoked in `Light()`, so
`ObjectiveSystem` can advance on first ignition without polling.

**Fire → wildlife hook (#16)**: `WolfFireAvoidance` (new file, `WolfAI.cs`
itself untouched) sits alongside `WolfAI` on `PF_Wolf` and nudges the
wolf's NavMesh destination away from any nearby lit fire — but **only**
when the wolf is not in `Chase`/`Attack` state, so fire is a soft
deterrent, not an absolute force field, matching the brief's explicit
instruction. Honest limitation: this is a destination-bias nudge, not a
deep `WolfAI` state-machine integration, since `WolfAI` doesn't expose a
safe non-invasive hook for finer control without editing that file
directly (which the brief said to avoid).

---

## Companion Integration (`TRLM.Companions`)

`CompanionAI` (NavMeshAgent-based, `Follow`/`Wait`/`MovingToCommandPoint`
states, loosely modeled on `WolfAI`'s style but far simpler) drives
`PF_Jonah_Companion.prefab` (built from the Sprint 04 `PF_Jonah` character
prefab + `NavMeshAgent`/`CapsuleCollider`/`HealthSystem`/`CompanionAI`).
Reuses the existing `TRLM.Survival.HealthSystem` — no separate companion
health system. `CompanionCommandInput` binds `1`/`2`/`3` to
Follow/Wait/Come-Here as a **documented, deliberately scoped exception**
to the "never read Keyboard.current directly" rule, since
`PlayerInputHandler` has no companion-command bindings and adding new raw
key bindings project-wide wasn't in scope — confined to this one component.

**Body carry** (`BodyCarry` on `PF_Player`, `CarryableCorpse` on
`CompanionAI` targets): `E` on a dead companion picks it up, re-parents it
under a new `CarryAnchor` transform, disables its `NavMeshAgent`/`Collider`
while carried. Honest limitation: `FirstPersonController` exposes no public
speed-multiplier or `CanSprint` toggle, so the brief's "movement slower,
sprint limited" requirement is only partially met — a stamina-regen
penalty via `StaminaRegenModifier` was applied (carrying drains/recovers
stamina worse), but true movement-speed slowdown would require an
additive change to `FirstPersonController.cs` that wasn't made this
sprint (flagged here rather than silently edited in).

**Burial** (`BurialZone`, one authored instance, `BurialZone_01`): requires
the player to be actively carrying a corpse, timed action with a stamina
cost, spawns a simple primitive cross grave marker at completion (no mesh
asset — built at runtime, documented as a placeholder), fires
`OnBurialComplete` as an explicit hook for a future morale/sanity system
(not implemented further this sprint).

Verified live: `CompanionAI.TakeDamage(200)` → `HealthSystem.IsDead == true`,
`NavMeshAgent.enabled == false`; `BodyCarry` correctly re-parents the
corpse under `CarryAnchor` on interact. This was a deliberate isolated
system test, not a scripted event in the main slice — the companion is not
automatically killed during normal play.

---

## Rowboat / Landing (`TRLM.Boat`)

`RowboatController` (on `PF_Rowboat.prefab`, alongside the existing,
**untouched** `BuoyancyController`) turns `SPACE` (the same `JumpPressed`
event `FirstPersonController` uses for jumping — reused contextually,
since the player's normal controller is disabled while rowing, so there's
no real conflict at runtime) into forward stroke impulses, with a cooldown
that makes well-timed strokes more efficient than spamming — deliberately
not a motorboat. `LandingZone` (`LandingZone_Beach`) ends the rowing state
and fires `OnLanded`, which `ObjectiveSystem` subscribes to.

---

## Objective Flow (`TRLM.Progression`)

`ObjectiveStep` enum (14 steps, `PreparationComplete` → `SliceComplete`) +
`ObjectiveSystem` (one instance on `WorldSystems`, `Advance()`/
`AdvanceTo(step)`, event `OnObjectiveChanged`). Automatic triggers wired
this sprint: `LandingZone.OnLanded` → `ReachLandingZone`, `DayNightSystem`
night transition → `NightBegins`, `SafeHouseArea` player-enter →
`ReachSafeHouse`, `FirePoint.OnLit` → `LightFire`, `SleepInteraction`
completing → `WakeNextMorning`. Steps without an automatic trigger yet
(`EnterCoastalForest`, `ReachAbandonedHouse`, `SearchHouse`,
`AcquireEssentialLoot`, `WolfThreat`) can be driven by `AdvanceTo()` calls
from a future scripted trigger/cinematic — the architecture supports it,
content wiring for those specific steps just wasn't authored this sprint
(documented here rather than silently left unclear).

---

## HUD (`TRLM.UI`)

`GameplayHUD` (`OnGUI`, matching `DebugHUD`'s existing style) shows
Health/Stamina/Hunger/Thirst/Battery only when relevant (not full,
recently changed, or below a warning threshold), a plain-text 10-slot
inventory list toggled by `I`, and short-lived objective-change
notifications. `SimpleTutorialPrompt` shows one-shot contextual prompts
(`SPACE — Row`, `E — Interact`, etc.) from the systems that need them,
not spammed every frame. Verified all HUD component references are wired
(not null) via Sub-Agent A's scene check.

---

## Known Placeholders / Honest Limitations (do not report these as fixed)

- No real pickup/loot meshes — primitives stand in.
- Grave marker is a runtime-built primitive cross, not authored geometry.
- Fire has no real particle VFX — a placeholder emissive object slot exists but isn't populated with real fire art.
- Body-carry movement penalty is stamina-only, not true speed reduction (see above).
- Wolf/companion still have no real locomotion animation (unchanged from Sprint 04 — see `AnimationPipeline.md`).
- 5 of 14 objective steps have no automatic scene trigger yet.
- `05_Neighborhood_Cinematic.unity` only got 3 placeholder hook GameObjects (`PreparationTrigger`, `EquipmentLoadPoint`, `DeparturePoint`) — no Timeline content, per the brief's explicit lowest-priority scoping.

## Performance Notes

Baseline (before this sprint's systems, Play Mode, production island scene):
CPU frame time ~45.8ms, GPU ~47.5ms. After full integration: CPU ~34.9ms,
GPU ~34.4ms — no regression observed (the difference is most likely normal
frame-to-frame/editor-state variance, not a real optimization from this
sprint's work; reported honestly rather than claimed as an improvement).
Companion `NavMeshAgent` destination updates are throttled to a periodic
timer (not every frame) to match the project's existing performance
conventions (see `WildlifeSystem.md`'s established patterns).
