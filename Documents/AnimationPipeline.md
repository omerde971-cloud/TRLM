# TRLM Animation Pipeline

**Sprint:** Character & Creature Material + Animation Sprint 04 (2026-08-22)

---

## A. Blender availability — BLOCKED (confirmed hard blocker, not worked around)

Checked directly on this machine: `C:\Program Files\Blender Foundation\Blender 4.5\`
contains **only the versioned data subfolder** (`4.5\python`, `4.5\scripts`) —
**no `blender.exe` exists anywhere on the system** (also checked
`%LOCALAPPDATA%\Programs` for an alternate install location — none found).
This is a broken/incomplete install, not a missing-PATH issue.

**Decision made and followed:** do not attempt to install Blender (an
unauthorized system-level action, outside this sprint's remit) and do not
fake having used it. Every requirement in the sprint brief that depended on
a Blender step (custom rigging, mesh-based root-motion authoring, retargeting
non-Humanoid skeletons) is reported below as blocked, honestly, rather than
silently skipped or faked.

**What this blocks concretely:**
- Rigging the Wolf mesh (confirmed zero bones/rig) from scratch.
- Any rigging/animation work for Bear/Boar/Snake/Mountain Goat (moot anyway —
  see `CreatureMaterialAudit.md`, these have `NO_ASSET`).
- Custom mesh-space animation edits beyond what Unity's Humanoid muscle-curve
  system and Transform-curve system can do without leaving the Editor.

**What this does NOT block** (and was completed instead): the human
characters use Reallusion's CC3+ skeleton, which auto-maps cleanly onto
Unity's built-in Humanoid Avatar system (confirmed — see Section B). This
means human animation authoring did **not** need Blender at all this sprint,
since Unity's own muscle-curve `AnimationClip` API is a legitimate,
Blender-free way to author Humanoid animation.

---

## B. Human rig setup (Elias, Mira, Jonah, Lena, Noah)

All 3 underlying base meshes (`CC3_Base_Plus`, `Neutral_F`, `Neutral_M`) had
`ModelImporter.animationType` changed from `Generic` to `Human`. Unity's
auto-bone-mapper succeeded on **all 3 with zero manual mapping and zero
errors** — 55 bones each, `avatar.isValid == true`, `avatar.isHuman == true`.
This confirms the Reallusion CC3 skeleton naming convention is directly
compatible with Unity's Humanoid/Mixamo-style retargeting system, which is
what makes Blender-free authoring possible for humans specifically.

Each of the 5 character prefabs (`PF_Elias`, `PF_Mira`, `PF_Jonah`,
`PF_Lena`, `PF_Noah`) has its own `Animator` component with:
- `avatar` = the auto-generated Humanoid Avatar from its source FBX
- `runtimeAnimatorController` = `Assets/_TRLM/Animations/Human/AC_Human_Base.controller`
  (shared across all 5 — they use the same skeleton topology, so one
  controller is correct, not a shortcut)
- `applyRootMotion = false` (movement stays driven by gameplay code, not
  animation, matching `FirstPersonController`'s existing architecture)

---

## C. Human animation authored this sprint

### `Anim_Human_Idle_Breathing.anim`
`Assets/_TRLM/Animations/Human/` — the first real (non-placeholder) human
animation clip. Authored via Unity's Humanoid **muscle-curve** API
(`AnimationClip.SetCurve("", typeof(Animator), "<Muscle Name>", curve)`),
which is rig-agnostic and needs no per-bone Transform keyframing — a
legitimate Blender-free authoring path for Humanoid rigs.

- 3.2s seamless loop, sine-wave curves, subtle amplitudes (0.02–0.05) on:
  Spine Front-Back, Chest Front-Back, Left/Right Shoulder Down-Up,
  Head Up-Down, RootT.y (a faint vertical breathing lift).
- **Verified two ways:** (1) live in Play Mode, Spine bone rotation sampled
  twice at different times and confirmed to change (14.12° → 12.00°),
  proving the curve is actively driving the rig; (2) a side-by-side pose
  comparison — the Animator was disabled on a test instance and the pose was
  re-screenshotted; **the pose was identical with the Animator off**,
  confirming the initially-surprising "hunched" look in the first screenshot
  is this rig's own natural bind/reference pose, not an artifact of the new
  animation curves. No amplitude correction was needed as a result — the
  breathing clip itself is working as authored.

### `AC_Human_Base.controller`
`Assets/_TRLM/Animations/Human/` — one shared `AnimatorController` for all 5
human characters. Default state `Idle` uses `Anim_Human_Idle_Breathing`
(working, verified). **19 additional states exist as named placeholders**
(no motion assigned yet) representing the full set the sprint brief called
for, so the architecture is fully laid out and ready for future clips to be
dropped in without restructuring the controller:

`Walk, Run, CrouchIdle, CrouchWalk, JumpStart, JumpLoop, JumpLand,
InjuredWalk, LookVariation, ConversationIdle, FearAlertIdle, CarryingIdle,
CarryingWalk, Digging, Rowing, FlashlightHold, BookHold, PageTurn,
ClimbMantle`

**Honest scope note:** only `Idle` has a working clip. Authoring the other
19 (correct locomotion timing, foot-planting, transition blend trees) is
real, substantial animation-authoring work — a full walk/run cycle alone
typically needs several hours of hand-keying or a mocap/retarget source.
This sprint's time was intentionally spent proving the full pipeline works
end-to-end (rig → avatar → controller → working clip → verified in Play
Mode) rather than partially hand-keying many clips badly. This matches the
brief's own priority ("do not accept fake placeholder... as done" — an empty
named state is honestly incomplete; a bad rushed walk cycle would be a worse
kind of fake).

---

## D. Creature animation

### Wolf — substitute only, clearly labeled, real blocker documented
`Assets/ThirdParty/Animals/Wolf_CC0` has **no rig, no bones** (re-confirmed
this sprint) — true skeletal locomotion animation is impossible without
rigging it first, which needs Blender (blocked, see Section A).

**What was built instead, as an honest interim measure:**
1. `PF_Wolf.prefab` restructured: mesh moved from the root object onto a new
   child `Visual` GameObject. The root keeps `NavMeshAgent`/`WolfAI`/
   collider (unchanged behavior — AI/navigation logic untouched); `Visual`
   exists purely so a substitute animation can move independently of the
   NavMeshAgent-driven root transform without the two fighting over the same
   Transform.
2. `Anim_Wolf_IdleSway_SUBSTITUTE.anim` (`Assets/_TRLM/Animations/Wolf/`) —
   a 2-second looping root-transform animation (Transform curves, not
   muscle curves — there's no Humanoid rig to use those on) on the `Visual`
   child: a small vertical bob (`localPosition.y`, 0–0.035m) and a gentle
   side sway (`localEulerAngles.z`, 0–2.5°). This is a static-object idle
   "breathing"/weight-shift substitute — **it is explicitly not a walk,
   run, or attack animation**, since none of those are achievable without
   real bones.
3. `AC_Wolf_Substitute.controller` — a single-state Animator Controller
   playing the above clip on loop, attached to the root's `Animator`.
4. Verified live in Play Mode: spawned a wolf instance, confirmed the
   `Visual` mesh renders correctly (bounds sane) and the Animator is
   actively in the `IdleSway` state during Play Mode.

**Real fix still needed (unchanged from Sprint 03's documented gap):**
rigging this mesh (Blender + a retarget/Mixamo-style pipeline, or sourcing a
pre-rigged wolf) is the only way to get genuine walk/run/attack animation.
This substitute makes the wolf visually alive at a standstill; it still
slides across the ground when `NavMeshAgent` moves it, same as before.

### Bear / Boar / Snake / Mountain Goat
No animation work possible or attempted — `NO_ASSET` (see
`CreatureMaterialAudit.md`). Each species needs a real rigged model sourced
before any animation pipeline step applies.

---

## E. Summary of what's genuinely usable today

| Item | Status |
|---|---|
| Human Humanoid rig/avatar (all 5 characters) | ✅ Working, verified |
| Human idle breathing animation | ✅ Working, verified |
| Human walk/run/etc. (19 states) | ⚠️ Placeholder states only, no clips |
| Wolf substitute idle sway | ✅ Working, verified, clearly labeled temporary |
| Wolf real locomotion (walk/run/attack) | ❌ Blocked — needs rigging (Blender unavailable) |
| Bear/Boar/Snake/Goat animation | ❌ Blocked — `NO_ASSET`, needs sourcing first |
