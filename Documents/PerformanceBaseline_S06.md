# TRLM Performance Baseline — Sprint 06

**Captured:** 2026-08-22, Play Mode, `20_Island_Blockout.unity`, before any Sprint 06
optimization work. Quality level: PC (level 1), URP asset `PC_RPAsset`.

---

## Measurement caveat (read before drawing conclusions)

`get_performance_stats`'s render block (`drawCalls`/`batches`/`setPassCalls`/
`triangles`/`vertices`) returned **0 across every sample**, including after
forcing a Game View render via `capture_game_view` — this MCP build's render-stats
hook isn't populating in this Editor/headless-automation configuration. **Frame
timing (`cpuFrameTimeMs`/`gpuFrameTimeMs`) DID populate reliably and is the primary
metric used below.** Scene-wide object counts (renderers/colliders/lights/AI) were
captured directly via `FindObjectsOfType`, not the stats API, and are exact.
Absolute frame-time numbers below likely include Unity Editor overhead (not a pure
built-player number) — treat deltas *between* Sprint 06's before/after measurements
as meaningful, and the absolute "48ms in a built Player" claim as not proven by this
harness. A real player-build profiling pass remains a manual to-do for Ömer.

---

## Scene-wide static counts (constant regardless of player position)

| Metric | Count |
|---|---|
| Renderers | 1631 |
| Colliders (total) | 278 |
| — of which MeshCollider | 117 |
| Rigidbodies | 1 (the rowboat) |
| Lights | 2 (sun + one other) before flashlight/fire are activated |
| Unique materials in use | 98 |
| Active WolfAI instances | 3 |

URP asset settings: `shadowDistance=50`, `shadowCascadeCount=4`,
`mainLightShadowmapResolution=2048`, `additionalLightShadows=true` (also 2048,
soft shadows on), `renderScale=1`, `msaa=1` (off).

---

## Per-location frame timing (Play Mode, teleported player, real in-editor samples)

| Location | World Pos | CPU frame (ms) | GPU frame (ms) | Notes |
|---|---|---|---|---|
| A — Coast | (380, 8, 50) | 33.4 – 37.4 | 34.3 – 36.6 | 2 samples, some jitter |
| B — Deep Forest | (300, 46, 320) | 43.6 | 36.2 | Highest CPU of the "normal" locations |
| C — Settlement | (420, 22, 170) | 40.9 | 34.4 | Dense structures + loot markers |
| D — Wolf Encounter (day) | (400, 66, 350) | 36.8 | 37.1 | Near active wolf zone, 3 wolves live |
| E — Night + Flashlight (same spot as D) | (400, 66, 350) | 33.8 | 38.9 | Forced night + flashlight on |
| F — Safe House + Fire + Night + Flashlight | (420, 22, 170) | **48.3** | **43.2** | Worst measured — stacked: settlement density + night + flashlight (spot+shadow) + fire (point light) |
| G — Mountain-facing view | (400, 212, 700) | 31.3 | 34.6 | Lowest CPU — open area, fewer nearby colliders |

**60 FPS target = 16.7ms/frame.** Every single sample is 2–3× over budget.
None of the 7 locations meets even the sprint's temporary "≥50 FPS" (20ms)
acceptance threshold.

---

## Bottleneck classification

**MULTIPLE**, with two distinct patterns:

1. **CPU varies meaningfully by location** (31.3ms at the open mountain view vs.
   43.6–48.3ms in Deep Forest/Settlement) — this points to local **CPU cost
   scaling with nearby renderer/collider/script density**, consistent with the
   117 `MeshCollider`s (expensive narrow-phase physics vs. primitive colliders)
   concentrated in rock-heavy regions, and per-object `Update()` proliferation
   across trees/rocks/markers/survival systems.
2. **GPU has a suspiciously high floor (~34ms) even at the open mountain view**
   with comparatively little nearby geometry — this does NOT scale down with
   scene simplicity the way a normal draw-call-bound cost would, suggesting a
   **global, distance-independent GPU cost**: candidates are the ocean shader
   (Uber Stylized Water, likely rendering its full extent every frame
   regardless of camera position/distance), the 4-cascade 2048-res shadow
   maps re-rendering every frame regardless of what's actually in cascade
   range, or Editor Game-View overhead itself (see caveat above — cannot
   fully rule this out with this measurement harness).

**Top suspects for Sub-Agent A to investigate, in priority order:**
1. Ocean/water shader render cost (full-extent vs. distance-culled)
2. `MeshCollider` count (117) — candidates for `BoxCollider`/`CapsuleCollider` replacement, especially in Deep Forest/Rock Belt
3. Tree/rock renderer shadow-casting distance (are distant background cards casting shadows unnecessarily?)
4. Shadow cascade count/resolution (4× 2048 is a lot for a stylized survival game)
5. Per-frame `Update()` cost across ~1600 renderers' owning scripts + the new Sprint 05 survival systems (Hunger/Thirst/Wetness/Cold) — confirm none of these poll expensively every frame
6. Stacked light cost at the worst-case location F (sun + flashlight spot+shadow + fire point) — confirm flashlight/fire shadows are worth their cost

This baseline intentionally does **not** guess at fixes — Sub-Agent A's job is to
investigate these specific suspects with the Profiler/rendering-stats tools and
apply targeted, measured fixes, then re-profile the same 7 locations for
`PerformanceReport_S06.md`.
