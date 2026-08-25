# TRLM Character Material Audit

**Sprint:** Character & Creature Material + Animation Sprint 04 (2026-08-22)
**Scope:** Elias, Mira, Jonah, Lena, Noah (the 5 named human characters)

---

## Pre-existing limitation (carried from P0/Sprint 01, not new)

Only **3 distinct human base meshes** exist in the project at all
(`Assets/ThirdParty/Characters/CC_Base/{01_CC3_Base_Plus, 02_Neutral_F, 03_Neutral_M}`).
5 named characters must share these 3 meshes — documented since the P0 audit
(`AssetRegistry.md` §6/§7) as a real asset-count gap, not something this sprint could
fix without new sourcing (explicitly out of scope: no budget, no Blender pipeline
available — see `AnimationPipeline.md`). Casting used this sprint:

| Character | Base mesh | Shares mesh with |
|---|---|---|
| Elias | `03_Neutral_M` | — (unique) |
| Mira | `02_Neutral_F` | Lena (body shape only, see differentiation below) |
| Jonah | `01_CC3_Base_Plus` | Noah (body shape only) |
| Lena | `02_Neutral_F` | Mira |
| Noah | `01_CC3_Base_Plus` | Jonah |

---

## Diagnostic categories used below

`MATERIAL_OK` / `MATERIAL_LINK_BROKEN` / `TEXTURES_EXIST_BUT_UNASSIGNED` /
`TEXTURES_MISSING` / `REBUILD_REQUIRED`

---

## Root cause (applies to all 3 base meshes identically)

Before repair, all 51 materials (17 per mesh × 3 meshes) came in as
`TEXTURES_MISSING` for a base color/diffuse map — **not a broken link, not an
unassigned-but-present texture**. Investigated directly: the Reallusion CC3+
"Digital Human Shader" export for these free base meshes ships **no plain
diffuse/albedo texture at all**. What exists per material is a set of
specialized maps: `_ao` (real ambient occlusion), `_NBMap` (confirmed genuine
tangent-space normal map — sampled ~(0.506, 0.502, 1.0), correct neutral-normal
values), `_BCBMap` (investigated and confirmed to be a **grayscale blend
mask**, not a color map — sampled ~0.45–0.52 gray across multiple pixels),
plus `_MicroNMask`, `_SSSMap`, `_TransMap`, `_SpecMask`, `_RGBAMask`,
`_roughness`, and two `_ResourceMap_*` utility maps. None of these substitute
for a base color texture in a standard URP/Lit pipeline.

Additionally: the base meshes are **fully nude and bald** — no hair or
clothing geometry exists at all. This is a modeling gap, not a material gap;
out of scope for this sprint (would need new geometry, not new materials).

Per sprint instruction ("if textures truly do not exist, report that clearly
and build a proper replacement material workflow"), this is what was done.

---

## Repair applied (identical per-material logic across all 3 meshes × 17 slots = 51 materials)

1. `ModelImporter.materialLocation = External` set on all 3 FBX imports —
   extracts the embedded materials as standalone `.mat` assets so they can be
   edited without touching the source FBX (source `.Fbx` files themselves are
   untouched — safe-copy principle followed).
2. Each material's shader forced to `Universal Render Pipeline/Lit`.
3. Real texture maps wired where they exist and are actually usable:
   `_NBMap` → `_BumpMap` (+ `_NORMALMAP` keyword enabled), `_ao` → `_OcclusionMap`.
4. Since no diffuse texture exists, `_BaseColor` set to a solid,
   research-based realistic skin/eye/mouth tone per slot (see table below) —
   an intentional, documented **replacement material**, not a texture fix.
5. `_Smoothness` tuned per material type (skin lower/matte, eyes higher/wet).

| Material slot (×17, per character) | Treatment |
|---|---|
| `Std_Skin_{Head,Body,Arm,Leg}` | Skin tone `_BaseColor` + real `_NBMap` normal + real `_ao` occlusion |
| `Std_Cornea_{L,R}` | Sclera-tone `_BaseColor`, higher smoothness (wet look) |
| `Std_Eye_{L,R}` | Iris-tone `_BaseColor` (dark brown) |
| `Std_Eye_Occlusion_{L,R}` | Dark contact-shadow tone |
| `Std_Upper_Teeth` / `Std_Lower_Teeth` | Teeth tone + `_ao` |
| `Std_Tongue` | Tongue/pink tone |
| `Std_Nails` | Nail tone |
| `Std_Eyelash` | Dark near-black |
| `Std_Tearline_{L,R}` | Near-transparent wet tone |

**Skin-tone differentiation between characters sharing a mesh**: rather than
leave Jonah/Noah (same mesh) or Mira/Lena (same mesh) visually identical, the
4 skin materials (`Std_Skin_{Head,Body,Arm,Leg}`) were **duplicated per
character** (not shared assets) under
`Assets/_TRLM/Materials/Characters/{Jonah,Noah,Lena}/` with a distinct
`_BaseColor` tone each, while keeping the real normal/AO maps. Elias and Mira
keep the original repaired materials (their tone is the "canonical" one from
the repair pass). This matches `AssetRegistry.md`'s previously-recommended
"Option 3: tint the existing 3 bases" mitigation — zero new asset downloads,
verified visually distinct in the validation scene (see Test Results below).

| Character | Skin tone (`_BaseColor`, linear RGB) |
|---|---|
| Elias | (0.72, 0.55, 0.46) — canonical |
| Mira | (0.72, 0.55, 0.46) — canonical |
| Jonah | (0.60, 0.45, 0.36) — darker/warmer, own material copies |
| Noah | (0.80, 0.64, 0.54) — lighter/cooler, own material copies |
| Lena | (0.78, 0.60, 0.50) — own material copies, distinct from Mira |

---

## Per-character diagnostic (final status)

| Character | Base mesh | Status | Notes |
|---|---|---|---|
| Elias | Neutral_M | **REBUILD_REQUIRED → REPAIRED** | 17/17 materials repaired; realistic skin/eye/mouth materials; real normal+AO |
| Mira | Neutral_F | **REBUILD_REQUIRED → REPAIRED** | 17/17 materials repaired (shared Neutral_F material set) |
| Jonah | CC3_Base_Plus | **REBUILD_REQUIRED → REPAIRED** | 17/17 materials repaired + 4 skin materials given a distinct per-character tint |
| Lena | Neutral_F | **REBUILD_REQUIRED → REPAIRED** | 17/17 materials repaired + 4 skin materials given a distinct per-character tint |
| Noah | CC3_Base_Plus | **REBUILD_REQUIRED → REPAIRED** | 17/17 materials repaired + 4 skin materials given a distinct per-character tint |

No character is left with the fake flat-white placeholder material. Verified
visually (see Test Results) — all 5 show real skin tone + visible normal-map
muscle/form detail under lighting, and are distinguishable from one another
by skin tone even where the underlying mesh is shared.

---

## Known limitations (honest, not worked around)

- **No hair or clothing geometry** exists on any of the 3 base meshes —
  characters are nude/bald. This is a modeling gap, not fixable by material
  work; would need new geometry (new asset sourcing or Blender modeling —
  Blender is unavailable this sprint, see `AnimationPipeline.md`).
- **Only 3 unique face/body shapes** for 5 characters — skin-tone tinting
  differentiates Jonah/Noah and Mira/Lena at a glance but they remain the
  same underlying geometry. A true fix needs either a 4th/5th base mesh
  (new sourcing, zero-budget constraint applies) or sculpting distinct
  face morphs (needs Character Creator or Blender, neither available).
- **No skin subsurface-scattering** — `_SSSMap`/`_TransMap` exist on disk but
  were not wired in, since URP/Lit has no native SSS input; a custom shader
  graph would be needed for a real subsurface skin look — out of scope for
  a material-repair pass, noted as a future quality upgrade.

---

## Prefabs produced

`Assets/_TRLM/Prefabs/Characters/PF_{Elias,Mira,Jonah,Lena,Noah}.prefab` — each
a standalone production prefab (not just a scene instance), Humanoid Avatar
configured (see `AnimationPipeline.md`), `AC_Human_Base` controller assigned.
`PF_Character_Test.prefab` (pre-existing placeholder from the P0 pass) is now
superseded by these 5 — left in place, not deleted, since it may still be
referenced elsewhere; recommend Ömer confirm it can be removed in a future
cleanup pass.
