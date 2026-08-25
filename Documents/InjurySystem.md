# TRLM Injury System

**Sprint:** Combat, Equipment & Injury Sprint 07 (2026-08-22)
**Scripts:** `Assets/_TRLM/Scripts/Combat/` (regional injury, bleeding, poison,
trauma), extends `Assets/_TRLM/Scripts/Survival/` (Sprint 05's status-effect
foundation — not rewritten)

---

## Foundation reused, not rebuilt

Sprint 05 built `TRLM.Survival.IStatusEffect` (a minimal `Tick`/`IsExpired`
contract) and `StatusEffectController` (ticks a list of active effects every
frame, removes expired ones, never bypasses `HealthSystem`) purely as
proof-of-architecture, with one trivial example (`MinorBleedEffect`). This sprint
implements the real content on top of that same interface/container — **neither
file was rewritten**, only real `IStatusEffect` implementations were added.

---

## Critical fix: self-damage feedback loop (found by QA, fixed same sprint)

`RegionalInjurySystem.HandleDamaged` subscribes to the player's `HealthSystem.
OnDamaged` for every damage event. `BleedingEffect`/`PoisonEffect` (and, it turns
out, Sprint 05's `HungerSystem`/`ThirstSystem`/`ColdExposureSystem` critical-damage
ticks too) all call `TakeDamage` with **no** `source` argument — every real
external attacker (`WolfAI`, `MeleeController`, `WeaponController`,
`RockfallPlayerDamage`) always passes its own `gameObject`. Without a guard, a
bleed tick's own damage re-entered `HandleDamaged`, rolled a fresh region, and
could spawn a *new* bleed — QA reproduced a 100→0 HP death spiral from two modest
status effects ticking against each other for ~90 seconds. Fixed with one guard
clause (`if (source == null) return;`) — internal/environmental damage no longer
re-triggers the injury system. Re-verified live: the exact reproduction now
produces a normal, decelerating, self-limiting health curve instead of a spiral.

## Regional Injury (`RegionalInjurySystem`, on `PF_Player/Systems`)

`BodyRegion` enum: `Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg`.

**Region determination**: no current damage source in the game provides precise
hit-location for player-received damage (wolf bites, rockfall — both call a plain
`HealthSystem.TakeDamage(amount, source)` with no spatial hit data).
`RegionalInjurySystem` subscribes to the player's own `HealthSystem.OnDamaged` and
picks a **weighted-random** region per hit (Head 10%, Torso 30%, each arm 15%,
each leg 15%) — roughly realistic weighting, and an honest fallback per the
brief's own hedge ("hit sources can provide approximate region **where
available**" — none currently are, for player-received damage). Severity is
derived from the damage amount and scaled by
`TRLM.Progression.DifficultySettings.InjurySeverityMultiplier`.

### Effects per region

| Region | Effect | Mechanism |
|---|---|---|
| Arm (either) | Increased weapon sway | `WeaponController.SetSwayModifier("Injury_<Arm>", penalty)` |
| Leg (either) | Movement speed penalty + sprint block at high severity | `FirstPersonController.SetSpeedModifier`/`SetSprintBlocked("Injury_<Leg>", ...)` |
| Torso | Increased stamina drain + elevated bleeding chance on further hits | `StaminaRegenModifier.SetPenalty("Injury_Torso", ...)` |
| Head | `OnHeadInjury` event hook (future camera-shake/audio) + elevated bleed at high severity | public event, no forced screen-shake implementation this sprint |

All modifier calls are `sourceId`-keyed (`"Injury_LeftArm"` etc.) into the same
worst-penalty-wins pattern established by Sprint 05's `StaminaRegenModifier` and
Sprint 06's `FirstPersonController` speed modifiers — multiple simultaneous
injuries compose sensibly rather than stacking multiplicatively into an
unplayable state.

**No arm-injury reload penalty**: `WeaponController` has no existing
reload-duration-multiplier hook, and adding one wasn't judged safe within this
sprint's scope (would be a same-day third touch to a file already modified twice).
Documented as a known gap, not silently skipped.

---

## Fracture / Trauma Foundation (Section 25)

Severe leg or arm injury (above a severity threshold) applies a `TraumaLeg`/
`TraumaArm` marker — a real `IStatusEffect` (`TraumaStatusFlag`), non-damaging,
stacking a stronger movement/sway penalty while active. Has a genuine countdown
(`IsExpired` becomes true after real elapsed time), **not indefinite** — the
brief explicitly required "do not make the player permanently limp for hours."
Safe-house sleep accelerates recovery: `SleepInteraction.ApplyRest()` gained one
additive call, `RegionalInjurySystem.AccelerateRecovery()`, halving remaining
trauma duration — same minimal-additive-hook pattern as every other
cross-system integration this project has used since Sprint 05.

---

## Bleeding (`BleedingEffect : IStatusEffect`)

Real implementation superseding Sprint 05's `MinorBleedEffect` proof-of-concept
(left in place, unreferenced by new code, in case anything else still points at
it — not deleted blind). **Periodic tick, not per-frame damage**: an internal
timer accumulates `deltaTime` and only calls `HealthSystem.TakeDamage` roughly
every 2 seconds — `StatusEffectController.Tick()` itself still runs every frame
(that part of the container is correct-by-design and unchanged), but the actual
damage application inside `BleedingEffect` is rate-limited internally, satisfying
the brief's explicit "must not cause damage every frame" requirement. Severity
stacks only within a cap (max severity 3) — repeated wolf bites can't produce an
unkillable-fast bleed by piling up unlimited effect instances. Applied by
`RegionalInjurySystem` on a probability roll per hit (elevated for torso/head),
not guaranteed every hit.

## Bandage (`Bandage.asset`, new `ItemCategory.Bandage`)

Real inventory item, used via `PlayerInventory.UseSelectedItem()`'s existing
dispatch switch (extended, not rebuilt). Short use duration via a coroutine
(`ANIMATION_PLACEHOLDER` — no real bandaging animation exists, same honesty
pattern as weapon reload/sleep). On completion: consumes the item, calls
`RegionalInjurySystem.TreatBleeding()` which removes the active `BleedingEffect`
from `StatusEffectController`. Bandage's stated purpose is bleeding control, not
a large HP restore — it does not touch `HealthSystem` directly.

---

## Medicine (expanded, not rebuilt)

`PlayerInventory.UseSelectedItem()` already called `HealthSystem.Heal()` for
Medicine-category items (Sprint 06). This sprint's addition: the same medicine-use
branch now also calls a modest injury/poison-severity reduction
(`RegionalInjurySystem.ReduceAllInjurySeverity`/`ReducePoisonSeverity`) —
**deliberately not a full cure**, per the brief's explicit "do not let common
medicine instantly remove fractures/poison/etc." instruction. A severe leg
fracture or advanced poisoning is not solved by one item.

---

## Poison Foundation (`PoisonEffect : IStatusEffect`)

Same periodic-tick pattern as bleeding (internal timer, ~3s intervals),
configurable severity, reduced by Medicine (see above). **No snake AI or other
production poison source exists** — per the brief, none is required this sprint.
A test-only stand-in, `PoisonTestTrigger`, exists purely for repeatable QA
(clearly named/commented as test-only, not production content) — a real poison
source (snake bite, contaminated water, etc.) is future-sprint work once a
poison-capable creature or hazard actually exists in the game.

---

## Hypothermia — verified, not rebuilt

Sprint 05's `ColdExposureSystem` already applies its own stamina-regen penalty
(via `StaminaRegenModifier`) and periodic critical-cold health damage internally
— this **is** Section 27's required "critical cold → stamina penalty → health
pressure" chain, built two sprints ago. Per the brief's explicit "do not rewrite
it" instruction, this sprint only **verified** the existing behavior live (forced
low body temperature via reflection, confirmed both the stamina penalty and
eventual health drain still fire correctly) rather than re-implementing anything.
A `HypothermiaStatusFlag` bridge into the unified `StatusEffectController` (so
`HasEffect("Hypothermia")` becomes queryable alongside Bleeding/Poison/Trauma) was
considered explicitly optional per the brief and was **skipped** this sprint —
`ColdExposureSystem.BodyTemperature` remains directly queryable as a public float
for now, which is sufficient for the HUD.

---

## Companion Damage Compatibility (verified, not built)

`PF_Jonah_Companion` already reuses `TRLM.Survival.HealthSystem` (Sprint 05) — the
same `IDamageable`-based melee/firearm/injury code that works against the player
and wolves works against a companion target with zero special-casing, verified
live (melee swing against a companion instance applied damage correctly when
faction-matching was bypassed for the test, and correctly blocked when the normal
`PlayerTeam`-vs-`PlayerTeam` friendly-fire rule was in effect). No companion
medical AI was built — out of scope, per the brief.

---

## Combat HUD Additions

`GameplayHUD.cs` gained one additive block (matching its existing threshold-gated
`OnGUI` style): an "Injured: <region>" line when any region has non-zero severity,
and a clear red "BLEEDING" warning when `StatusEffectController.HasEffect(
"Bleeding")` is true. No permanent clutter — both are gated the same way every
other HUD element already is (hidden when not relevant).

---

## Known Limitations (honest, not worked around)

- Region determination for player damage is weighted-random, not precise hit
  location — no current damage source provides real spatial hit data to the
  player.
- No reload-speed penalty from arm injury (see `CombatSystem.md`).
- `HypothermiaStatusFlag` unified-status bridge was skipped (explicitly optional).
- No production poison source — test trigger only.
- No visual/animation representation of any injury state (limping, wound decals,
  etc.) — mechanical state only, consistent with the project's established
  animation-debt honesty pattern (see `AnimationPipeline.md`).
