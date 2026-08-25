# TRLM World Design — Island Blockout

**Sprint:** World & Level Design Sprint 02 (2026-08-22, extended pass same day)
**Scenes:**
- `Assets/_TRLM/Scenes/Production/20_Island_Blockout.unity` — the playable island
- `Assets/_TRLM/Scenes/Production/05_Neighborhood_Cinematic.unity` — separate, compact departure-prep scene (NOT part of the island; see dedicated section below)

**Terrain:** `Assets/_TRLM/Scenes/Production/IslandTerrainData.asset` — 800×800m, 230m max height, procedurally shaped (not hand-sculpted — see Known Limitations)

## Gameplay Integration Sprint 05 Update

No map/terrain/region redesign this sprint (out of scope per the brief). What
changed structurally: the 5 pre-existing `LootPoint` markers now have a
functional `LootSpawnPoint` component (see `GameplayIntegration.md`); a new
`LandingZone_Beach` trigger and `BurialZone_01` were added as authored gameplay
triggers; the existing settlement `SafeHouse`-type marker gained
`SafeHouseArea`/`SleepInteraction` components; `05_Neighborhood_Cinematic.unity`
got 3 placeholder hook GameObjects (`PreparationTrigger`, `EquipmentLoadPoint`,
`DeparturePoint`) with no Timeline content — deliberately deferred, lowest
priority in the brief. All otherwise unchanged from the Sprint 02/03 layout
below.

## Extended Pass Update (same-day continuation)

The initial blockout pass used primitive placeholders for all forest/rock content. This
continuation replaced them with real assets and added physical systems:

- **~520 placeholder capsule trees → real vegetation.** Built from
  `Assets/ThirdParty/Environment/TreePack/Tree_Pack.fbx` (user-provided, see
  `AssetRegistry.md` for the license caveat). 4 combined trunk+branch "full tree" prefabs
  and 5 background-card prefabs, randomly distributed across the same density-tuned
  clusters as the original placeholder pass (same seed, same radii/density values — the
  composition logic didn't change, only the geometry).
- **~213 placeholder cube rocks → real stylized rocks.** Built from
  `Assets/ThirdParty/Environment/RockPack/Free Pack - Rocks Stylized.fbx` (11 distinct
  meshes, 36–1,438 tris). Every placed instance has a `MeshCollider`.
- **Ocean wave physics + rowboat buoyancy.** New `TRLM.World.BuoyancyController`
  (`Assets/_TRLM/Scripts/World/BuoyancyController.cs`) samples the same two-layer
  Gerstner-style wave function the water shader uses (read from the water material's
  `_1st_Wave_*`/`_2nd_Wave_*` properties) at 4 hull points and applies spring-damper
  buoyancy forces per point — pitch/roll emerge naturally from the force asymmetry, no
  separate rotation code. Verified live in Play Mode: the boat settles to a stable draft
  and continuously heaves/rolls by small amounts (no jitter, no runaway spin, no
  tunneling). The boat was moved off the beach into open water at (400, 1.5, -25) for the
  Region 1 "sea approach" framing, replacing its earlier beached placement.
- **3 mountain rockfall zones.** New `TRLM.World.RockfallZone` component:
  pooled rocks (4 per zone, reused from the stylized rock pack), each zone fires on its
  own random timer (25–90s depending on zone), launches one rock down-slope with real
  Rigidbody physics, and returns it to the pool once it settles or times out — no
  unbounded spawning. Placed at Rock Belt narrow pass (400,550), Mountain Pass boulder
  choke (350,615 — matches `SetPiece_02`), and Summit Approach (400,735). **A real bug was
  caught and fixed during Play Mode verification**: the pooled rocks initially had no
  `Collider` at all (the source prefabs are static dressing with none), so they fell
  through the terrain indefinitely; fixed by adding a convex `MeshCollider` sized to each
  part's corrected FBX scale, plus `CollisionDetectionMode.ContinuousDynamic` on the
  Rigidbody. Re-tested live: a triggered rock now lands within ~0.5m of the actual terrain
  height and settles correctly.
- Shoreline advance/recede and continuous small-wave motion were **already active** from
  the initial pass (`M_Ocean_TestConfig.mat` has `_ENABLEWAVE`/`_ENABLESHORELINE` on) —
  this pass didn't need to add that, only to make the boat physically respond to it.

---

## Island Structure

Single Unity `Terrain` (800×800m) rising from a shoreline at world Z≈0 to a
summit/cave staging plateau at Z≈800. Height and surface-ruggedness are both
driven by an 8-region curve along the Z axis, with Perlin-noise ridges and a
hand-flattened landing cove, so the coastline reads as naturally jagged
rather than a straight rectangle. Terrain is textured with 5 blended
layers (grass, forest dirt, rock, mud, wet ground) driven by slope + region,
not painted by hand.

Philosophy applied: **semi-open exploration, density over raw size.** 800m
is a fraction of what "8-10 hour game" might suggest as a naive scale — the
intent is the player crosses meaningful content every 60-150m, not empty
terrain. This blockout establishes the skeleton; final pass will add
detail without growing the footprint.

---

## Regions

| # | Region | Z range (m) | Elevation | Status |
|---|---|---|---|---|
| 1 | Sea Approach | -250 to 0 (ocean, outside terrain) | sea level | Ocean plane + rowboat placed |
| 2 | Coastal Forest | 0–140 | 0–7% of max height | Sparse forest placeholders, landing cove, first storytelling props |
| 3 | Abandoned Settlement | 140–230 | 7–10% | 1 real house + 3 ruin clusters, SafeHouse_01, loot/storytelling markers |
| 4 | Deep Forest | 230–420 | 10–30% | Dense forest placeholders, wolf/boar/bear zones, 2 traversal candidates |
| 5 | Rock Belt | 420–550 | 30–62% | Rock clusters, snake zone, radio tower + rock arch landmarks |
| 6 | Mountain Pass | 550–680 | 62–86% | Boulder clusters, bear territory, Set-Piece 02, 2 traversal candidates |
| 7 | Summit Approach | 680–770 | 86–98% | Sparse rock formations, passive wildlife, SafeHouse_03 candidate |
| 8 | Secret Cave (entrance only) | 770–800 | ~97% | Staging marker placed only — interior deferred per instructions |

### Region 1 — Sea Approach
Ocean plane (`Uber Stylized Water`, waves + shoreline enabled) covers
X -50..850, Z -250..60, overlapping the terrain's low shoreline so there's
no visible gap. `Rowboat_Landed` sits in the flattened cove at
(400, ~0.5, 15). From open water the mountain silhouette (peak at Z=795,
230m) is visible above the coastal treeline — establishes "that mountain is
the destination" per the brief.

### Region 2 — Coastal Forest
Sparse tree placeholder clusters (`CoastalForest_Sparse_West/East`,
`CoastalForest_EdgeStrip`) at ~35% density, deliberately leaving open sightlines.
Landing cove and a 90m radius around it are excluded from tree placement so
the beach reads clearly. First storytelling beat here:
`Story_AbandonedLuggage_Coast` — a prior human trace, before the settlement
proper.

### Region 3 — Abandoned Settlement
`Settlement_MainHouse` (the real P0 abandoned-house asset, scale-corrected
in Sprint 01's audit) plus three additional ruin clusters built from
primitives: a collapsed foundation + leaning wall fragment, a small storage
shed footprint, and a 6-post broken fence line — reaching the "3-6
structures" target without needing new assets. `SafeHouse_01` marks the
main house as the first safe-house candidate (real interior, one door,
central on the primary route). Storytelling: skeleton, broken door, and a
second "violent aftermath" detail — communicates people lived here,
something violent happened, then it was abandoned, without over-explaining.

### Region 4 — Deep Forest
The strongest atmosphere region per the brief. Three dense clusters
(~55-62% density, capsule spacing tuned for ~20-30m visibility) plus one
deliberately sparser "clearing ring" (~32% density) around
`Landmark_LargeOldTree` at (400, 250) — a clearing where the mountain
becomes visible again, breaking up the low-visibility feeling. Two wolf
pack territories flank it (west/east), a night-only high-risk wolf corridor
runs through the center (feeds `SetPiece_01`), a boar/mud pocket, and one
bear territory near the region's water source. `TR_CollapsedBridge_DeepForest`
is a risk/reward shortcut candidate.

### Region 5 — Rock Belt
Vegetation thins sharply (one fringe cluster only); rock clusters dominate,
placed as a narrow-pass formation (`RockBelt_NarrowPass_Walls`) framed by
`Landmark_RockArch` — the natural gateway between forest and mountain.
`Landmark_RadioTower_Candidate` sits on a ridge here, visible from lower
regions as an orientation landmark. Snake zone (warm exposed rock) and a
mountain-stream water source feeding `Landmark_Waterfall_Candidate`.

### Region 6 — Mountain Pass
High-risk per the brief: two boulder clusters, a cliff-edge rock cluster,
`SafeHouse_02_RangerHut_Candidate` is deliberately placed one region *before*
this (in Rock Belt) rather than inside it, so the player has a shelter
option before the hardest stretch, not during it. `SetPiece_02` (bear /
dangerous pass) sits at a boulder choke point. Two traversal candidates
(narrow ledge, rope crossing) on the cliff edge.

### Region 7 — Summit Approach
Vegetation gone; large, sparser rock formations (wind-scoured feel — lower
noise ruggedness than Mountain Pass despite being higher, intentional).
`SafeHouse_03_MountainShelter_Candidate` is the last safe area before the
cave. Passive wildlife (mountain goats) — first non-hostile animal
presence, reinforcing "not every animal is an enemy."

### Region 8 — Secret Cave (entrance only, per instructions)
`Landmark_CaveEntrance_Staging` marks the approach/staging point at
(400, 790) on the summit plateau. Interior is explicitly deferred to a
future sprint — only location, approach, and framing were established here.

---

## Routes

### Primary Route
Sea → landing cove (400, 15) → Settlement (420, 170) → Deep Forest center
(400, 350, passing between the two wolf territories) → Rock Belt arch
(400, 530) → Mountain Pass boulder choke (350-480, 600-630) → Summit
plateau (400, 700-790) → Cave staging (400, 790). This stays legible via
the mountain's constant visibility and the landmark chain (old tree → rock
arch → radio tower → peak), not forced waypoints.

### Alternative / Risk Routes
- **Deep Forest**: `TR_CollapsedBridge_DeepForest` (550, 350) — shortcut
  east around the wolf night corridor, at collapse-hazard risk.
- **Rock Belt → Mountain Pass**: western line via `TR_ClimbingRoute_RockBelt`
  (300, 500) and `TR_RopeTraversal_Cliff` (200, 620) avoids the boulder
  choke point but is more exposed and further from `SafeHouse_02`.
- **Mountain Pass**: `TR_NarrowLedge_MountainPass` (250, 650) is a faster,
  more dangerous line than the eastern boulder-field path.
- **Shoreline**: `HT_PirateRaidZone_Shoreline` (150, 20) is well off the
  main landing — an optional west-coast detour, not on the critical path.

Not every path is equally useful, per the brief: the western routes trade
safety (further from safe houses, more exposure) for speed.

---

## Wildlife Habitat Zones

All zones use the shared `TRLM.World.WorldMarker` component
(`MarkerType.WildlifeZone`) with `animalType`/`maxPopulation`/
`activityPeriod`/`aggressionLevel`/`spawnProbability`/`respawnDelaySeconds`
exposed in the Inspector for a future spawner system to read — no AI is
implemented yet.

| Zone | Location | Notes |
|---|---|---|
| WZ_Wolf_DeepForest_01/02 | (250,300) / (550,320) | Two pack territories flanking the forest core |
| WZ_Wolf_NightCorridor_01 | (400,350) | Night-only, high aggression, feeds Set-Piece 01 |
| WZ_Wolf_ForestEdge_01 | (400,460) | Rock Belt fringe patrol |
| WZ_Wolf_Den_Candidate | (220,310) | Specific den site inside DeepForest_01 |
| WZ_Bear_Territory_01/02 | (560,395) / (200,500) | Rare (spawnProbability 0.15), riverside + isolated |
| WZ_Boar_MudArea_01 / ForestFloor_01 | (400,380) / (260,280) | Muddy/water-adjacent |
| WZ_Snake_Settlement_01 / RockyArea_01 | (380,200) / (300,470) | Ruins + rocky exposed ground |
| WZ_PassiveWildlife_MountainSlope/Summit | (350,650) / (450,730) | Non-hostile, aggression 0.05 |

Wolves cover roughly a third of the map (deep forest core + fringe), not
the whole island, per the brief. Bears are deliberately rare — only two
territory candidates, low spawn probability.

---

## Safe Houses

1. **SafeHouse_01** — Settlement House (420, 170). Real intact structure,
   central on the primary route. Strongest candidate.
2. **SafeHouse_02_RangerHut_Candidate** (250, 460) — Rock Belt / Deep
   Forest boundary, no structure placed yet (marker only) — positioned
   deliberately *before* the Mountain Pass danger spike.
3. **SafeHouse_03_MountainShelter_Candidate** (420, 740) — last safe point
   before the cave staging area.

---

## Human Threat Zones (rare, per instructions)

- **HT_Checkpoint_MountainRoute** (500, 600) — old military checkpoint on
  the mountain route; soldier encounter candidate; feeds `SetPiece_03` and
  two loot markers (medical, soldier gear).
- **HT_PirateRaidZone_Shoreline** (150, 20) — west shoreline, aftermath
  only. No structures were built to imply an active pirate camp — consistent
  with the brief's pirate history (raided the settlement, looted, left).

Two zones total. Not a Far-Cry-style enemy-camp density.

---

## Set-Piece Candidates

1. **SetPiece_01 — Night Wolf Pursuit** (400, 350): uses the wolf night
   corridor through Deep Forest's narrowest visibility stretch.
2. **SetPiece_02 — Bear / Dangerous Pass** (350, 600): Mountain Pass
   boulder choke point.
3. **SetPiece_03 — Soldier Encounter** (500, 605): tied to the mountain
   checkpoint.

Locations and spatial context only — no scripted sequences implemented.

---

## Traversal Candidates

5 markers (`TRLM.World.WorldMarker`, type `Traversal`): a Rock Belt climb,
a Mountain Pass narrow ledge, a Deep Forest collapsed-bridge shortcut, a
Summit slippery slope, and a cliff rope-traversal alternate. Composition
(map layout) is in place; no climbing/mantle mechanics were built, per
instructions.

---

## Loot Candidates

5 markers, all tied to believable containers/locations rather than scattered
on the forest floor: settlement cabinets, the storage shed ruin, abandoned
coastal luggage, and two checkpoint-area markers (medical, soldier gear).

---

## Water Sources

4 markers: a Deep Forest stream (feeds the nearby bear territory logically),
a coastal pond, a settlement rain-collection point, and a Rock Belt mountain
stream (source of the Waterfall landmark downhill). Not evenly distributed —
water is scarce enough to matter for route planning, per instructions.

---

## Landmarks

Mountain peak (global landmark, visible from the sea onward), a radio/
observation tower candidate on a Rock Belt ridge, an old oversized tree at
the Deep Forest clearing, a rock arch framing the Rock Belt narrow pass, a
waterfall candidate near the mountain-stream source, and the cave-entrance
staging marker. Six landmarks total across an 800m island — deliberately
not overused, per instructions.

---

## Environmental Storytelling

Five markers, each with a specific reason (not decoration): abandoned coastal
luggage (first human trace), settlement skeleton + broken door + a second
"violent aftermath" detail (people lived here, something violent happened),
and a ruined camp in Deep Forest (aftermath of the historic pirate raid —
explicitly *not* an active camp).

---

## Performance Notes (this pass)

- 1,594 total GameObjects, 1,496 renderers, **83 unique materials** in the
  scene — forest/rock placeholders share exactly 2 materials
  (`M_TreePlaceholder`, `M_RockPlaceholder`) across ~700 instances, so
  material count stays low despite object count.
- Placeholder trees have **no colliders** (deliberately stripped after
  instantiation) to avoid physics bloat during blockout — real foliage
  assets (once unblocked) should decide collider strategy properly (likely
  none, or a shared capsule per cluster).
- No LOD groups, no occlusion culling, no GPU instancing set up yet — out
  of scope for a blockout pass, flagged for Codex.

---

## Known Limitations

- **Forest and rocks are now real geometry** (user-provided low-poly tree
  pack + stylized rock pack), replacing the initial placeholder pass — but
  their **license is unverified** (no license file was bundled with either
  zip; used per Ömer's direct instruction). See `AssetRegistry.md`.
- The original CC0 forest pack from the P0 acquisition mission remains
  separately blocked (Blender not installed) and unused — superseded by
  the local tree pack for this sprint, not fixed.
- Ocean coverage is a large flat plane with wave-shader animation; it was
  not tested at extreme camera angles/distances for seams. Buoyancy is
  verified stable, but only tested at one location/timeframe, not across
  extended play sessions.
- Terrain was shaped procedurally (curve + Perlin noise), not hand-sculpted
  — a first pass, not a final landscape. Ömer should treat every elevation/
  slope as adjustable.
- No formal LOD/occlusion/GPU-instancing on the new real vegetation/rocks
  yet — flagged for Codex above.

---

## Sprint 03 Update — Wildlife Integration, Validation, Polish (2026-08-22)

### Wildlife
4 of the existing wolf habitat markers (`WZ_Wolf_DeepForest_01/02`, `WZ_Wolf_NightCorridor_01`,
`WZ_Wolf_ForestEdge_01`) are now functional spawn zones (`WildlifeSpawnZone` + `WildlifeSpawner`,
referencing `SpeciesProfile_Wolf`). Verified live: 9 wolves spawn across the 4 zones, roam their
territory, detect/chase/attack the player, deal real damage, and disengage correctly when the
player escapes. Full behavior detail in `Documents/WildlifeSystem.md`. Bear/Boar/Snake/
MountainGoat zones remain markers only — no 3D assets exist for them yet (unchanged from before,
not a Sprint 03 regression).

### Route Walkability Audit — PASS
NavMesh baked (collider-based) for the whole island. `NavMesh.CalculatePath` was checked across
all 8 segments of the primary route (Sea → Coast → Settlement → Deep Forest → Rock Belt →
Mountain Pass → Summit → Cave staging) — every segment returns `PathComplete`. No broken paths,
cliff traps, or terrain holes found along the primary route. Off-route areas (deep in rock
clusters, extreme mountain slopes) were not individually walked/audited.

### Rockfall & Ocean/Rowboat Re-Validation — PASS, both stable
Re-triggered `RockfallZone_02_MountainPass` live: the rock fell, landed within ~0.1m of actual
terrain height, and settled correctly — pooling/collision/tunneling all still fine. Ocean/rowboat
re-checked mid-rockfall-event: boat stayed at a stable draft (~1.34m) with modest pitch/roll, no
jitter or extreme rotation. Neither system needed changes this sprint.

### Tree Collision Strategy
28 of ~520 trees (the ones within 22m of a primary-route waypoint, the settlement, or a wildlife
zone center) received a cheap `CapsuleCollider`. The ~285 background-card trees never receive
colliders (they're the cheap distant-filler tier by design). This directly fixes "player can walk
through every trunk" for the areas that matter without collider-izing decorative background trees.

### Rock Collider Audit
Of 213 rock `MeshCollider`s, 97 (mesh >200 triangles) were replaced with bounds-fit `BoxCollider`s;
116 low-poly ones (≤200 tris) kept their `MeshCollider` since the cost is negligible. Current
scene collider total: 254 (117 Mesh, 107 Box, 28 Capsule on trees, 2 others).

### GPU Instancing / LOD Audit
GPU Instancing enabled on all 17 environment materials (tree/rock/grass/terrain). No `LODGroup`s
were created — the tree pack's background cards and full trees are unrelated meshes (not detail
levels of the same asset), same for the 11 distinct rock meshes, so a real LOD chain needs
generated lower-poly variants that don't currently exist. Documented rather than faked, per
sprint instructions.

### Character Cast Recommendation (short, no new sourcing this sprint)
The 3 free Reallusion base meshes (`CC3_Base_Plus`, `Neutral_F`, `Neutral_M`) have **zero
identity-shaping morphs** — confirmed in the P0 audit and unchanged since. The only realistic
levers available without new software/assets are: **(1) skin-tone/material tint per character**
(cheap, already-owned textures can be recolored), **(2) hair/clothing swaps** if any free
hair/clothing assets are sourced later (none currently in the project), and **(3) minor uniform
scale variation** (height difference reads as a distinct silhouette at a glance). Facial/body
proportion changes are not possible without Reallusion's Character Creator software itself.
Recommendation: budget a small follow-up task for (1)+(3) — tint + height variation — as the
cheapest real improvement before any new character asset is purchased or downloaded.

### Performance Snapshot (Island scene, Edit Mode, wildlife not spawned)
GameObjects: 2,466 · Renderers: 1,621 · Materials: 87 · Colliders: 254 (117 Mesh / 107 Box / 28 Capsule + 2 other)
In Play Mode with wildlife active: +9-12 wolves typical (well under the ~12-per-species global cap).

---

## Neighborhood Cinematic Scene (Separate, Pre-Island)

`Assets/_TRLM/Scenes/Production/05_Neighborhood_Cinematic.unity` — deliberately **not**
part of the island terrain or explorable world. A compact (~35×35m) yard set: a house
facade backdrop (only the wall + porch roof the camera would see, not a full building), a
trailer/vehicle placeholder hauling the rowboat, 5 gear props (duffel bags, supply crates,
rope coil) being loaded, and the 5 friends positioned in a natural small cluster — two
loading gear near the boat, two chatting near the porch, one mid-yard carrying a bag
toward the boat. Three `CinematicCameras` transforms (wide establish, loading close-up,
porch-friends) are placed as framing references for a future cinematic/timeline pass; no
camera-switching or animation logic was built (out of scope this sprint).

**Character honesty note:** the 5 friends reuse the project's only 3 available free
character base meshes (documented as a hard limitation since Foundation Sprint 01 — no
identity-shaping morphs exist in the free Reallusion export). Two pairs are visually
identical placeholders right now (e.g. Elias and one other friend share the same male
base). This is expected and already tracked as an open item, not a new problem — it will
need either new character sourcing or Character-Creator-based customization before this
scene is presentation-ready.

## Codex Review Priorities

1. Terrain performance at 800×800/513 heightmap resolution — check draw
   calls and whether heightmap/alphamap resolution should scale down for
   a blockout-stage scene.
2. Forest/rock object counts (~520 trees + ~213 rocks = ~733 combined) —
   evaluate whether this should move to GPU-instanced rendering before any
   playtesting pass. No formal `LODGroup` was set up on the tree prefabs
   despite having natural high/low-detail variants (full trees vs.
   background cards) — wiring that up is a quick, high-value follow-up.
3. Collider coverage — trees have none (deliberate, avoids physics bloat
   from ~520 colliders on cosmetic vegetation); rocks have per-instance
   `MeshCollider`s (~213). Review whether that many individual mesh
   colliders is acceptable or should collapse to simplified proxies.
4. Verify no navigation dead-ends — the rock clusters/boulder fields were
   placed by radius-based scatter and were not individually checked for
   accidentally sealing off a route.
5. Wildlife zone radius/placement logic — sanity-check zone overlaps (e.g.
   wolf night corridor overlapping boar mud area) before any spawner
   system consumes these markers.
6. **License verification for the 2 local asset packs** (tree pack, rock
   pack) — used on Ömer's direct instruction with no bundled license file;
   flagged `LICENSE_UNVERIFIED` in `AssetRegistry.md`. Needs the original
   source confirmed before commercial ship.
7. `BuoyancyController`'s wave-sampling function approximates the water
   shader's Gerstner math in C# rather than reading it directly — review
   for drift between visual wave motion and physical wave motion over
   longer play sessions, and whether it should scale with sea state
   (calm/storm) once a weather system exists.
8. `RockfallZone` pooled rocks use `MeshCollider(convex=true)` — convex
   hulls simplify real geometry, so impact shapes are approximate. Fine
   for a blockout; revisit if precise rockfall collision matters later.
9. **Sprint 03**: Wolf AI has no visual animation (source mesh has no rig at all) — review
   whether to prioritize rigging this mesh or sourcing a new one before wolf combat needs
   to feel real rather than functional.
10. **Sprint 03**: Review the 28-tree/22m-radius tree-collider corridor for coverage gaps —
    it was sized by proximity to known route waypoints/zones, not by walking every path.
11. **Sprint 03**: Rockfall impact currently has no `IDamageable` hookup (rocks land and
    settle but never damage the player on collision) — sprint instructions called this
    optional ("if practical"); flagging as a real gap, not forgotten.
