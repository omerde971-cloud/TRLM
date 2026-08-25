# TRLM Development Log

---

## 2026-08-23 — Weather, Sanity & Visual Atmosphere Sprint 09

### Multi-agent execution
Main Claude (this session) wrote all C# systems, animation/architecture decisions, and scene
wiring. One Haiku sub-agent did visual-only polish on `94_Test_WeatherSanity` (rain particle
look, fog color, directional light mood, 5 dressing props reusing existing Environment prefabs) —
it introduced a real bug (rain material left as Opaque surface type despite a 0.35 alpha, which
rendered as a solid blue wall filling the screen instead of transparent streaks); caught and fixed
during review before commit, per the brief's "Main Claude reviews Haiku's result" rule.

### Weather
New `TRLM.Weather` namespace: `WeatherType` (Clear/Cloudy/LightRain/HeavyRain/Storm),
`WeatherProfile` (ScriptableObject data — five authored assets under
`Assets/_TRLM/Data/Weather/`), `WeatherSystem` (current/target profile with a smooth Lerp
transition, controlled-random cycling via per-profile hold-duration range + relative pick weight
so storms stay rare, `SetWeather`/`ForceWeather`/`ReleaseWeatherOverride`), `RainVisualController`
(camera-relative rain particle emission + RenderSettings fog + occasional storm lightning flash —
no world-scattered particles, one system parented under the player). `PF_RainVFX` and
`PF_WeatherSystem` prefabs; RainVFX is now baked into `PF_Player` itself so any scene with the
player prefab gets rain automatically.

### Survival connections (extended existing Sprint 05 foundation, not rebuilt)
- `WetnessSystem`: now actually gains wetness from `WeatherSystem.CurrentRainIntensity` when
  outdoors, on the same 0.5s tick that already checked shelter. Added a lightweight upward
  raycast so a roof (not just an authored SafeHouse marker) counts as shelter. Exposes
  `IsNearFire`/`IsSheltered` so other systems reuse this one scan instead of re-scanning
  FirePoint/WorldMarker themselves.
- `ColdExposureSystem`: three readable stages (Mild/Moderate/Critical, was two) — Moderate adds a
  `FirstPersonController` movement penalty on top of the existing stamina penalty. Drain now also
  factors `WeatherSystem.CurrentTemperatureModifier` (0 for Clear, so the island isn't permanently
  freezing). Warms automatically near a lit fire via `WetnessSystem.IsNearFire` (no second fire
  scan).
- `SleepInteraction`: now also gives a partial (not full) `PsychologicalState` and
  `ColdExposureSystem` recovery on waking, alongside the existing hunger/thirst/stamina/injury
  rest restore.

### Sanity / Morale — one coherent model, not two
New `PsychologicalState` (`TRLM.Survival`): a single 0-100 Stability value with four tiers
(Stable/Uneasy/Stressed/Critical), explicitly not a separate Sanity+Morale pair. Drain sources are
mostly event-driven (`HealthSystem.OnDamaged`, `RegionalInjurySystem.OnInjuryChanged`,
Hunger/ThirstSystem's `On*Changed`, `BurialZone.OnBurialComplete`) rather than each polling its own
subsystem; the genuinely continuous inputs (night, cold, isolation, companion proximity, shelter,
fire, daylight) share one 0.5s tick. Companion proximity gives a capped, diminishing recovery bonus
(dead companions don't count); `OnCompanionDied(CompanionId)` applies a one-time morale hit guarded
against duplicate application. `PsychologicalVisualEffects` drives a restrained Vignette-only URP
Volume (`VP_PsychologicalVignette.asset`) — no blur, no color overlay, no distortion. Aim sway and
stamina regen at Stressed/Critical reuse the exact `WeaponSway`/`StaminaRegenModifier` worst-wins
patterns from Sprint 07/05 — no competing modifier ownership.

### Perception foundation
New `PerceptionEventSystem` — a tiny static bus deliberately separate from `NoiseEvents` so a false
perception event is structurally incapable of being heard by wildlife or dealing damage.
`PsychologicalState` fires at most one of two example kinds (`DistantBranchCrack`/`DistantWhisper`)
every 45-120s while Stressed/Critical and alone — not a hallucination content library. Silhouette
test deferred (would have cost real time this pass; the audio-hook foundation exists for it later).

### Wildlife weather connection
`WolfPerception.WeatherHearingMultiplier` (static, set by `WeatherSystem`) mildly dulls hearing in
heavy rain (down to 0.75x, never lower) — a hook + one conservative value, not a rewrite.

### Companion / Sprint 08A carryover
No CompanionAI changes were needed — `PsychologicalState` hooks each companion's existing
`HealthSystem.OnDeath` directly via `CompanionIdentity`, so the "expose hooks for weather/morale"
ask from Sprint 08A required zero edits to Companion code. `CompanionAI.MoveSpeedMultiplier`
(already existed) remains available for a future storm-slows-companions pass; not wired this
sprint to avoid scope creep.

### Visual quality investigation
Checked `Assets/Settings/PC_RPAsset.asset`: MSAA was disabled (1). Tried enabling 4x MSAA as the
"obvious safe win" the brief suggested checking for — measured GPU frame time jump from ~0.5ms to
~85ms on the production island (17M triangles, an unoptimized Sprint 02 blockout). That is not a
safe change on this content, so it was **reverted**, not shipped. A shadow-resolution bump
(1024->2048) was also tested and reverted — GPU timing stayed noisy/high in the same session,
making it impossible to isolate a clean before/after this pass. **No URP setting changes were kept
this sprint.** Flagging for Codex/a dedicated profiling pass: the production island's polycount
itself is likely the real cost center, not the RP asset's quality knobs.

### Test scene
`Assets/_TRLM/Scenes/Tests/94_Test_WeatherSanity.unity`: Ground, a lean-to Shelter (Roof + Wall,
roof-raycast shelter detection), a SafeHouse trigger, a FirePoint, PF_Player + all four companions,
WeatherSystem, and `WeatherSanityDebugControls` (OnGUI force-weather/wetness/sanity/companion-death
buttons, same debug pattern as `BurialZone`/`SleepInteraction`).

### Tested live
Clear -> ForceWeather(HeavyRain) -> player wetness rose from 0 to 47 while outdoors -> moved under
the shelter roof -> wetness dropped to 0 and `IsSheltered` flipped true, all within seconds.
`PsychologicalState.DebugSetStability(30)` -> `OnCompanionDied(Jonah)` -> stability dropped to 5,
tier flipped to Critical, `StaminaSystem.RegenMultiplier` immediately read 0.6 (confirmed effect
application, not just the stored value). Duplicate `OnCompanionDied` call for the same companion
correctly no-op'd. Zero console errors across the entire pass, in both the test scene and after
adding `WeatherSystem` to `20_Island_Blockout`.

---

## 2026-08-22 — First-person: hands only, pitch-based hide when looking down

Second correction on the same feature: legs/feet are now always hidden too
(only the Arm submesh — hands — is ever shown), and the hand/arm mesh itself
additionally hides once the camera pitches down past 35° so glancing at the
ground doesn't show a dangling forearm/hand up close. `FirstPersonBodyMask`
now reads `CameraRoot`'s local pitch every frame (only swaps the one Arm
submesh material when the hidden/visible state actually flips — no per-frame
allocation) and restores the real arm material once the camera comes back up.
Hands stay visible through the whole 0-35° range, which covers normal
forward-facing play (walking, running, jumping, aiming/firing) — the hide
only kicks in when actively looking down at the ground/feet.

Honest limitation: this is a full submesh show/hide at a pitch threshold, not
a true "only the wrist stays visible" partial reveal — that would need either
a custom shader clipping by distance from the hand bones or a dedicated
viewmodel rig, both bigger than this fix's scope. Verified live at 0° (hands
visible) and 50° (hands, legs, torso, head all hidden — nothing floating).

---

## 2026-08-22 — First-person body: hands/legs only, not the full torso

Follow-up correction: the previous pass made the full Elias body visible in
first person (torso + limbs, head hidden), but the ask was hands and feet
only — a full visible torso reads wrong and isn't what most first-person
survival games do.

`CC_Base_Body` turned out to already be split into per-region submeshes
(Head/Body/Arm/Leg/Nails/Eyelash, each its own material) — no need to touch
the skeleton. Added `TRLM/Invisible` (a trivial `ColorMask 0 / ZWrite Off`
shader — no color, no depth write, so it can't occlude the camera either) and
a `MAT_Invisible` material. New `FirstPersonBodyMask` component (replaces the
previous `HideHeadForFirstPerson` bone-scale hack) swaps the Head/Body/Eyelash
submesh materials to invisible and disables the small head-attachment
renderers (eyes, teeth, tongue, tearline), leaving only Arm/Leg/Nails visible.
Verified live: torso and head fully gone, hands clearly visible at moderate
look-down angles, legs/thighs visible too (feet specifically still need a
steeper angle than tested to confirm — not re-checked exhaustively this pass).

---

## 2026-08-22 — First-person body, companion formation, forest fix, wind, mountain path

Small requested fix batch ahead of Sprint 09 (explicitly: fix these, then wait —
Sprint 09 itself was deferred until asked for by name).

- **First-person hands/feet**: `PF_Player` had no visible body at all (camera +
  invisible capsule only). Nested a `PF_Elias` instance ("FirstPersonBody")
  under `PF_Player`, driven by the same `AC_Human_Base`/Speed-parameter pattern
  as companions (`PlayerBodyLocomotionAnimator`). Hands are confirmed visible
  looking down at moderate angles. Feet are **not** reliably visible — the
  single combined body mesh's chest/torso occludes the view at steep downward
  angles from an eye-height camera; fixing that properly needs a dedicated
  first-person viewmodel (separate arm rig, or a pitch-based camera offset in
  `FirstPersonController`), which is out of scope for this pass. Added
  `HideHeadForFirstPerson` (collapses the `CC_Base_Head` bone to ~0 scale on
  the player's own body instance only, so the head doesn't block/clip the
  camera — standard first-person trick, doesn't touch the canonical `PF_Elias`
  prefab used elsewhere).
- **Companion formation**: tightened from `trailDistance=2.5`/wide angles
  (±45°/±110°, putting Lena/Noah almost beside the player) to
  `trailDistance=1.8` with a compact ±25°/±55° wedge behind the player — reads
  as a group walking together, ~1-2m apart, not spread into a wingman line.
- **Forest tree rotation bug**: root cause found — `PF_Tree_TypeA_01/02/03` and
  `PF_TreeCard_BG_1..5` (8 prefabs, ~524 placed instances) had their mesh
  children at identity local rotation, but the source meshes (`Tree_Trunk_01`,
  `Background_Tree_Atlas`, etc.) were authored Z-up, not Y-up — every tree in
  the forest was lying flat (world-space Y size ~2 vs X/Z ~10-15). Fixed with
  one `-90°` X corrective rotation per prefab (8 edits, propagates to all
  placed instances automatically) — confirmed live via Scene View screenshot,
  forest now reads as upright pines. The tree pack the user provided
  (`low-poly-forest-tree-pack.zip`) turned out to be the exact same asset
  already imported at `Assets/ThirdParty/Environment/TreePack/` — no new
  import needed, extracted copy discarded.
- **Wind sway**: new `WindSway` component (cheap sine-based local-rotation
  sway, no shader changes — the tree/grass materials are plain URP/Lit and a
  hand-rolled wind vertex shader risked breaking lighting for a restrained
  ask). Added to the `Branches` child of `PF_Tree_TypeA_*` (trunk stays rigid),
  the root of `PF_TreeCard_BG_*` (thin billboard canopy), and
  `PF_GrassPatch_Circle`. ~560 total instances (524 trees + 40 grass patches),
  phase-randomized per instance so the forest doesn't sway in lockstep.
- **Mountain path**: painted a winding `TL_ForestDirt` (sand/dirt, not
  concrete) trail on the production terrain's alphamap from
  `LandingZone_Beach` through all three safe houses up to
  `Landmark_MountainPeak`, 3.5m half-width with a 3m soft falloff blended
  proportionally against the existing layers (not a hard cutout). Verified via
  a top-down Scene View screenshot.
- Verified live: companions still Follow/Come Here correctly with the new
  formation, zero console errors across all playtesting in this pass
  (including the earlier `OnFootstep` AnimationEvent gap on the new
  `FirstPersonBody` — fixed by moving the no-op receiver onto the same
  GameObject as its `Animator`).

### Deferred
- Reliable first-person feet visibility (needs a real viewmodel or camera
  pitch-offset work).
- Nothing else deferred from this batch.

---

## 2026-08-22 — Companion visuals: real names, URP material fix, shared locomotion

Small integration pass ahead of Sprint 09, requested directly (not a numbered
sprint): fix the purple/pink companion materials, wire real locomotion
animation, and confirm the 5-person cast (Elias Ward + Dr. Mira Voss/Jonah
Reed/Lena Ortiz/Noah Bell) is fully represented — no 5th companion needed,
the "5" is Elias + the existing 4.

- **Purple material bug**: `npc_casual_set_00`'s `Materials/` folder ships
  Built-in-RP `Standard` shader materials (pink in this URP project). The pack
  already ships a `MaterialsUPR/` folder with identically-named URP/Lit
  equivalents — swapped all `SkinnedMeshRenderer`/`Renderer` material
  references on `PF_Mira`/`PF_Lena`/`PF_Noah` (75 total slots, 0 unmatched).
  `PF_Jonah_Companion` and `PF_Elias` already used real URP/Lit materials from
  Sprint 04 — not affected.
- **Companion display names**: `CompanionIdentity.displayName` updated to the
  full cast names (Dr. Mira Voss / Jonah Reed / Lena Ortiz / Noah Bell).
- **Locomotion animation**: `AC_Human_Base.controller` (Sprint 04's shared
  human Animator — Idle/Walk/Run states existed but Walk/Run had no motion, no
  parameters, no transitions) filled in with real Humanoid clips from
  `StarterAssets/ThirdPersonController` (`Stand--Idle` was already wired;
  added `Locomotion--Walk_N`/`Locomotion--Run_N`), a `Speed` float parameter,
  and Idle↔Walk↔Run transitions with hysteresis thresholds. New
  `CompanionLocomotionAnimator` component (separate from `CompanionAI` — nav
  logic stays animation-agnostic) drives `Speed` from `NavMeshAgent.velocity`.
  Assigned controller + driver to all four companions (Jonah already had the
  controller reference, just no working states). Also silenced the
  `OnFootstep` AnimationEvent console error the StarterAssets clips carry (no
  footstep SFX exists yet — no-op receiver added, real audio deferred).
  All other `AC_Human_Base` states (CrouchWalk, Digging, Rowing, etc.) remain
  the pre-existing NULL-motion placeholders — untouched, out of scope.
- Verified live: all four companions transition Idle→Run correctly while
  navigating, zero console errors, materials confirmed URP/Lit at runtime.

---

## 2026-08-22 — Companion Core Sprint 08A

### Scope
Navigation + command foundation only, generalized from the Sprint 05 Jonah-only
companion prototype to all four companions (Mira, Jonah, Lena, Noah). No
personality, threat reactions, rescue, or permanent-death work — those are
Sprint 08B–08E.

### Foundation reused, not rebuilt
`CompanionAI`/`CompanionCommandInput`/`CarryableCorpse`/`BodyCarry`/`BurialZone`
(Sprint 05/07) were already generic — no Jonah-specific branching existed in any
of them. `PF_Jonah_Companion` already carried the exact target component stack
(`NavMeshAgent`, `HealthSystem`, `CompanionAI`, `CarryableCorpse`,
`FactionMember`). Sprint 08A only added identity + formation + recovery on top
of that foundation.

### New
- **`CompanionId`** (enum: Mira/Jonah/Lena/Noah) and **`CompanionIdentity`**
  (data-only component exposing `Id`/`DisplayName`) — the "which companion is
  this?" hook Sprint 08B+ personality/threat/rescue systems read instead of
  adding per-character branches.
- **`CompanionAI` formation + recovery**: `formationAngle` per instance fans
  follow positions out around the player instead of one shared trail point;
  `avoidancePriority` is spread per-instance (`GetInstanceID()`-derived) so
  four agents don't deadlock over the same spot; conservative stuck-repath
  (agent has a destination, near-zero velocity for `stuckTimeout`) and
  off-NavMesh recovery (`NavMesh.SamplePosition` + `Warp`, only after
  `offMeshTimeout`, never mid-navigation teleports) were added to `Update`.
  `MoveSpeedMultiplier` hook added for future injury/personality use, same
  pattern as `WeaponSway`/`StaminaRegenModifier`.
- **`CompanionCommandInput`**: Shift+1/2/3 now command every companion in range
  instead of just the nearest one — the minimal "command all" hook the brief
  asked for, no squad UI added.
- **Companion prefabs**: `PF_Mira`, `PF_Lena`, `PF_Noah` built as Prefab
  Variants of the newly-added `npc_casual_set_00` humanoid NPC pack (already
  Humanoid-rigged — `body`/`footwear`/`lower_cloth`/`upper_cloth` under one
  `Animator`+`LODGroup` root), nondestructive to the third-party originals.
  `PF_Jonah_Companion` (existing CC_Base asset) kept as-is, just gained
  `CompanionIdentity` + `formationAngle`. Same component stack as Jonah on all
  four: `CapsuleCollider`(r0.35/h1.8), `NavMeshAgent`(same radius/height,
  `Companion` layer), `HealthSystem`(100hp), `CompanionAI`, `CompanionIdentity`,
  `CarryableCorpse`, `FactionMember`(PlayerTeam).
- **Test scene**: `Assets/_TRLM/Scenes/Tests/93_Test_Companions.unity` — ground,
  a narrow two-wall passage, two open-area obstacles, a ramp, baked legacy
  NavMesh, `PF_Player` + all four companions.

### Optional Sprint 07 micro-fix — DONE
Arm-injury reload-speed penalty. `WeaponController` already had the exact
`WeaponSway` source-keyed/worst-wins pattern to copy: added
`SetReloadSpeedModifier`/`ClearReloadSpeedModifier` + `ReloadSpeedMultiplier`
(default 1, applied to `def.reloadSeconds` in `ReloadRoutine`).
`RegionalInjurySystem.ApplyArmPenalty` now calls it alongside the existing sway
call. No duplicate injury logic in `WeaponController` — it stays ignorant of
injuries, same as before.

### Tested live (Play Mode, `93_Test_Companions`)
Follow (all four track the player with distinct formation offsets, verified via
a hard player teleport + position sampling), Wait (companion provably held
position through a player teleport — does not drift back), resume Follow
(re-pathed back to the player across the obstacle field), Come Here (arrived
and auto-transitioned to Wait), three-companion simultaneous Come Here through
the narrow passage (all arrived, spaced apart, no permanent stacking), kill →
`IsDead=true`, `NavMeshAgent.enabled=false`, `CompanionAI.enabled=false`,
`CarryableCorpse.InteractionPrompt="Carry Body"` (corpse/burial chain intact).
Zero console errors across the full pass. `cpuMainThreadFrameTimeMs` ≈ 8.5ms
with all four companions active — in line with Sprint 07's baseline, no
regression.

### Honest limitations / deferred to 08B+
No personality, threat warnings, Hide/Help, rescue, morale, or permanent-death
work (out of scope by design). Companions have no locomotion animation (same
placeholder-animation debt as before — `npc_casual_set_00` characters have no
Animator Controller wired, only Jonah has an Avatar in active use). No
production-scene companion swap performed — Jonah remains the only companion
placed in `20_Island_Blockout`; Mira/Lena/Noah exist as verified prefabs ready
for a later story sprint to place. `npc_casual_set_00` asset import surfaced in
`git status` alongside this sprint's work (team art asset drop, not authored
by this sprint) — left untouched beyond building the four prefab variants on
top of it.

---

## 2026-08-22 — Combat, Equipment & Injury Sprint 07

### Manual playtest checkpoint
No filled-in Ömer feedback found in project documents (only the checklist
template authored in Sprint 06) — recorded `MANUAL_PLAYTEST_PENDING` per the
brief's instruction and continued without blocking.

### Weapon asset audit
Confirmed **zero weapon 3D assets exist anywhere in the project**
(`Assets/ThirdParty/Weapons/` empty, nothing in `Assets_Source/`, no entries in
`AssetRegistry.md`). Zero-budget constraint stands. Built fully mechanically
functional weapons using primitive placeholder geometry rather than delaying the
sprint chasing real art — see `CombatSystem.md`.

### Completed
- **Multi-agent execution**: Sub-Agent A1 (Sonnet 5, equipment wheel + firearms),
  Sub-Agent A2 (Sonnet 5, melee + regional injury/bleeding/poison), Sub-Agent B
  (Haiku 4.5, production weapon/ammo placement), Sub-Agent C (Sonnet 5,
  performance/QA) — all sequential against the single live Unity Editor.
- **Equipment**: `PlayerEquipment` (4 physical slots, separate from the 10-slot
  inventory), `EquipmentWheelUI` (Tab-held, pauses the game via `Time.timeScale =
  0` — zero changes needed to `WolfAI`/`CompanionAI`/`DayNightSystem` since they
  all already key off `Time.deltaTime`).
- **Firearms**: pistol (semi-auto, 10-round mag) + shotgun (8-pellet spread,
  5-shell mag) — both hitscan, zero-alloc fire loop (confirmed via GC sampling:
  0 collections, 0 byte delta across 200 shots), real ammo types in the normal
  inventory, real-duration reload with correct partial-reload math and
  wrong-ammo-type rejection, recoil (`PlayerCamera.AddRecoilKick`, one additive
  method), sway (worst-penalty-wins modifier API), gunshot noise via the
  **existing** `NoiseEvents` bus (no second perception system).
- **Wolf combat needed zero `WolfAI.cs` changes** — it already handled
  death-on-damage correctly from Sprint 03's rockfall-damage work. Verified live.
- **Melee**: one knife, light-attack-only, stamina-gated, wall-blocking verified,
  cooldown-respecting.
- **Regional injury**: 6 body regions, weighted-random region assignment (no
  damage source currently provides precise hit location for player-received
  damage), arm/leg/torso/head effects each plugging into an **existing** modifier
  API (`WeaponSway`, `FirstPersonController` speed modifiers, `StaminaRegenModifier`)
  rather than inventing new ones. Real bleeding/poison (periodic-tick, not
  per-frame, built on Sprint 05's `IStatusEffect` foundation without rewriting
  it), bandage, fracture/trauma with real recovery timers accelerated by sleep.
- **Friendly fire**: `Faction`/`FactionMember` — player→companion damage blocked
  by default, verified live for both firearms and melee.
- **Two real bugs found and fixed during integration/QA** (not silently worked
  around):
  1. Firearm and melee raycasts had no `QueryTriggerInteraction.Ignore` — shots
     would have been silently absorbed by invisible trigger volumes
     (SafeHouseArea, LandingZone, pickup colliders). Fixed in both
     `WeaponController.cs` and `MeleeController.cs`, re-verified live.
  2. **Self-damage feedback loop**: `RegionalInjurySystem` reacted to its own
     bleeding/poison ticks' damage (which — like Sprint 05's Hunger/Thirst/Cold
     critical ticks — pass no damage `source`), re-rolling new injuries and
     potentially spawning new bleeds every tick. QA reproduced a 100→0 HP death
     spiral from two modest status effects alone. Fixed with a one-line
     `source == null` guard (internal/environmental damage vs. real attackers).
     Re-verified live: the exact reproduction now produces a normal,
     decelerating health curve, not a spiral.
- **Layer Collision Matrix** (deferred from Sprint 06, closed out this sprint):
  6 new layers (`Player`, `Wildlife`, `Companion`, `Loot`, `TriggerZone`,
  `Rockfall`), conservative disables only (Loot×Loot/Wildlife/Companion,
  TriggerZone×TriggerZone/Wildlife/Companion/Loot) — `Player×TriggerZone`/
  `Wildlife`/`Companion` deliberately left enabled since region/safehouse/
  landing/burial triggers and wolf combat depend on them. Verified live: the
  full Sprint 06 end-to-end objective chain still fires correctly.
- **Production integration**: one pistol + 4 rounds of 9mm hand-placed near the
  settlement safe house (sparse, authored discovery, not a loot-table roll, per
  the brief's explicit "weapons are valuable, ammo is more valuable, do not
  flood loot pools"). Shotgun/12-gauge deliberately withheld from production
  this sprint (reserved as a later exploration reward), remains available in
  the combat test scene for QA.
- **Performance**: no regression found. GC-clean fire loop, no leaked impact
  objects (there's nothing to instantiate to leak). Absolute frame-time numbers
  this pass were lower than Sprint 06's, but per Sprint 06's own documented
  session-warmup-noise finding, reported honestly as "no regression detected"
  rather than a confirmed further win.

### Documentation
New: `CombatSystem.md`, `InjurySystem.md`, `PerformanceReport_S07.md`. Updated:
`Architecture.md`, `GameplayIntegration.md`, `AssetRegistry.md`,
`ManualPlaytestChecklist.md`, this file.

### Honest limitations
No reload-speed penalty from arm injury (no hook exists on `WeaponController` for
it yet). No heavy melee attack. `HypothermiaStatusFlag` unified-status bridge
skipped (explicitly optional). No production poison source (test trigger only).
Rockfall pooled rocks remain on the `Default` physics layer. All weapon visuals
are primitive placeholders. Blender still unavailable (unchanged from Sprint 04,
not touched this sprint).

---

## 2026-08-22 — Vertical Slice Completion & Performance Sprint 06

### Completed
- **Git initialized** at the project root (was missing entirely — Sprint 05 noted this blocked worktree isolation). `.gitignore` authored for Unity (`Library/`, `Temp/`, generated `.csproj`/`.sln`, `Assets_Source/`). Baseline commit `847bec8 "Sprint 05 vertical slice baseline"` (2020 files) made before any Sprint 06 changes.
- **Multi-agent execution**: Sub-Agent B (Sonnet 5, gameplay integration — objective triggers, inventory use/drop, generic movement-modifier API), Sub-Agent A (Sonnet 5, performance profiling/optimization), Sub-Agent C (Haiku 4.5, scene validation) — run sequentially against the single live Unity Editor (worktree isolation was available via the new git repo but not used for these, since all three needed live scene mutation, which the brief itself said must stay Main-Claude-coordinated).
- **Real Sprint 05 gaps found and fixed during this sprint's own baseline/testing work** (not by a sub-agent — found directly): no `FirePoint` instance existed anywhere in the production scene despite the class being built in Sprint 05 (placed one, `DEV_Placeholder_FirePoint_Settlement`, near the settlement house); no rowboat instance existed in the scene at all (placed one at sea, `(400, 3, -30)`); `SliceComplete` had no automatic trigger anywhere (added one line to `SleepInteraction.cs` — waking after sleep now also advances to `SliceComplete`).
- **Generic movement modifier API** added to `FirstPersonController.cs` (`SetSpeedModifier`/`ClearSpeedModifier`/`SetSprintBlocked`, worst-penalty-wins, same pattern as Sprint 05's `StaminaRegenModifier`) — `BodyCarry` now applies a real speed slowdown while carrying a corpse, not just a stamina penalty.
- **5 remaining objective auto-triggers wired**: `EnterCoastalForest`/`ReachAbandonedHouse` (new reusable `RegionEntryTrigger`), `SearchHouse` (any successful `PickupItem` pickup), `AcquireEssentialLoot` (real inventory-state check against water+food, order-tolerant — already-had-items case verified live), `WolfThreat` (driven by real `WolfAI.CurrentState`, not a proximity sphere — `WolfAI` already exposed a safe public state read, no `WolfAI.cs` changes needed).
- **Inventory Use UX**: selected-slot concept, `UseSelectedItem()` (food→Eat, water→Drink, medicine→Heal, battery→flashlight replace), Drop now drops the selected slot instead of always slot 0.
- **Soft-lock pass**: Wood added as a guaranteed item in `LootTable_House` (previously entirely absent from the house table).
- **Performance**: full 7-location before/after profiling (`Documents/PerformanceBaseline_S06.md` → `Documents/PerformanceReport_S06.md`). Real fixes applied: shadow cascades 4→2, shadow resolution 2048→1024, 285 background tree-card renderers set to not cast shadows. **Important honest finding**: most of the baseline's 30-48ms readings turned out to be Play-session/measurement warm-up cost, not a persistent per-frame scene cost — re-measuring the same locations later in the same session (before any fix) already dropped readings to 14-19ms. The shadow/tree-card fixes are real and kept, but a genuine built-Player Profiler pass (not this MCP/Editor harness) is still needed to fully separate warm-up from real cost — flagged as a manual to-do.
- **Full end-to-end objective flow verified live**, in order, via real trigger/interaction code paths (not fake `AdvanceTo` calls): boat→landing zone→coastal forest region→inventory-driven loot check→forced night→forced wolf Alert state→real `FirePoint.Interact()` (consumed Wood)→real `SleepInteraction.Interact()`→`WakeNextMorning`→`SliceComplete`. Zero console errors across the full run.

### Documentation
New: `PerformanceBaseline_S06.md`, `PerformanceReport_S06.md`, `ManualPlaytestChecklist.md`. Updated: this file.

### Honest limitations
Objective progression is deliberately order-tolerant (a fast/unusual player path can jump `Current` ahead of intermediate steps) — this matches the sprint brief's own explicit robustness requirement, not a bug, documented here so it isn't mistaken for one later. Layer Collision Matrix, `WolfAI` distance-based tick throttling, and `WetnessSystem`'s periodic `FindObjectsByType` call were all investigated and left as documented recommendations rather than implemented (each judged too risky or out-of-scope-file for this sprint). Rowboat/FirePoint placement were minimal/placeholder positioning, not level-design-authored.

---

## 2026-08-22 — Gameplay Integration Sprint 05: Vertical Slice Core Loop

### Completed
- **Unity MCP verified working** (not reinstalled — confirmed via `editor_status`, `get_scene_hierarchy`, live `eval` calls throughout the sprint).
- **Multi-agent execution**: 2 sequential gameplay-code agents (Sonnet 5, since the project has no git repo and both needed exclusive live access to the single Unity Editor instance) built inventory/loot/survival/fire/day-night/boat/companion/burial/HUD/objective systems; 1 scene-integration agent (Haiku 4.5) placed loot spawns and cleaned up hierarchy duplicates. Full delegation record in `GameplayIntegration.md`.
- **10-slot `PlayerInventory`** + `PickupItem`/`LootTable`/`LootSpawnPoint` — 5 pre-existing Sprint 02 loot markers converted from inert to functional, first house loot guarantees water+food.
- **Hunger/Thirst/Wetness/Cold** + `TeamProvisions` (shared food/water) + `StaminaRegenModifier` (worst-active-penalty pattern, avoids multiplicative over-penalization) — one additive line added to `StaminaSystem.cs` (`RegenMultiplier` property), nothing else in that file touched.
- **Flashlight** (F toggle, battery drain/flicker, R to replace battery — R reused deliberately since Reload is otherwise unused this sprint).
- **Real `DayNightSystem`** replacing the `DebugWorldTimeSource` placeholder — same `IWorldTimeSource` interface, zero AI code changes, verified live that wolves correctly read the new day/night state.
- **Fire** (`FirePoint`, Wood-costed, static lit-fire registry) + a soft wolf-avoidance hook (`WolfFireAvoidance`, separate file, `WolfAI.cs` untouched).
- **Safe house + sleep**: existing Sprint 02 safe-house marker turned functional (`SafeHouseArea` + `SleepInteraction`), sleep skips to morning and partially restores survival stats.
- **Rockfall player damage**: `RockfallPlayerDamage` on all 3 existing zones, subscribes to `RockfallZone.OnRockImpact`, impulse-thresholded + debounced so gentle contact does nothing and one rock can't multi-hit per frame.
- **Rowboat**: `RowboatController` (stroke-timed SPACE rowing, diminishing returns on spam) on top of the existing, untouched `BuoyancyController`; `LandingZone` ends rowing and advances the objective.
- **Companion (Jonah)**: `PF_Jonah_Companion.prefab`, `CompanionAI` (Follow/Wait/Come-Here via a deliberately-scoped raw-keybind exception, 1/2/3), reuses the existing `HealthSystem`. **Body carry + burial**: verified live — damaging the companion to death correctly disables its `NavMeshAgent`, `BodyCarry` re-parents the corpse under a new `CarryAnchor`, `BurialZone_01` completes a timed burial and spawns a placeholder grave marker.
- **Minimal HUD + tutorial prompts**, **14-step `ObjectiveSystem`** with 9 of 14 steps auto-triggered from the new systems (5 remain `AdvanceTo()`-only, documented, not yet content-wired).
- **Performance**: baseline ~45.8ms CPU / 47.5ms GPU frame time (Play Mode, production island) vs. ~34.9ms/34.4ms after full integration — no regression found (difference read as normal variance, not claimed as an optimization).

### Documentation
New: `GameplayIntegration.md`. Updated: `Architecture.md`, `WildlifeSystem.md` (this file).

### Honest limitations (see `GameplayIntegration.md` for full detail)
Body-carry movement penalty is stamina-only (no `FirstPersonController` speed hook exposed); fire/grave-marker/pickup visuals are primitive placeholders; 5 objective steps lack automatic triggers; neighborhood scene got only 3 placeholder hooks, no Timeline content (explicitly lowest sprint priority). Wolf/companion locomotion animation gaps are unchanged from Sprint 04.

---

## 2026-08-22 — Character & Creature Material + Animation Sprint 04

### Completed
- **Human material repair**: root-caused the white/untextured characters — Reallusion CC3+
  "Digital Human Shader" exports have no plain diffuse texture at all, only specialized
  mask/detail maps (`_ao`, `_NBMap`, `_BCBMap`, etc. — `_BCBMap` confirmed via pixel
  sampling to be a grayscale blend mask, not a color map). Extracted all 3 base FBX's
  embedded materials to standalone assets (`materialLocation = External`), repaired all
  51 materials (17 × 3 meshes) with realistic skin/eye/mouth `_BaseColor` values + the
  real normal (`_NBMap`) and AO (`_ao`) maps wired in. Full details: `CharacterMaterialAudit.md`.
- **5 named human prefabs created**: `PF_Elias`, `PF_Mira`, `PF_Jonah`, `PF_Lena`, `PF_Noah`
  under `Assets/_TRLM/Prefabs/Characters/`. Since only 3 base meshes exist for 5 characters,
  the 2 reused pairs (Jonah/Noah on `CC3_Base_Plus`, Mira/Lena on `Neutral_F`) got their own
  duplicated + re-tinted skin materials so they're visually distinguishable despite sharing
  geometry.
- **Humanoid rig setup**: all 3 base FBX's `animationType` switched `Generic → Human`; Unity's
  auto-mapper succeeded with zero errors on all 3 (55 bones, valid avatars) — confirms the
  Reallusion CC3 skeleton is Humanoid-compatible, which is what made Blender-free human
  animation authoring possible this sprint.
- **First real human animation**: `Anim_Human_Idle_Breathing.anim`, authored via Humanoid
  muscle curves (no Blender needed), verified live in Play Mode and pose-compared against
  the Animator disabled (confirmed the initial "hunched" look is the rig's own bind pose,
  not an animation bug — no fix needed). `AC_Human_Base.controller` created with this working
  Idle state + 19 named placeholder states for the rest of the required set (not yet authored
  — honestly reported as incomplete rather than faked).
- **Wolf material fix**: replaced the flat uniform-grey materials with a believable two-tone
  dark-back/light-underbelly coloration matching the mesh's existing material zones.
- **Wolf substitute animation**: since the wolf mesh has zero rig/bones (confirmed, unfixable
  without Blender), restructured `PF_Wolf.prefab` to move its mesh onto a new `Visual` child
  and added a small root-transform idle bob/sway animation (`Anim_Wolf_IdleSway_SUBSTITUTE.anim`)
  — clearly labeled as a temporary standstill-only substitute, not real locomotion. Also fixed
  a real bug found during testing: `WolfAI.Update()` could throw `GetRemainingDistance` errors
  if the agent was ever off the NavMesh (spammed 1000+ console errors during an off-mesh test
  spawn) — added an `agent.isOnNavMesh` guard.
- **Bear/Boar/Snake/MountainGoat**: confirmed and honestly reported as `NO_ASSET` — zero 3D
  models exist for any of them. No fake placeholder creatures were created. Full detail:
  `CreatureMaterialAudit.md`.
- **Blender pipeline**: confirmed genuinely unavailable on this machine (`blender.exe` does
  not exist anywhere under the Blender install directory or common alternate locations) —
  reported as a hard blocker rather than faked or silently skipped. Full detail:
  `AnimationPipeline.md`.
- **New validation scene**: `Assets/_TRLM/Scenes/Tests/91_Test_CharacterMaterials.unity` —
  all 5 human prefabs + the wolf lined up for visual material/animation spot-checking.
  Verified live: real skin-tone materials with visible normal-map detail (not flat white),
  distinct tones between mesh-sharing character pairs, consistent idle pose/animation across
  all 5.

### Documentation
New: `CharacterMaterialAudit.md`, `CreatureMaterialAudit.md`, `AnimationPipeline.md`.

---

## 2026-08-22 — Fix: Removed "Vecindario" Neighborhood Backdrop from the Island

Ömer flagged that a "mahalle" (neighborhood) object was present on the production island —
this was **`Vecindario`** ("neighborhood" in Spanish), a large multi-building street-block
mesh bundled inside the `Abandoned_House.fbx` model itself (originally kept active as
distant window-view set dressing per Sprint 02's reasoning). That reasoning is now
overridden: the neighborhood/departure-prep concept belongs only in
`05_Neighborhood_Cinematic.unity`, never physically on the island, and a full street block
also numerically contradicted the "small former settlement, 3-6 structures" story
established in `WorldDesign.md`. Disabled `Vecindario` on both `Settlement_MainHouse` (the
island scene instance) and `PF_AbandonedHouse.prefab` (the production prefab, so future
placements don't have it either). The original third-party FBX itself was not modified.

---

## 2026-08-22 — World Gameplay Sprint 03: Wildlife, Wolf AI, Navigation, Polish

### Completed
- Reusable wildlife spawn architecture (`WildlifeSpeciesProfile`, `WildlifeSpawnZone`, `WildlifeSpawner`, `WildlifeSpawnManager`, `WildlifeDespawnWatcher`) under `Assets/_TRLM/Scripts/AI/Wildlife/`
- `IWorldTimeSource`/`DebugWorldTimeSource` day-night abstraction so wolf night-behavior is testable now without a real day/night system
- `NoiseEvents` static bus + `PlayerNoiseEmitter` — turns the existing, unmodified `FirstPersonController`'s movement into world noise (walk/sprint/crouch/land) with zero changes to the controller itself
- Full wolf AI (`WolfAI`, `WolfPerception`) — all 9 required states, sight+sound perception, lightweight pack alert/flanking/attacker-cap, damage via the **existing** `IDamageable`/`HealthSystem` (no new health system)
- `PF_Wolf.prefab` configured with `NavMeshAgent`/`WolfPerception`/`HealthSystem`/`WolfAI`; 5 `WildlifeSpeciesProfile` assets created (Wolf fully wired, Bear/Boar/Snake/MountainGoat data-only pending real assets)
- NavMesh baked (collider-based) for both `20_Island_Blockout.unity` and the new `90_Test_AI.unity`; **entire 8-segment primary route confirmed `PathComplete` end-to-end**
- 4 production wolf zones converted from markers to functional spawners; verified live (9 wolves spawned, full detect→chase→attack→damage→retreat cycle confirmed)
- Rockfall (3 zones) and ocean/rowboat buoyancy re-tested live — both still stable, no regressions
- Tree collision strategy: 28 route-relevant trees got cheap `CapsuleCollider`s (of ~520 total)
- Rock collider audit: 97 high-poly `MeshCollider`s downgraded to `BoxCollider`; 116 low-poly ones kept
- GPU Instancing enabled on all 17 environment materials; LODGroup creation explicitly skipped and documented (no real lower-detail meshes exist in the source packs — faking one was rejected per sprint instructions)
- `Documents/WildlifeSystem.md` created; `Architecture.md`/`WorldDesign.md` updated

### Real bugs found and fixed this sprint
1. **Wolf attack dealt zero damage**: `GetComponentInParent<IDamageable>()` searched the wrong direction (HealthSystem lives on a child of the player root, not a parent). Fixed to `GetComponentInChildren`; verified live (player health 100→45 across an engagement).
2. **Process pitfall, not a code bug**: baking the island's NavMesh, then switching scenes without saving first, silently discarded the bake. Re-baked and saved correctly; documented in WildlifeSystem.md so it doesn't happen again.
3. A transient Unity asset-database desync prevented `PlayerNoiseEmitter.cs` from compiling into the assembly despite having no syntax errors and no console error — delete+recreate the file fixed it. Noted here in case the pattern recurs.

### Known Issues
- Wolf has no visual animation (source asset has no rig at all — pre-existing, not new).
- No "reached a safe structure" detector for wolf disengagement (only indirect, via line-of-sight).
- Rockfall rocks don't yet damage the player on impact (optional per brief, not implemented this pass).
- Bear/Boar/Snake/MountainGoat have no 3D assets — profiles exist, spawners self-disable safely.

### Deferred Work
Inventory, weapons, melee combat, companions, sanity/hunger/thirst, full weather, full day/night
visuals, save system, final UI, final cave, final cinematics — all explicitly out of scope per
sprint brief.

---

## 2026-08-22 — Post-Sprint Fixes: Hierarchy Cleanup, Grass Replacement, Attribution

### Completed
- **Fixed a real bug**: duplicate empty cluster GameObjects were left behind in `Forest` (8) and `Rocks` (10) groups from the original placeholder pass — Ömer correctly caught this as "trees not added" (they were added, but the empty duplicates made the hierarchy confusing/misleading). Removed all 18 empty duplicates; only the populated real-asset clusters remain.
- Imported a 3rd local pack — **Grass Patches (Circle)** (`grass-patches-circle.zip`, author brandon_grey) — same recurring FBX unit-scale bug found and fixed (`globalScale = 0.12`).
- Removed 158 low-poly grass tuft objects (8 tris each) baked into the abandoned house model's own geometry, replaced with 40 real grass patch instances around the same yard footprint, per Ömer's explicit instruction.
- **Licensing resolved**: Ömer confirmed the creator names for all 3 local packs — commercial use is open, attribution required. Updated `AssetRegistry.md` from `LICENSE_UNVERIFIED` to confirmed, with a new "Attribution / End-Credits Requirement" table (99 Mil → trees, PolyOne Studio → rocks, brandon_grey → grass) that must appear in the game's end-credits screen.

### Known Issues
- None new. Scene console remains clean.

---

## 2026-08-22 — World & Level Design Sprint 02 (extended pass, same day)

### Completed
- Imported 2 local user-provided asset packs (low-poly forest tree pack, stylized rock pack) into `Assets/ThirdParty/Environment/`, license status logged as `LICENSE_UNVERIFIED` (used on Ömer's direct instruction, no bundled license file — see `AssetRegistry.md`)
- Built 20 reusable prefabs under `Assets/_TRLM/Prefabs/Environment/` (4 full trees, 5 background tree cards, 11 rocks)
- Replaced ~520 placeholder capsule trees and ~213 placeholder cube rocks across the island with real geometry, same cluster composition/density as the blockout pass
- New `TRLM.World.BuoyancyController` — sampled-wave buoyancy (no fluid sim), verified stable in Play Mode (boat heaves/rolls, no jitter/tunneling/spin-out); rowboat moved into open water for the sea-approach shot
- New `TRLM.World.RockfallZone` — pooled, authored rockfall events, 3 zones (Rock Belt, Mountain Pass, Summit Approach); found and fixed a real bug where pooled rocks had no Collider and fell through the world indefinitely; re-verified live — rocks now land correctly and return to pool
- New scene `Assets/_TRLM/Scenes/Production/05_Neighborhood_Cinematic.unity` — compact, separate departure-prep set (house facade, trailer+boat, gear props, 5 friends, 3 cinematic camera framing points)
- Updated `Documents/AssetRegistry.md` and `Documents/WorldDesign.md`

### Known Issues
- Tree/rock pack licensing unverified — flagged clearly, not blocking, needs Ömer's source confirmation before commercial ship.
- Neighborhood cinematic scene reuses only 3 distinct character base meshes for 5 friends (pre-existing limitation, not new).
- No LODGroup/GPU-instancing set up on the new real vegetation/rocks yet.

### Deferred Work
Same as the initial Sprint 02 pass — cave interior, climbing/traversal mechanics, wildlife/human AI, scripted set-pieces, final loot logic, weather, save system, cinematic camera-switching logic.

---

## 2026-08-22 — World & Level Design Sprint 02

### Completed
- Procedural 800×800m island terrain (`IslandTerrainData.asset`), 8-region elevation curve (sea shore → summit), Perlin ridge/detail noise, hand-flattened landing cove for a natural jagged coastline
- 5 URP TerrainLayers created from the P0 CC0 textures, alphamap painted by slope + region (grass/forest-dirt near shore, rock on steep slopes, mud/wet in low pockets)
- Ocean plane (Uber Stylized Water, waves+shoreline enabled) placed covering the shoreline with no visible gap; planar reflection volume added
- Rowboat placed at a flattened landing cove
- Settlement built: real `PF_AbandonedHouse` (marked `SafeHouse_01`) + 3 primitive ruin clusters (foundation, storage shed, fence line)
- ~490 placeholder forest "trees" (capsule primitives, 2 shared materials) across coastal/deep forest/rock-belt-fringe with intentional density variation (35%/62%/28%) — **forest pack still blocked**, see notes below
- ~220 placeholder rocks across Rock Belt / Mountain Pass / Summit / coast
- New `TRLM.World.WorldMarker` component (one flexible class covering SafeHouse/LootPoint/Traversal/SetPiece/WildlifeZone/HumanThreatZone/Landmark/StorytellingProp/WaterSource) — ~40 markers placed across all 8 regions
- `PF_Player` placed at the landing beach; gravity/terrain collision verified live in Play Mode
- Full `WORLD` hierarchy per spec (Terrain/Ocean/Environment/GameplayMarkers/WildlifeZones/HumanThreatZones/Landmarks/StorytellingProps/WaterSources/PlayerTesting)
- `Documents/WorldDesign.md` created

### Known Issues
- Forest and rocks are primitive placeholders, not real assets — forest specifically blocked on the same Blender-not-installed issue from Sprint 01/P0 audit (unchanged this sprint, not re-attempted since it wasn't this sprint's blocker to solve).
- Terrain/rock/ocean composition has not been walked end-to-end by a human; only automated position/collision checks were run (see WorldDesign.md → Known Limitations).
- No LOD/occlusion/GPU-instancing set up — flagged for Codex, expected at this stage.

### Deferred Work
Final vegetation/rock art, cave interior, climbing/traversal mechanics, wildlife AI, human threat AI, scripted set-piece sequences, final loot logic, weather, save system — all explicitly out of scope per sprint brief.

---

## 2026-08-22 — Foundation Sprint 01

### Completed Systems
- Full `Assets/_TRLM/` production folder structure (Scripts subfolders, Scenes/Production+Tests, Prefabs/Player, Settings, Tests/EditMode, etc.)
- Input handling (`PlayerInputHandler`) — code-defined `InputAction`s for all 14 requested bindings, centralized so no other script polls devices directly
- First-person movement (`FirstPersonController`) — walk/sprint/crouch/jump/gravity/slope-slide/grounded detection, smoothed acceleration
- Mouse look (`PlayerCamera`) — clamped pitch, yaw on body root, decoupled from Health/Stamina
- Stamina system (`StaminaSystem`) — sprint drain, jump cost, delayed regen, all values inspector-configurable
- Health system (`HealthSystem`) — damage/heal/death event, implements `IDamageable`
- Damage interface (`TRLM.Core.IDamageable`)
- Interaction system (`InteractionOrigin`, `IInteractable`) + 3 proof objects (`TestDoor`, `TestPickup`, `TestButton`)
- Debug HUD (`DebugHUD`, F3 to toggle) + interaction prompt UI (`InteractionPromptUI`)
- `PF_Player.prefab` — fully wired hierarchy (Controller / CameraRoot+MainCamera / InteractionOrigin / Systems)
- Prototype test scene `Assets/_TRLM/Scenes/Tests/10_Prototype.unity` — ground, ramp, 5-step stairs, obstacles, elevated platform, small enclosed test room with door/pickup/button
- 11 EditMode unit tests (Health ×5, Stamina ×6) — all passing
- `Documents/Architecture.md` created

### Known Issues
- **Input simulation limitation**: `InputSystem.QueueStateEvent` in this headless/automated environment set the underlying keyboard control's raw value correctly, but the `Move` action's 2DVector composite never reported the change (`ReadValue<Vector2>()` stayed `(0,0)` despite the bound control reading `1`). This blocked fully-automated verification of WASD movement/sprint/crouch inside this session. **Root cause not conclusively identified** — likely an event-processing quirk specific to driving the Input System outside its own `InputTestFixture`, not necessarily a bug in `PlayerInputHandler` itself (the composite binding structure was inspected and is correctly formed: 4 controls, correct part names/paths).
  - What *was* verified live in Play Mode: gravity + `CharacterController` grounding (player fell from y=1 to a resting ~y=0.08 and stopped, confirming collision + grounded detection work), and the full raycast→`IInteractable`→`Interact()` pipeline (directly proven against `TestButton`, correct prompt text returned, `Interact()` executed without error).
  - **Action needed from Ömer**: manually test WASD/mouse-look/Sprint/Crouch/Jump in the Editor with a real keyboard — if movement doesn't work, the bug is real and needs a fix; if it does work, this was purely a simulation-harness limitation.
- A real (now-fixed) bug was caught during this manual interaction test: `TestButton` used `Renderer.material` (which instantiates and leaks a unique material copy per call) instead of a `MaterialPropertyBlock`. Fixed.
- One pre-existing, unrelated Unity console error persists across sessions: `ExecutionEngineException: String conversion error: Illegal byte sequence` from Unity's own `QuickInstall` package, triggered by the Windows account's non-ASCII username (`ömer`). Not caused by this sprint's code and out of scope to fix (would require renaming the OS user account or patching a Unity-internal package).

### Deferred Work (explicitly out of scope this sprint)
Inventory, loot, weapons, shooting, melee, wolf AI, companion AI, rain,
day/night, hunger, thirst, temperature, sanity, save system, equipment
wheel *logic* (input binding only exists), rowing system, production
terrain/island, cinematics.

### Follow-ups for a future sprint
- Manually verify movement/camera/sprint/crouch/jump with real input (see Known Issues above)
- Wire `PlayerCamera` FOV/sensitivity into a Settings menu once one exists
- Consider adding an `InputActionAsset` version of the bindings later if the team wants a visual editor for rebinding — current code-only approach was a pragmatic choice for this sprint, not a permanent architectural decision
