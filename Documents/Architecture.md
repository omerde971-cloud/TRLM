# TRLM Architecture

Living document. Updated as new systems are added. Last major update:
**Foundation Sprint 01** (2026-08-22).

---

## System Overview

TRLM's gameplay foundation is built from small, focused, single-responsibility
components that communicate through public events/properties rather than
tight coupling. No singletons, no god classes.

```
PlayerInputHandler  ──events/properties──▶  FirstPersonController
      │                                            │
      │                                     (asks) StaminaSystem
      │
      ├──event: InteractPressed──▶  InteractionOrigin ──▶ IInteractable
      │
      └──events: Jump/Fire/Aim/etc.──▶ (future systems subscribe here)

HealthSystem  (implements IDamageable)  — standalone, no dependencies
StaminaSystem — standalone, only FirstPersonController reads it
DebugHUD / InteractionPromptUI — read-only observers of the above, OnGUI-based
```

Nothing above depends on level/map content. Everything is testable or
inspectable without opening a specific scene.

---

## Namespaces

| Namespace | Folder | Contents |
|---|---|---|
| `TRLM.Core` | `Scripts/Core/` | Cross-cutting interfaces (`IDamageable`) |
| `TRLM.Player` | `Scripts/Player/` | `PlayerInputHandler`, `FirstPersonController`, `PlayerCamera` |
| `TRLM.Survival` | `Scripts/Survival/` | `HealthSystem`, `StaminaSystem` |
| `TRLM.Interaction` | `Scripts/Interaction/` | `IInteractable`, `InteractionOrigin`, `TestDoor`, `TestPickup`, `TestButton` |
| `TRLM.UI` | `Scripts/UI/` | `DebugHUD`, `InteractionPromptUI` |
| `TRLM.Tests` | `Tests/EditMode/` | `HealthSystemTests`, `StaminaSystemTests` |
| `TRLM.AI.Wildlife` | `Scripts/AI/Wildlife/` | `WildlifeSpeciesProfile` (SO), `WildlifeSpawnZone`, `WildlifeSpawner`, `WildlifeSpawnManager`, `WildlifeDespawnWatcher` |
| `TRLM.AI.Wolf` | `Scripts/AI/Wolf/` | `WolfAI` (state machine, implements `IDamageable`), `WolfPerception` |
| `TRLM.AI.Perception` | `Scripts/AI/Perception/` | `NoiseEvents` (static bus), `PlayerNoiseEmitter` |

Assembly definitions: `TRLM.Runtime` (all `Scripts/` code) and
`TRLM.Tests.EditMode` (references `TRLM.Runtime` + NUnit/TestRunner).
`TRLM.Runtime` was required so the test assembly could reference our code —
Unity compiles the default `Assembly-CSharp` *after* asmdef-based assemblies,
so a test asmdef cannot reference bare `Assembly-CSharp`.

---

## Key Scripts

### `PlayerInputHandler` (`TRLM.Player`)
Single source of truth for raw device input. Builds all `InputAction`s in
code (no `.inputactions` asset — see "Input System Structure" below).
Exposes `MoveInput`/`LookInput`/`SprintHeld`/`CrouchHeld` as polled
properties, and `JumpPressed`/`InteractPressed`/`FlashlightPressed`/
`InventoryPressed`/`FirePressed`/`AimPressed`/`AimReleased`/`ReloadPressed`/
`DropPressed`/`PausePressed` as events. **Nothing else in the codebase reads
`Keyboard.current` or `Mouse.current` directly** — every other script goes
through this component.

### `FirstPersonController` (`TRLM.Player`)
`CharacterController`-based movement tuned for slow atmospheric exploration,
not arcade FPS. Smoothed acceleration/deceleration (`accelerationTime`/
`decelerationTime`), separate walk/sprint/crouch speeds, gravity with a
grounded "stick" force, basic steep-slope sliding, and jump gated by
`StaminaSystem.ConsumeJump()`. Reads input only via `PlayerInputHandler`.

### `PlayerCamera` (`TRLM.Player`)
Mouse look with clamped pitch. Yaw is applied to a `bodyRoot` Transform
(the player capsule) so movement direction stays correct; pitch is applied
to the camera's own transform only. This split is deliberate: a future
visible player body can share the same yaw rotation, and a future
third-person cinematic camera can drive a *different* transform without
touching this component's logic. Not coupled to Health/Stamina.

### `HealthSystem` / `StaminaSystem` (`TRLM.Survival`)
Both use **lazy initialization** (`EnsureInitialized()`) instead of `Awake()`
for their starting value, specifically so they behave identically whether
queried from Play Mode, Edit Mode tests, or immediately after
`AddComponent()` — `Awake()` timing is not guaranteed in all of those
contexts, and relying on it caused real test failures during this sprint
(see `Documents/DevelopmentLog.md`).

`HealthSystem` implements `TRLM.Core.IDamageable` (`TakeDamage`, `Heal`,
`IsDead`) and exposes `OnHealthChanged`, `OnDamaged`, `OnDeath` events.
`StaminaSystem` exposes `ConsumeSprint(deltaTime)`, `ConsumeJump()`,
`Tick(deltaTime)` (regen — called from `Update()`, also called directly by
tests), and `OnStaminaChanged`.

### `IInteractable` / `InteractionOrigin` (`TRLM.Interaction`)
`InteractionOrigin` raycasts forward from a `Camera` every frame
(`interactionRange`, default 2.5m), caches the current `IInteractable` hit
(via `GetComponentInParent`), and calls `Interact()` on
`PlayerInputHandler.InteractPressed`. `TestDoor`/`TestPickup`/`TestButton`
are throwaway proof-of-architecture objects, not real gameplay content.

### `DebugHUD` / `InteractionPromptUI` (`TRLM.UI`)
`OnGUI`-based (no Canvas/UGUI setup needed for a dev-only overlay).
`DebugHUD` toggles with **F3**. Both are read-only — they never mutate
Health/Stamina/Controller state, only display it. Safe to delete before a
real build.

---

## Prefab Structure

`Assets/_TRLM/Prefabs/Player/PF_Player.prefab`:

```
PF_Player                          (CharacterController, FirstPersonController, PlayerInputHandler)
├── CameraRoot                     (positioned at eye height, 1.6m)
│   └── MainCamera                 (Camera, AudioListener, PlayerCamera)
├── InteractionOrigin              (InteractionOrigin — raycasts from MainCamera)
└── Systems                        (StaminaSystem, HealthSystem, DebugHUD, InteractionPromptUI)
```

`FirstPersonController` and `PlayerCamera` reference `PlayerInputHandler` and
each other via serialized fields wired at prefab-authoring time (not
`GetComponent` lookups at runtime) — keeps dependencies explicit and
inspectable.

Other production prefabs from the Production Prep pass remain under
`Assets/_TRLM/Prefabs/{Vehicles,Buildings,Animals,Characters}/` — untouched
by this sprint.

---

## Input System Structure

**No `.inputactions` asset exists.** Programmatic `AssetDatabase`-level
generation of one proved unreliable in this environment (the
`InputActionAsset` editor API and hand-built JSON both hit issues), so
`PlayerInputHandler` constructs each `InputAction` directly in `Awake()`
using `new InputAction(name, type, binding)`. This is a fully supported,
documented Input System workflow (the "code-only" approach) and keeps every
binding in one file.

| Action | Binding | Type |
|---|---|---|
| Move | WASD (2DVector composite) | Value (Vector2) |
| Look | Mouse delta | Value (Vector2) |
| Sprint | Left Shift | Button |
| Crouch | Left Ctrl | Button |
| Jump | Space | Button |
| Interact | E | Button |
| Flashlight | F | Button |
| Inventory | I | Button |
| EquipmentWheel | Tab (held) | Button |
| Fire | Left Mouse Button | Button |
| Aim | Right Mouse Button | Button |
| Reload | R | Button |
| Drop | G | Button |
| Pause | Escape | Button |

**Known limitation:** headless/automated simulation of these bindings (via
`InputSystem.QueueStateEvent`) proved unreliable in this environment — the
underlying device control read the correct value but the composite action
never reported it. This blocked fully-automated Play Mode verification of
movement/sprint/crouch in this sprint (see `DevelopmentLog.md`). It did
**not** block verifying the raycast+`IInteractable` pipeline, which was
proven directly by invoking `InteractionOrigin`'s target-detection and
`Interact()` call in Play Mode without needing a simulated keypress.
**Real keyboard/mouse testing by Ömer in the Editor is the next step to
close this gap.**

Where future systems should connect: subscribe to the relevant
`PlayerInputHandler` event/property. Do not add new direct
`Keyboard.current`/`Mouse.current` reads anywhere else in the codebase.

---

## Combat, Equipment & Injury (Sprint 07)

Full architecture in `Documents/CombatSystem.md` and `Documents/InjurySystem.md` —
index-level pointer only. New namespaces: `TRLM.Equipment` (weapon data/runtime,
equipment slots, equipment wheel), `TRLM.Combat` (melee, regional injury,
bleeding/poison status effects), `TRLM.Core.Faction`/`FactionMember` (friendly-fire
filtering). Reuses `IDamageable`/`HealthSystem` for all damage — no parallel health
framework. Reuses `NoiseEvents` for gunshot noise — no second perception bus.
Extends (does not replace) Sprint 05's `IStatusEffect`/`StatusEffectController` for
Bleeding/Poison/Trauma. Zero weapon 3D assets exist in the project — all weapons use
primitive placeholder geometry, documented honestly, not claimed as final art.

## Gameplay Integration (Sprint 05)

Full architecture documented separately in `Documents/GameplayIntegration.md` —
this section is just the index-level pointer. New namespaces this sprint:
`TRLM.Inventory`, `TRLM.Companions`, `TRLM.Boat`, `TRLM.Progression`,
`TRLM.Equipment`. `TRLM.Survival` and `TRLM.World` were extended (not
rewritten) with Hunger/Thirst/Wetness/Cold/StatusEffect/TeamProvisions and
Fire/DayNight/SafeHouse/Sleep/RockfallDamage respectively. Key point for
future systems: `IWorldTimeSource` now has a real implementation
(`DayNightSystem`) instead of the debug placeholder — wildlife and cold
both already consume it through the interface, so nothing needs to change
again when weather/seasons are added later, same pattern as before.

## Wildlife System (Sprint 03)

Full architecture, wolf state machine, pack behavior, and perception design documented
separately in `Documents/WildlifeSystem.md` — this section is just the index-level pointer.
Key point for future systems: animal AI reads world state only through interfaces
(`TRLM.World.IWorldTimeSource` for day/night, `TRLM.AI.Perception.NoiseEvents` for sound),
never through concrete systems, so a real day/night or weapon system can plug in without
touching `WolfAI`.

## Where Future Systems Should Connect

- **Inventory**: subscribe to `PlayerInputHandler.InventoryPressed`.
  Item pickups should implement `IInteractable`, following `TestPickup`'s
  pattern.
- **Combat**: anything damageable implements `TRLM.Core.IDamageable`
  (already used by `HealthSystem`). Weapons subscribe to `FirePressed`/
  `AimPressed`/`AimReleased`/`ReloadPressed`.
- **Equipment wheel**: `PlayerInputHandler.EquipmentWheelHeld` is already
  exposed as a polled bool (true while Tab is held).
- **Status effects** (bleeding/poisoning/hypothermia): call
  `HealthSystem.TakeDamage()` over time; do not add new health fields
  elsewhere.
- **Animal/companion AI**: attack behaviors call
  `IDamageable.TakeDamage(amount, source)` on the player's `HealthSystem`.
- **Third-person cinematics**: swap what drives `PlayerCamera`'s output
  transform, or add a parallel camera rig — `PlayerCamera` was deliberately
  kept independent of Health/Stamina so this doesn't ripple elsewhere.
- **Visible player body/hands**: attach a skinned mesh under `PF_Player`
  driven by the same yaw as `CameraRoot`; `PlayerCamera.bodyRoot` already
  exists as the yaw-authority transform to parent against.

---

## Non-Goals (Sprint 01)

No inventory, loot, weapons, shooting, melee, wolf/companion AI, rain,
day/night, hunger/thirst/temperature/sanity, save system, equipment wheel
*logic* (only the input binding), rowing system, production terrain/island,
or cinematics. These are deliberately out of scope — see
`Documents/DevelopmentLog.md` for the full deferred list.
