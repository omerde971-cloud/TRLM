# TRLM Combat System

**Sprint:** Combat, Equipment & Injury Sprint 07 (2026-08-22)
**Scripts:** `Assets/_TRLM/Scripts/Equipment/` (firearms, equipment), `Assets/_TRLM/Scripts/Combat/` (melee)

---

## Honest asset reality (report per sprint instructions, not silently worked around)

**Zero weapon 3D assets exist anywhere in this project.** Confirmed at sprint start:
`Assets/ThirdParty/Weapons/` is an empty folder, `Assets_Source/` has nothing
weapon-related, `Documents/AssetRegistry.md` had zero weapon entries. This project
also carries a standing zero-budget constraint from the P0 sprint. Per this sprint's
own instruction ("do not delay the entire sprint trying to obtain six perfect
weapons... do not use ripped or unclear assets"), every weapon this sprint uses a
**simple primitive-geometry placeholder mesh** (`DEV_Placeholder_*` naming
convention, same precedent as Sprint 06's fire/grave placeholders), fully
mechanically functional. A future zero-cost asset-sourcing pass (same shape as the
P0 sprint did for characters/environment) is the correct path to real weapon
models — not this sprint.

---

## Equipment Architecture (`TRLM.Equipment`)

`PlayerEquipment` (on `PF_Player`) — 4 physical slots (`Sidearm`, `LongGunA`,
`LongGunB`, `Melee`), **separate from the 10-slot inventory**. One slot is
"active"/drawn at a time. `TryEquip(WeaponDefinition)` auto-picks the correct slot
from the definition's `WeaponCategory`. Mount point Transforms (`HipMount`,
`BackLeftMount`, `BackRightMount`, `MeleeMount`) exist as children of `PF_Player`
for future visual attachment — **`ANIMATION_PLACEHOLDER`**: positions are
approximate, not aligned to real character geometry (none exists — see
`AnimationPipeline.md`). Mechanical slot state is authoritative; visuals are
secondary, per explicit sprint instruction ("do not fake finished cinematic weapon
posing").

`WeaponDefinition` (ScriptableObject) — one data class covers both firearms and
melee (melee leaves magazine/reload/ammo fields unused rather than forking into a
second data type, since the fields degrade gracefully). Fields: category, required
ammo, magazine capacity, damage, range, fire rate, reload time, recoil profile,
pellet count + spread (shotgun), sway profile.

`WeaponRuntimeState` (plain C# class, not a `MonoBehaviour`) — per-slot runtime
data (current magazine, reload-in-progress, cooldown timer), owned by
`PlayerEquipment`, not a separate scene object.

`WeaponController` (on `PF_Player`) — single data-driven controller for both
equipped firearms (reads whichever `WeaponDefinition` is active rather than one
controller per weapon type, per the brief's explicit "do not create one enormous
WeaponManager... separate weapon data from weapon runtime state" instruction, while
still avoiding a second near-duplicate class). `MeleeController` (separate,
`TRLM.Combat`) drives the `Melee` slot using the same `WeaponDefinition`/
`WeaponRuntimeState` shapes.

**Input gating**: `PlayerInputHandler.FirePressed` (LMB) is shared by three
consumers this sprint — `WeaponController` (fire equipped firearm),
`MeleeController` (swing equipped melee), and `GameplayHUD` (use selected
inventory item while the inventory panel is open, Sprint 06). Each checks
`PlayerEquipment.ActiveSlot`/`GameplayHUD.InventoryOpen` before acting, so exactly
one consumer does anything on any given press — no raw new keybind added.

---

## Equipment Wheel (`EquipmentWheelUI`)

Opens while `PlayerInputHandler.EquipmentWheelHeld` (Tab, held) is true. **Pause
strategy**: sets `Time.timeScale = 0f` on open, `1f` on close. This achieves the
brief's "pause wildlife AI, companion AI, world time, physics gameplay progression"
requirement with **zero changes to `WolfAI.cs`, `CompanionAI.cs`, or
`DayNightSystem.cs`** — all three already key their per-frame logic off
`Time.deltaTime`/`Time.time`, so a global timescale freeze pauses them for free.
`OnGUI` and raw mouse-position reads are unaffected by `timeScale`, so wheel
selection still works while paused. Categories: Empty Hands, Sidearm, Long Gun A,
Long Gun B, Melee, Flashlight, Symbol Book (reserved slot, disabled — **Symbol Book
itself is not implemented this sprint**, per explicit instruction). Empty slots are
visibly disabled/unselectable. Mouse position for wheel selection is read directly
via `Mouse.current.position` — a documented, scoped exception to the "always go
through `PlayerInputHandler`" rule, same precedent as Sprint 05/06's
`CompanionCommandInput`/inventory-slot-cycling exceptions.

---

## Firearms

**Pistol** (`WPN_Pistol.asset`): semi-auto, 10-round magazine, 22 damage, 40m
range, 0.35s fire rate, 1.6s reload, moderate recoil.

**Shotgun** (`WPN_Shotgun.asset`) — the long gun chosen this sprint (per the
brief's stated preference "pump shotgun IF suitable asset/animation setup exists,"
which is moot here since no asset/animation exists for either option — shotgun was
picked as the more distinct/valuable second weapon type): 5-shell magazine, 8
pellets per shot at a 4.5° spread cone, 14 damage per pellet, 18m range, 0.9s fire
rate, 2.4s reload, strong recoil. Pellet count is capped (≤10) per the brief's
"do not run hundreds of rays" instruction — 8 pellets per shot, not per magazine.

**Ammo**: real `ItemDefinition` assets living in the normal 10-slot inventory
(`Ammo_9mm.asset`, `Ammo_12Gauge.asset`), a new `Ammo` `ItemCategory` value added.
Reload checks the weapon's exact required ammo type — reloading with the wrong or
no ammo type present fails gracefully (verified live), and reload correctly
computes a **partial** reload when reserve ammo is less than the magazine gap
(verified live: 5 reserve, 7 needed → loaded 5, reserve emptied, not an error).

**Hit detection**: hitscan (`Physics.Raycast`/pellet-cone rays), zero-alloc (no
`RaycastAll`, no per-shot array/list allocation). **Fixed during integration**:
neither the firearm nor melee raycast originally specified
`QueryTriggerInteraction.Ignore` — without it, shots would have been silently
absorbed by invisible trigger volumes (`SafeHouseArea`, `LandingZone`, pickup
colliders, etc.) standing between the player and a real target, since
`Physics.Raycast` hits trigger colliders by default in Unity. Both
`WeaponController.cs` and `MeleeController.cs` now pass
`QueryTriggerInteraction.Ignore` explicitly. Verified live post-fix: firing,
damage application, and gunshot noise all still work correctly.

**Damage**: reuses `IDamageable`/`HealthSystem` — no parallel health framework.
`TRLM.Progression.DifficultySettings.EnemyDamageMultiplier` is genuinely consumed
(not decorative) when scaling damage applied to non-`PlayerTeam` targets.

**Friendly fire** (`TRLM.Core.Faction`/`FactionMember`): `PlayerTeam, Wildlife,
HumanHostile, Environment` enum. `FactionMember` present on `PF_Player`
(PlayerTeam), `PF_Wolf.prefab` (Wildlife), `PF_Jonah_Companion.prefab`
(PlayerTeam). Player→companion damage is blocked by default (both `PlayerTeam`) —
verified live: a confirmed weapon fire against a `PlayerTeam` target consumed
ammo/magazine but applied zero damage.

**Gunshot noise**: one call to the existing `TRLM.AI.Perception.NoiseEvents.Raise`
per shot — no second noise system built. Loudness is large relative to
footstep/sprint noise (pistol: 65, vs. sprint's 18m from Sprint 03) so gunfire is
heard far across the map. Wolves already subscribe to this bus (Sprint 03) — no
`WolfAI`/`WolfPerception` changes needed for gunfire to alert wildlife.

**Recoil**: `PlayerCamera` gained one additive public method,
`AddRecoilKick(pitchDegrees, yawDegrees)`, nudging internal pitch/yaw offsets that
decay smoothly — no other change to that file's existing look logic.

**Sway**: `WeaponSway`/`WeaponController` expose a `SetSwayModifier(sourceId,
multiplier)`-style API, worst-penalty-wins (same pattern as Sprint 05's
`StaminaRegenModifier` and Sprint 06's `FirstPersonController` speed modifiers) —
the regional injury system (see `InjurySystem.md`) plugs into this for arm-injury
handling penalties.

**Sound/event hooks** (no audio assets required): `OnFire`, `OnDryFire`,
`OnReloadStart`, `OnReloadComplete`, `OnWeaponHit`, `OnImpact` all exist and fire
at the correct moments — verified live via subscribed counters. A future audio
pass wires real sounds to these without touching firing logic.

---

## Wolf Combat — the good news

**`WolfAI.cs` needed zero code changes for wolf combat to work.** It already
implements `IDamageable` directly and, on taking lethal damage, already sets
`IsDead = true`, stops the `NavMeshAgent`, removes itself from pack-attacker
tracking, and disables its own `Update()` loop entirely (`enabled = false`) — built
in Sprint 03 for rockfall damage, and it turns out to be exactly correct for
weapon damage too. Verified live this sprint with **zero file changes**: repeated
`TakeDamage` calls via the pistol → `IsDead = true`, AI fully halted. Per the
brief's honesty rule: this is a **gameplay-state PASS**; visual hit/death reaction
is **not claimed** — the wolf still has no rig (see `AnimationPipeline.md`), so
there is no death animation, only correct mechanical death.

---

## Melee (`TRLM.Combat.MeleeController`)

One weapon: **Knife** (`WPN_Knife.asset`, reuses `WeaponDefinition` with category
`Melee` — damage 18, range 1.8m, 0.6s cooldown). LMB light attack only (heavy
attack explicitly skipped — "only if useful", judged not worth the added
complexity for a single-weapon minimum scope). `Physics.SphereCast` for the swing
(also fixed with `QueryTriggerInteraction.Ignore`) — reports the first collider
along the cast, so a wall between the player and a target fully blocks the hit,
verified live with a test wall. Costs stamina via a new
`StaminaSystem.ConsumeFlat(float)` (one additive method, same minimal-change
precedent as every other sprint's stamina hooks) — fully exhausted stamina blocks
the attack outright (no heavy-attack mode exists to degrade to instead). Respects
cooldown — rapid LMB spam does not multi-hit. Same friendly-fire check as firearms
(verified live against a companion instance: zero damage despite the swing
registering).

---

## Layer Collision Matrix (Section 42, deferred from Sprint 06, closed out this sprint)

New layers: `Player`, `Wildlife`, `Companion`, `Loot`, `TriggerZone`, `Rockfall`.
Assigned: `PF_Player` (+ prefab asset) → `Player`; `PF_Wolf.prefab` (+ all runtime
instances) → `Wildlife`; `PF_Jonah_Companion.prefab` (+ instances) → `Companion`;
all `PickupItem`/`WeaponPickup` instances → `Loot`; `LandingZone_Beach`,
`BurialZone_01`, `RegionTrigger_CoastalForest`, `RegionTrigger_AbandonedHouse`,
`PoisonTestVolume` → `TriggerZone`. **Rockfall pooled rock instances were left on
`Default`** (they're created at runtime by `RockfallZone.cs`, which is off-limits
to edit this sprint — reassigning their layer would need a code change; low value
given rockfall already worked correctly without layer separation).

Collision matrix disables — **conservative, deliberately narrow**: `Loot×Loot`,
`Loot×Wildlife`, `Loot×Companion`, `TriggerZone×TriggerZone`,
`TriggerZone×Wildlife`, `TriggerZone×Companion`, `TriggerZone×Loot`.
**Deliberately left enabled** (do not disable these — they are load-bearing):
`Player×TriggerZone` (region/safehouse/landing/burial triggers all require the
player to physically enter them), `Player×Wildlife` (wolf combat),
`Player×Companion`. Verified live post-change: the full Sprint 06 end-to-end
objective chain (landing → coastal forest → safe house) still fires correctly, and
pistol pickup/fire/damage/noise all still work with the new layers/matrix in
place.

---

## Difficulty & Save Hooks (architecture only, no UI)

`TRLM.Progression.DifficultySettings` — static fields
`PlayerDamageMultiplier`/`EnemyDamageMultiplier`/`LootAmmoMultiplier`/
`InjurySeverityMultiplier`, all default `1f`. `EnemyDamageMultiplier` and
`InjurySeverityMultiplier` are genuinely consumed by weapon damage and injury
severity calculations respectively (not decorative fields). All new equipment/
injury state lives in plain serializable fields (ints, enums, `ScriptableObject`
references) — no delegates/coroutines as core state — keeping a future save system
implementable without an architecture change.

---

## Production World Integration

Per the brief's explicit design intent ("weapons are valuable, ammo is more
valuable, do not give the player 100 rounds, do not flood loot pools"): **one**
pistol placed as a hand-authored, one-time discovery near the settlement safe
house (not a random loot-table roll — weapons are rare, authored finds, not
common loot). **4 rounds of 9mm** (2 hand-placed pickups, 2 rounds each) placed
alongside it — guaranteed-but-scarce, consistent with Sprint 05's own "critical
progression must never depend entirely on random chance" principle while staying
deliberately small. The shotgun and 12-gauge ammo are **not** placed in the
production island this sprint (reserved as a later "optional exploration reward",
per the brief) — both remain available in the combat test scene for QA purposes
only.

---

## Combat Test Scene

`Assets/_TRLM/Scenes/Tests/92_Test_Combat.unity` — `PF_Player` (fully equipped
with all Sprint 07 components), pistol/shotgun/knife pickups, 9mm/12-gauge/bandage
pickups, 3 static damage-target capsules (`HealthSystem` + `FactionMember` =
Wildlife), a damageable wolf instance, distance markers (10m/20m/50m), a
`MeleeTestWall` (inactive by default) for wall-block testing, and a
`CombatTestHarness` with `eval`-callable test methods
(`ForceInjury`/`ForceBleed`/`ForcePoison`) for repeatable injury-system testing
without needing real combat encounters.

---

## Known Limitations (honest, not worked around)

- All weapon visuals are primitive placeholders — no real 3D weapon assets exist.
- No reload-speed-multiplier hook exists on `WeaponController` for arm-injury to
  plug into (would need a small additive change that wasn't made this sprint) —
  arm injury currently only affects sway, not reload duration.
- No heavy melee attack — light-attack-only is the honestly-scoped minimum.
- No real weapon-kick visual animation (no character/weapon geometry to animate
  meaningfully) — recoil is camera-only.
- Rockfall pooled rocks remain on the `Default` physics layer, not `Rockfall`.
