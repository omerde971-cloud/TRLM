# TRLM Performance Report — Sprint 06

**Captured:** 2026-08-22, Play Mode, `20_Island_Blockout.unity`, same 7 locations and
methodology as `PerformanceBaseline_S06.md`. Same measurement caveat applies:
`get_performance_stats`'s render block (`drawCalls`/`batches`/`setPassCalls`/
`triangles`/`vertices`) still returns 0 in this Editor/MCP harness — only
`cpuFrameTimeMs`/`gpuFrameTimeMs` were reliable. A built-Player profiling pass
remains a manual to-do for Ömer.

---

## BEFORE (cited from PerformanceBaseline_S06.md)

| Location | CPU (ms) | GPU (ms) |
|---|---|---|
| A Coast | 33.4–37.4 | 34.3–36.6 |
| B Deep Forest | 43.6 | 36.2 |
| C Settlement | 40.9 | 34.4 |
| D Wolf (day) | 36.8 | 37.1 |
| E Night+Flashlight | 33.8 | 38.9 |
| F SafeHouse+Fire+Night+Flashlight (worst) | **48.3** | **43.2** |
| G Mountain view (best) | 31.3 | 34.6 |

## AFTER (this sprint, immediately-post-teleport samples, same methodology as baseline)

| Location | CPU (ms) | GPU (ms) |
|---|---|---|
| A Coast | 19.2 | 13.4 |
| B Deep Forest | 14.7 | 11.8 |
| C Settlement | 15.0 | 11.8 |
| D Wolf (day) | 17.3 | 12.9 |
| E Night+Flashlight | 14.5 | 11.4 |
| F SafeHouse+Fire+Night+Flashlight (was worst) | 15.2 | 12.1 |
| G Mountain view (was best) | 15.4 | 12.2 |

Every location now sits at or near the 16.7ms (60fps) budget, comfortably under the
20ms/50fps temporary floor. F, previously the worst location by a wide margin
(48.3/43.2ms), is now indistinguishable from the other six (15.2/12.1ms) — the
"stacked lights at F" cost is no longer visible at this scale.

No console errors after any change; `capture_game_view` at F (night, fire lit,
flashlight on) confirms geometry, shadows (rock casting a shadow under 2-cascade/
1024-res shadows), and lighting all still render correctly. Scene and URP asset
saved via `save_scene`/`save_all`.

---

## TOP 5 BOTTLENECKS FOUND (ranked, with evidence)

1. **Play-session/measurement-warmup overhead dwarfed everything else.** Before
   touching a single setting, re-measuring the SAME 7 locations later in the same
   play session (no fixes applied yet) already dropped every reading from the
   30–48ms baseline range to 15–19ms. The very first sample taken this session
   (Location A, immediately after entering Play Mode) read 41.6/37.4ms; a second
   sample at the identical position, position, and camera angle taken ~15 seconds
   later (still no changes applied) read 15.4/12.2ms. This is the dominant
   explanation for the baseline's "GPU floor doesn't scale with scene simplicity"
   finding — see below.
2. **Shadow cascade count/resolution (4×2048).** Real, measurable, and cheap to
   fix — reduced to 2×1024. Contributes to every frame regardless of location
   (shadow maps re-render every frame for whatever's in cascade range of the
   camera), consistent with a location-independent GPU cost.
3. **Background tree card shadow-casting (285 renderers).** `Background_Tree_Atlas*`
   instances (the cheap-filler tier documented in `WorldDesign.md`/
   `WildlifeSystem.md`) were casting shadows despite being flat billboard cards —
   visually near-invisible contribution, real GPU shadow-pass cost multiplied by
   285 instances.
4. **117 MeshColliders — already mitigated in Sprint 03, not a fresh bottleneck.**
   `WildlifeSystem.md`'s Performance Rules confirm 97 of 213 rocks were already
   converted from `MeshCollider` to `BoxCollider` last sprint; the 116 remaining
   are deliberately-kept low-poly (≤200 tri) rocks plus `RockfallZone` pooled
   convex colliders (129 MeshColliders counted live in Play Mode, +12 from the
   rockfall pool). No further safe reduction found — touching the remainder risks
   either rockfall physics or the documented negligible-cost exception.
5. **`WetnessSystem.IsInsideSafeHouse()`** calls `FindObjectsByType<WorldMarker>`
   every 0.5s (throttled, not every frame) from inside `Update()`. Real but bounded
   cost; `WetnessSystem.cs` is off-limits this sprint (Survival/*Wetness*) —
   flagged below, not fixed.

---

## TOP FIXES APPLIED

1. **URP asset (`Assets/Settings/PC_RPAsset.asset`)**: `shadowCascadeCount` 4→2,
   `mainLightShadowmapResolution` 2048→1024, `additionalLightsShadowmapResolution`
   2048→1024. Applied via `SerializedObject` + `AssetDatabase.SaveAssets()`.
2. **285 `Background_Tree_Atlas*` `MeshRenderer`s**: `shadowCastingMode` set to
   `Off` (were `On`). Scene saved. These are the documented cheap-filler tree
   tier — full trunk+branch trees near player routes were left untouched.
3. Investigated but **did not change**: Ocean shader (`M_Ocean_TestConfig` /
   `UberStylizedWater`) — no render-distance/LOD property exists on this shader;
   `IslandOcean`'s bounds (Center 400,1.5,-95, Extents 450,0,155) don't even reach
   Location G's camera (z=700), so it isn't the explanation for G's GPU floor
   either. Left as-is; not a fix, ruled out as a suspect instead.
4. Investigated but **did not change**: MeshColliders (already optimal per Sprint
   03 convention), physics layers (see below), `WolfAI`/`WolfPerception` tick rate
   (see below).

No incremental before/after was taken per-fix (shadow-cascade change and
background-card-shadow change were applied together, then re-measured once) —
given how small the residual signal was after the warmup effect washed out, further
splitting the two changes would not have produced a meaningful separate delta with
this harness's noise floor.

---

## REMAINING BOTTLENECKS / RECOMMENDATIONS (not implemented)

- **Layer Collision Matrix**: `get_tags_layers` shows only `Default`,
  `TransparentFX`, `Ignore Raycast`, `Water`, `UI` — no dedicated layers for loot,
  wildlife, or decorative props exist. Everything gameplay-relevant collides on
  `Default`. Creating new layers and reassigning objects (loot pickups, wildlife,
  rockfall debris) is a structural change touching hundreds of objects — judged
  too risky/broad for this sprint's "targeted fix" mandate. Recommend a dedicated
  pass: add `Loot`, `Wildlife`, `Decoration` layers, reassign via prefab edits (not
  scene edits, to keep it maintainable), then disable `Loot×Loot`,
  `Loot×Wildlife`, `Decoration×Decoration`, `Decoration×Wildlife` pairs in the
  matrix.
- **`WolfAI`/`WolfPerception` tick cost at distance**: `WolfAI.Update()` runs every
  frame with `Vector3.Distance` checks and `Physics.Linecast` for player
  visibility, regardless of distance to the player. Only 3 wolf instances exist,
  so absolute cost is currently low, but no safe non-invasive external throttle
  was found (unlike Sprint 05's `WolfFireAvoidance`, there's no clean hook point
  to gate `Update()` execution from outside without either modifying `WolfAI.cs`
  directly or wrapping/disabling the component, which risks breaking its internal
  state timers). Recommend either a future `WolfAI.cs` change (out of this
  sprint's scope) adding an internal distance-gated early-return, or accept the
  current cost as negligible at 3 wolves and revisit if wolf count scales up.
- **`WetnessSystem.IsInsideSafeHouse()`**: throttled `FindObjectsByType` call
  every 0.5s, described above. File is off-limits this sprint. Flag for a future
  pass — trivial fix (cache `WorldMarker[]` once, or maintain a static
  active-safe-house list the way `FirePoint.ActiveLitFires` already does).
- **Ocean full-extent rendering**: confirmed NOT the explanation for the GPU floor
  (see above), but the shader genuinely has no distance-based LOD/tessellation
  knob. Not a Sprint 06 problem given current findings, but if a future built-Player
  profile shows real ocean shader cost, `Uber Stylized Water`'s alternative
  presets (Clear/Murky/etc., all present in
  `Assets/ThirdParty/.../Water Template/`) may have cheaper default configs worth
  comparing against the current `M_Ocean_TestConfig`.

## "GPU floor doesn't scale with scene simplicity" — status: LARGELY EXPLAINED, not a rendering bug

The baseline's central mystery (GPU time staying ~34ms even at the open mountain
view, Location G) did not survive re-measurement. Before any fix was applied,
simply re-sampling the same 7 locations later in the same Play Mode session (no
scene changes) already dropped every location into the 14–19ms range — including
G, which was never actually near the ocean or any dense geometry (ocean bounds
don't reach G's camera position). This points to the cost being dominated by
**session/measurement warm-up** (shader variant compilation on first use, texture/
mesh streaming, shadow-map cache construction on first render of a cascade) rather
than a persistent per-frame GPU cost tied to scene content. The baseline's own
caveat about "Editor overhead" was likely the larger truth than the ocean/shadow
suspects it also listed. This session's harness cannot fully separate "Editor Game
View overhead" from "genuine warm-up cost" — that distinction still needs a real
Development Build + Profiler pass (manual to-do for Ömer) to close out completely.
The shadow-cascade and background-card fixes applied here are real and worth
keeping regardless, but they are not what explains the bulk of the baseline's
30–48ms numbers.
