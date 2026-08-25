# TRLM Creature Material Audit

**Sprint:** Character & Creature Material + Animation Sprint 04 (2026-08-22)
**Scope:** Wolf, Bear, Wild Boar, Snake, Mountain Goat

---

## Diagnostic categories used below

`MATERIAL_OK` / `MATERIAL_LINK_BROKEN` / `TEXTURES_EXIST_BUT_UNASSIGNED` /
`TEXTURES_MISSING` / `REBUILD_REQUIRED` / **`NO_ASSET`** (species has zero 3D
model in the project — reported honestly per sprint instructions rather than
fabricated)

---

## Wolf — `Assets/ThirdParty/Animals/Wolf_CC0`

**Status before this sprint:** `REBUILD_REQUIRED`. 2 materials
(`Dark_Gray.mat`, `Dark_White__also_known_as_light_gray_.mat`), both flat
uniform mid-gray (0.82, 0.82, 0.82), no texture maps at all (confirmed —
mesh internally still named "Cube", a Blender default-export leftover, first
found in the P0 audit and re-confirmed this sprint).

**Repair applied:** both materials given a believable two-tone wolf
coloration matching the mesh's actual two-material zone split (the mesh is
already split into a "dark" zone and a "light" zone, it was just never
colored):

| Material | Old `_BaseColor` | New `_BaseColor` | Role |
|---|---|---|---|
| `Dark_Gray` | (0.82, 0.82, 0.82) flat | (0.28, 0.26, 0.23) dark brownish-grey | back/body coat |
| `Dark_White` | (0.82, 0.82, 0.82) flat | (0.62, 0.58, 0.52) lighter warm grey | underbelly/legs |

Both `_Smoothness` lowered to 0.12–0.15 (matte fur look, was defaulting
higher/shinier). Verified visually — produces a natural dark-back /
light-underbelly pattern instead of a flat grey blob.

**Status after this sprint:** `MATERIAL_OK` (color-only, no texture maps
exist to assign — a flat two-tone material is the correct/achievable ceiling
for this asset without new texture sourcing).

---

## Bear / Wild Boar / Snake / Mountain Goat

**Status:** `NO_ASSET` for all 4 species. Confirmed (originally in the P0
audit, re-confirmed this sprint by inspecting the live
`WildlifeSpeciesProfile` ScriptableObjects): `SpeciesProfile_Bear`,
`SpeciesProfile_Boar`, `SpeciesProfile_Snake`, `SpeciesProfile_MountainGoat`
all exist with fully-authored gameplay data (population, spawn chance,
day/night multipliers, aggression, etc. — see `WildlifeSystem.md`) but
**`animalPrefab = null` on every one** — there is no 3D model, no mesh, no
material, nothing to repair for these 4 species. `WildlifeSpawner` already
detects this and disables itself with a clear warning per zone rather than
erroring or spawning nothing silently.

Per explicit sprint instruction ("do not accept fake placeholder... materials
as done", "do not fabricate"), **no placeholder capsule/cube creature was
substituted for these 4 species.** Inventing a fake "Bear" out of a colored
primitive would look done in a screenshot while providing zero real asset
value, and was explicitly the kind of fake-progress this sprint's brief
prohibited. These remain honestly `NO_ASSET` and need new asset sourcing
(zero-budget constraint applies — same free/CC0 sourcing pass as the P0
sprint would need to be repeated for 4 more animal species) before any
material or animation work is possible.

**Recommendation for Ömer:** this is the single largest remaining content
gap in the wildlife roster. A future P0-style sourcing pass (CC0/free rigged
animal models — Bear, Boar, Snake, Mountain Goat) is the correct next step,
not a material/animation sprint against nothing.

---

## Prefab status

| Species | Prefab | Status |
|---|---|---|
| Wolf | `Assets/_TRLM/Prefabs/Animals/PF_Wolf.prefab` | Updated — new material, `Visual` child restructure, Animator + substitute idle sway (see `AnimationPipeline.md`) |
| Bear | — | Not created — no source asset. Report only. |
| Wild Boar | — | Not created — no source asset. Report only. |
| Snake | — | Not created — no source asset. Report only. |
| Mountain Goat | — | Not created — no source asset. Report only. |
