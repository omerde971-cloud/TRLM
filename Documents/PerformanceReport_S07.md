# TRLM Performance Report — Sprint 07 (Combat / Equipment / Injury QA Pass)

**Captured:** 2026-08-22, Sub-Agent C (independent QA pass, post-orchestrator fixes).
Orchestrator had already fixed a `QueryTriggerInteraction.Ignore` bug in
`WeaponController.cs`/`MeleeController.cs` and built a Layer Collision Matrix
(new layers `Player`/`Wildlife`/`Companion`/`Loot`/`TriggerZone`/`Rockfall`,
several pairs disabled) before this pass began. This report independently
re-verifies performance and runs the full Section 46 functional test suite.

Same measurement caveat as `PerformanceReport_S06.md` applies in spirit, but
this session's `get_performance_stats` calls *did* return non-zero
`drawCalls`/`triangles`/`vertices` (unlike S06's harness) — reported for
context, but `cpuFrameTimeMs`/`gpuFrameTimeMs` remain the primary signal since
that's what S06 is comparable against.

---

## BEFORE (cited from PerformanceReport_S06.md, immediately-post-teleport samples)

| Location | CPU (ms) | GPU (ms) |
|---|---|---|
| C Settlement | 15.0 | 11.8 |
| B Deep Forest | 14.7 | 11.8 |
| D Wolf (day) | 17.3 | 12.9 |

## AFTER (this pass, `20_Island_Blockout.unity`, two samples per location a few seconds apart)

| Location | CPU (ms) | GPU (ms) |
|---|---|---|
| Settlement/SafeHouse (420,22,170) | 9.0 / 7.9 | 4.2 / 4.5 |
| Deep Forest (300,46,320) | 7.5 / 6.7 | 3.7 / 3.6 |
| Wolf zone (400,66,350), idle | 6.8 / 7.7 | 4.5 / 4.7 |
| Wolf zone, pistol equipped + 8 shots fired | 6.5 / 5.8 | 4.5 / 4.2 |

**Honest read:** every location is *faster* than the S06 numbers by a wide
margin (roughly half the CPU time, a third of the GPU time), but per S06's own
finding, this harness has substantial session-warmup noise — the S06 report
itself showed the *same* scene dropping from 30-48ms to 15-19ms just from
re-sampling later in one play session with zero code changes. I did not
re-run a "cold" first-sample comparison this pass, so I cannot rule out that
today's lower numbers are partly a warmer session/editor state rather than a
Sprint 07 win. What I *can* say with confidence from a same-session,
apples-to-apples comparison: **firing the pistol 8 times in the wolf zone
produced no measurable frame-time increase** (6.5-5.8ms/4.5-4.2ms while firing
vs. 6.8-7.7ms/4.5-4.7ms idle — within the same noise band, arguably slightly
lower). No regression from the new combat systems or the layer-matrix change
is visible in this data. A real Development Build + Profiler pass remains the
only way to fully separate Editor overhead from genuine cost, as S06 also
noted.

---

## GC allocation check (firing loop, Section 39-40 zero-alloc target)

Method: `System.GC.CollectionCount(0)` and `System.GC.GetTotalMemory(false)`
before/after tight `WeaponController.TryFire()` loops (cooldown bypassed by
resetting `WeaponRuntimeState.nextFireTime` between calls so all shots land
in a single frame — real per-shot allocation would still show up in
`GetTotalMemory` even without a collection).

- 20 shots: Gen0 count 954 → 954 (no collections), memory delta **0 bytes**.
- 200 shots (larger sample): Gen0 count 955 → 955 (no collections), memory
  delta **0 bytes**.

This matches the code: `FireShots` uses a single non-allocating
`Physics.Raycast` (no `RaycastAll`, no per-shot array), and `OnImpact` is a
plain event invocation with no `Instantiate` call (see pooling section below).
**Caveat honestly stated:** `GetTotalMemory(false)` doesn't force a collection
and Editor/other systems (UI, physics, other MonoBehaviours) are also
allocating in the background — a flat delta across 200 iterations is strong
evidence of no *per-shot* allocation, but this is not a lab-grade Memory
Profiler capture. A Memory Profiler window pass (deep-profile a single
`TryFire()` call) is the correct way to fully close this out, per the brief's
own suggestion. One caveat worth naming: `DetermineSurfaceType` reads
`hitCollider.tag`, which historically allocates a string per call in some
Unity versions — the flat GC delta suggests this either isn't allocating in
6000.4.8f1 or is too small to register in 200 calls; not fully ruled out at a
finer grain.

## Impact/pooling check (Section 39)

Code review confirms `WeaponController.ApplyHit` does **not** instantiate
anything — `OnImpact?.Invoke(hit.point, hit.normal, surfaceType)` is the only
side effect, a pure event fire with no VFX prefab spawn (matches the class's
own doc comment: "VFX/audio hook, no VFX required this sprint"). Empirically
confirmed: `GameObject.FindObjectsByType<Transform>().Length` before and after
10 rapid shots at a wall/target was **identical (2604 → 2604, delta 0)** in
the island scene. No leak, because there is nothing to leak — there is no
pooling system because there is no instantiation to pool.

---

## Section 46 functional test results (92_Test_Combat.unity, Play Mode)

### FIREARMS — all PASS
| Test | Result |
|---|---|
| Fire consumes ammo (magazine decrements) | PASS — 10 → 9 after one shot |
| Empty firearm cannot fire | PASS — `TryFire()` returns false, `OnDryFire` fires, `OnFire` does not, no damage |
| Reload consumes reserve ammo | PASS — 50 → 43 reserve after a 7-round full reload |
| Reload respects magazine capacity (partial reload) | PASS — mag capacity 10, reserve capped at 3 → magazine ends at 3 (not 10), reserve drops to 0 |
| Correct ammo type required | PASS — pistol reload with only 12-gauge in inventory: `TryReload()` returns false, magazine unchanged |
| Damage applies once per hit | PASS — single `OnWeaponHit` invocation per shot, damage = weapon's `damage` value exactly (22 for pistol) |
| Gunshot sends `NoiseEvents.OnNoise` (pistol) | PASS — loudness = 65 (matches `WPN_Pistol.noiseLoudness`) |
| Gunshot sends `NoiseEvents.OnNoise` (shotgun, independently re-verified) | PASS — loudness = 95 (matches `WPN_Shotgun.noiseLoudness`) |

### EQUIPMENT — all PASS
| Test | Result |
|---|---|
| Sidearm/long-gun/melee slots populate on `TryEquip` | PASS — pistol→Sidearm, shotgun→LongGunA, knife→Melee, each `IsSlotFilled` true |
| TAB pauses game (`Time.timeScale`→0) and un-pauses on release | PASS (tested via reflection into `EquipmentWheelUI.Open()`/`Close()`, the exact code path the polled `EquipmentWheelHeld` triggers) — timeScale 1→0 while open, back to 1 on close |
| Equipment wheel selection applies | PASS — forcing `hoveredCategory=Sidearm` then closing with `applySelection:true` correctly set `ActiveSlot=Sidearm` |
| Empty slots handled safely | PASS — `SetActive(LongGunB)` on an unfilled slot returns false, no exception |

### INJURY — all PASS, but see **Concern #1** below
| Test | Result |
|---|---|
| Arm injury measurably affects sway | PASS — `CurrentSwayDegrees` 1.2 → 2.1 after applying RightArm injury (severity 5) |
| Leg injury measurably affects movement | PASS — `FirstPersonController.SpeedMultiplier` 1.0 → 0.6 after RightLeg injury (severity 5) |
| Bleeding ticks periodically, not every frame | PASS — health dropped in ~3hp steps roughly every 2s (matches `BleedingEffect.TickIntervalSeconds=2f`), not a continuous per-frame drain |
| Bandage stops bleeding | PASS — `StatusEffectController.HasEffect("Bleeding")` true → false after `PlayerInventory.UseSelectedItem()` on a Bandage item and waiting out the 2s apply delay |
| Status effects don't damage every frame (poison) | PASS in isolation — poison ticks in ~3-damage steps roughly every 3s (matches `PoisonEffect.TickIntervalSeconds=3f`), not continuous |

### MELEE — all PASS
| Test | Result |
|---|---|
| Costs stamina | PASS — `StaminaSystem.CurrentStamina` 100 → 92 (cost 8) per attack |
| Correct range | PASS (after correcting a testing artifact, see note) — target at 3.5m (outside knife's 1.8m range + sphere-cast padding): 0 damage; target at 1.2m (in range): damage applied |
| Does not hit through walls | PASS — with `MeleeTestWall` enabled between player and `MeleeTestTarget`, `SphereCast` hits the wall first (no `IDamageable`), `OnMeleeHit` never fires, target health unchanged |
| Respects attack cooldown | PASS — two `TryAttack()` calls at the same `Time.time`: first succeeds, second is blocked by `nextFireTime`, target health identical after both |

*Testing note on range:* an early range-boundary test produced a false
positive (damage registered against a target moved to 3.5m). Root cause was
a test-methodology artifact, not a game bug: moving `Transform.position` via
script does not immediately update Unity's physics broadphase unless
`Physics.SyncTransforms()` is called or a physics step runs first, so the very
next `Physics.SphereCast` in the same frame can query a stale collider
position. Once `Physics.SyncTransforms()` was added before each
re-position-then-attack step, results were clean and consistent, and an
isolated manual `Physics.SphereCast` call confirmed the same (correct) miss.
No code change needed — flagging only so this doesn't get mis-filed as a
melee-range bug in the future.

### COMBAT — all PASS
| Test | Result |
|---|---|
| Wolf can die | PASS — repeated `WolfAI.TakeDamage(25)` calls (3 hits) brought health to 0, `IsDead` → true |
| Dead wolf stops attacking | PASS, independently re-confirmed — `WolfAI.enabled` → false on death (matches orchestrator's earlier A1 finding) |
| Team filtering (fire at PlayerTeam target) | PASS — pistol fired successfully (`TryFire()` true, ammo consumed) at `TestFriendly` (Faction.PlayerTeam), but `HealthSystem.CurrentHealth` unchanged and `OnWeaponHit` never invoked (blocked before the event fires in `ApplyHit`) |

---

## Regressions found: NONE in the systems under test

No firearms/equipment/injury/melee/combat regression was found. The
orchestrator's `QueryTriggerInteraction.Ignore` fix and Layer Collision Matrix
change are **confirmed safe** by this pass: every raycast/spherecast-based
test above (firearm hits, melee hits, wall-blocking, team filtering) behaved
exactly as the code intends, with no evidence of rays/casts snagging on
trigger volumes or layer-disabled pairs causing missed or phantom hits.
Console logs after the full pass showed zero errors attributable to
gameplay/combat code — only my own tool-usage errors (a bad `find_assets`
call, one `GC.Collect()`-induced pipeline timeout) and pre-existing
known-benign warnings (Unity Cloud "Account API" warning).

## Concern #1 (real finding, not fixed — outside "trivial one-line" scope)

**`RegionalInjurySystem.HandleDamaged` subscribes to `HealthSystem.OnDamaged`
for ALL damage sources, including damage caused by its own status effects
(`BleedingEffect`/`PoisonEffect` ticks).** This creates a feedback loop: a
bleed/poison tick calls `HealthSystem.TakeDamage`, which fires `OnDamaged`,
which `RegionalInjurySystem.HandleDamaged` catches and rolls a fresh
weighted-random region injury *and* has a chance (35% torso / 15% other) to
apply a *brand new* bleed on top of the existing one. Each new bleed tick
then repeats the cycle. I hit this by accident during isolated testing:
applying a single `ApplyBleeding(2)` + `ApplyPoison(3)` via the debug test
hooks (modest starting severities, nothing close to lethal on their own)
snowballed the player from 100 HP to 0 HP over roughly 90 seconds of
real-time ticking, entirely from status-effect self-damage re-triggering more
injury/bleeding. Regional severities in `RegionalInjurySystem.severities`
have no natural decay outside of `ReduceAllInjurySeverity` (rest/medicine),
so once several regions accumulate severity this way, sway/speed/stamina
penalties escalate too. This is a genuine architecture issue in
`RegionalInjurySystem.cs`, not a scene-config fix, so it hasn't been touched
here — flagging for a follow-up: `HandleDamaged` likely needs to ignore
damage that originated from the injury/status-effect system itself (e.g. tag
self-inflicted `TakeDamage` calls, or have `HealthSystem.OnDamaged` carry a
"damage source category" so status-effect ticks don't re-roll new injuries).

## Concern #1 — FIXED post-QA (orchestrator, same session)

Confirmed and fixed immediately after this report was written. Root cause
verified precisely: `BleedingEffect.Tick`/`PoisonEffect.Tick` (and, it turns
out, Sprint 05's `ColdExposureSystem`/`HungerSystem`/`ThirstSystem` critical-
damage ticks too) all call `HealthSystem.TakeDamage(amount)` with **no**
`source` argument (defaults to `null`), while every real external damage
source in the codebase (`WolfAI`, `MeleeController`, `WeaponController`,
`RockfallPlayerDamage`) always passes its own `gameObject` as `source`. Fix:
`RegionalInjurySystem.HandleDamaged` now returns immediately when
`source == null`, one guard clause, `Assets/_TRLM/Scripts/Combat/
RegionalInjurySystem.cs`. Re-tested live with the exact reproduction (Force
Bleed 2 + Force Poison 3, same as this report's original repro): health now
decreases in a decelerating, tapering curve (100 → 81.4 → 67.9 → 61.15 across
successive ~10s samples, deltas shrinking: -18.6, -13.5, -6.75) and
`RegionalInjurySystem.HasAnyInjury()` stayed `false` throughout — confirms no
new injuries are being spawned by the status ticks anymore. No death spiral.
Console clean after the fix, no new errors introduced.

## Remaining concerns

- Performance numbers this pass are lower than S06's, but given S06's own
  well-documented warmup-noise finding, treat this as "no regression
  detected," not a confirmed further improvement — a clean Development Build
  profiling pass is still the only way to get a trustworthy absolute number.
- `92_Test_Combat.unity` has no baked NavMesh, so `WolfAI`/`NavMeshAgent`
  threw `"Stop" can only be called on an active agent that has been placed on
  a NavMesh` and `"Failed to create agent because there is no valid NavMesh"`
  during the wolf-death test. This is a test-scene environment limitation
  (no NavMesh geometry to bake against), not a Sprint 07 regression — WolfAI
  itself wasn't touched this sprint and the death/attack-stop logic tested
  fine despite the warning. Worth baking a NavMesh into the test scene at
  some point so wolf state-machine transitions (chase/attack) can be
  exercised there too, not just death.
- The GC/pooling checks are Editor-harness measurements, not a Memory
  Profiler capture — recommend an actual Memory Profiler window pass before
  fully closing out Section 40 if a lab-grade number is needed.
