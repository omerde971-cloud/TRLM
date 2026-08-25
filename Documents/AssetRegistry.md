# TRLM Asset Registry

Permanent record of every third-party asset evaluated, downloaded, or imported into
**The Road Leading to the Mountain (TRLM)**. Every entry that reaches
`Assets/ThirdParty/` or `Assets_Source/` MUST have an entry here before it is
considered usable in production.

Status legend: `SHORTLISTED` / `DOWNLOADED` / `IMPORTED` / `REJECTED` / `LICENSE_UNVERIFIED` / `USER_ACTION_REQUIRED`

---

## World & Level Design Sprint 02 — Local Asset Packs (2026-08-22)

Two packs supplied directly by Ömer (already downloaded to his machine, not sourced by
Claude) for production forest/rock dressing, per explicit sprint instruction to use them.

### Low Poly Forest Tree Pack

**Category:** Environment / Vegetation
**Source:** Local file, provided by Ömer — `C:\Users\ömer\Downloads\low-poly-forest-tree-pack.zip`
**Original Author:** **99 Mil** (confirmed by Ömer, 2026-08-22)
**License:** Commercial use confirmed open by Ömer; **attribution required** — creator asked
to be credited by name. Must appear in the game's end credits as the tree asset author.
**Commercial Use:** YES
**Attribution Required:** YES — credit "99 Mil" (trees) in end-of-game credits
**Downloaded:** Pre-existing on disk, provided by user
**Original File Format:** Nested RAR containing `Tree_Pack.fbx`/`.obj`/`.blend` + `textures/`
**Unity Destination:** `Assets/ThirdParty/Environment/TreePack/` (FBX + textures)
**Source archive preserved at:** `Assets_Source/Environment/TreePack/` (original zip contents + RAR extraction)
**Triangle Count:** 6–366 tris per part (very light — trunks, branch clusters, background billboard cards, bonus rock meshes)
**Texture Resolution:** Up to 2048×2048 (diffuse/normal/roughness/opacity per part)
**Rigged:** N/A (static vegetation)
**LOD Included:** Effectively yes via design — separate "background atlas" low-detail billboard cards vs. full trunk+branch combos serve as a manual two-tier LOD, though no formal Unity LODGroup was set up this pass.
**Optimization Required:** Formal LODGroup setup deferred; opacity maps were manually baked into diffuse alpha channels (see Notes) since the shipped format used separate diffuse+opacity textures, which URP/Lit cannot read as two textures for alpha clip.
**Notes:** Built into 4 combined "full tree" prefabs (trunk+branches) and 5 background-card prefabs under `Assets/_TRLM/Prefabs/Environment/`. Replaced ~520 placeholder capsule trees across the island's Coastal/Deep Forest/Rock-Belt-fringe with real instances. **Import scale bug found and fixed**: the FBX's per-part `localPosition` values encode original DCC-scene layout offsets, not valid combine-offsets — building prefabs using each part's original local position produced world-space-offset garbage (visible as tiny/misplaced geometry). Fix: reset position/rotation to zero and keep only the part's own `localScale` (100,100,100), which is the FBX's real unit-scale compensation.

### Free Pack — Rocks Stylized

**Category:** Environment / Rocks
**Source:** Local file, provided by Ömer — `C:\Users\ömer\Downloads\free-pack-rocks-stylized.zip`
**Original Author:** **PolyOne Studio** (confirmed by Ömer, 2026-08-22)
**License:** Commercial use confirmed open by Ömer; **attribution required** — creator asked
to be credited by name. Must appear in the game's end credits as the rock asset author.
**Commercial Use:** YES
**Attribution Required:** YES — credit "PolyOne Studio" (rocks) in end-of-game credits
**Downloaded:** Pre-existing on disk, provided by user
**Original File Format:** `Free Pack - Rocks Stylized.fbx` + single diffuse texture
**Unity Destination:** `Assets/ThirdParty/Environment/RockPack/`
**Source archive preserved at:** `Assets_Source/Environment/RockPack/`
**Triangle Count:** 36–1,438 tris across 11 distinct rock meshes (good variety, no repeats needed)
**Texture Resolution:** 2048×2048 single shared diffuse
**Rigged:** N/A
**LOD Included:** No — flagged for Codex
**Optimization Required:** No formal LOD; low enough poly count for a blockout pass
**Notes:** Built into 11 individual prefabs under `Assets/_TRLM/Prefabs/Environment/PF_Rock_Stylized_01..11.prefab`. Replaced ~213 placeholder cube rocks across Rock Belt/Mountain Pass/Summit/coastal clusters. Colliders added (MeshCollider) to every placed instance for player/rockfall physics interaction. 4 of the 11 variants are also reused (with a runtime-added convex MeshCollider) as the pooled rockfall projectiles in `RockfallZone` — see WorldDesign.md.

### Grass Patches (Circle)

**Category:** Environment / Ground Cover
**Source:** Local file, provided by Ömer — `C:\Users\ömer\Downloads\grass-patches-circle.zip`
**Original Author:** **brandon_grey** (confirmed by Ömer, 2026-08-22)
**License:** Commercial use confirmed open by Ömer; **attribution required** — creator asked
to be credited by name. Must appear in the game's end credits as the grass asset author.
**Commercial Use:** YES
**Attribution Required:** YES — credit "brandon_grey" (grass) in end-of-game credits
**Downloaded:** Pre-existing on disk, provided by user
**Original File Format:** `GrassPatch_Circle.fbx` + diffuse/normal textures
**Unity Destination:** `Assets/ThirdParty/Environment/GrassPack/`
**Source archive preserved at:** `Assets_Source/Environment/GrassPack/`
**Triangle Count:** 204,029 tris (single dense multi-blade clump mesh — this is one geometry-heavy "patch" object, not many separate low-poly blades)
**Texture Resolution:** Diffuse (with alpha channel for cutout) + normal map, resolution per source files
**Rigged:** N/A
**LOD Included:** No
**Optimization Required:** Flagged for Codex — a single patch at ~204K tris is heavy if scattered in large numbers; current placement (40 patches around the settlement house yard) keeps total impact modest, but this should not be scattered across large open areas without a decimated/LOD version.
**Import scale bug found and fixed:** same recurring pattern as the tree/rock packs — raw FBX import produced a ~20m-tall patch; corrected via `ModelImporter.globalScale = 0.12`, giving a realistic ~1.3×0.35×2.45m clump.
**Notes:** Built into `Assets/_TRLM/Prefabs/Environment/PF_GrassPatch_Circle.prefab`. **Replaced** 158 low-poly grass tuft objects (8 tris each) that were baked into the `Abandoned_House.fbx` model itself (`Settlement_MainHouse/grass/Grass..Grass.157`) with 40 real grass patch instances scattered around the same yard footprint — per Ömer's explicit instruction to remove the house's built-in grass and use this asset instead.

---

## Attribution / End-Credits Requirement

Three local asset packs used in TRLM require **on-screen credit** per their creators' terms
(commercial use is confirmed open, but attribution was explicitly requested). These must be
added to the game's end-of-game credits screen before commercial release:

| Asset | Creator | Category |
|---|---|---|
| Low Poly Forest Tree Pack | **99 Mil** | Trees |
| Free Pack — Rocks Stylized | **PolyOne Studio** | Rocks |
| Grass Patches (Circle) | **brandon_grey** | Grass |

This list will grow as more named creators are confirmed — do not remove entries, only add.

---

## P0 Vertical Slice — Status Overview

| # | Category | Status |
|---|----------|--------|
| 1 | Old Wooden Rowboat | **IMPORTED + VALIDATED** — Marpa Studio boat, URP materials fixed → `Assets/_TRLM/Prefabs/Vehicles/PF_Rowboat.prefab` |
| 2 | Ocean / Water Solution | **IMPORTED + VALIDATED** — Uber Stylized Water (MIT) — PASS WITH LIMITATIONS |
| 3 | Forest Environment | **BLOCKED** — .blend pack cannot import (Blender not installed on this machine), 0 usable GameObjects |
| 4 | Terrain Materials | **IMPORTED + VALIDATED** — 5 CC0 surfaces from Poly Haven + ambientCG, URP materials authored |
| 5 | Abandoned House | **IMPORTED + VALIDATED** — scale bug fixed, URP OK → `Assets/_TRLM/Prefabs/Buildings/PF_AbandonedHouse.prefab` |
| 6 | Main Character (Elias Ward) | **MATERIAL REPAIRED (Sprint 04)** — Humanoid rig configured, skin/eye/mouth materials rebuilt with real normal+AO maps since no diffuse texture exists in the source export → `Assets/_TRLM/Prefabs/Characters/PF_Elias.prefab`. See `CharacterMaterialAudit.md`. |
| 7 | Companion Characters (Mira, Jonah, Lena, Noah) | **MATERIAL REPAIRED (Sprint 04)** — same repair applied to all; still only 3 base meshes for 5 characters (unchanged limitation), mitigated with per-character skin-tone tinting on the 2 reused meshes → `Assets/_TRLM/Prefabs/Characters/PF_{Mira,Jonah,Lena,Noah}.prefab`. See `CharacterMaterialAudit.md`. |
| 8 | Wolf | **MATERIAL REPAIRED, SUBSTITUTE ANIMATION ADDED (Sprint 04)** — two-tone coloration replaces flat grey; still 0 real rig (Blender unavailable, confirmed hard blocker), added a root-transform idle sway as an honest interim substitute → `Assets/_TRLM/Prefabs/Animals/PF_Wolf.prefab`. See `CreatureMaterialAudit.md` / `AnimationPipeline.md`. |
| 9 | Bear / Wild Boar / Snake / Mountain Goat | **NO_ASSET (confirmed, Sprint 04)** — zero 3D models exist for any of these 4 species; data-only `WildlifeSpeciesProfile` assets exist but `animalPrefab = null`. Needs a new sourcing pass. See `CreatureMaterialAudit.md`. |
| 10 | Weapons (Pistol, Shotgun, Knife) | **NO_ASSET (confirmed, Sprint 07)** — `Assets/ThirdParty/Weapons/` is empty; zero 3D weapon models exist anywhere in the project. Fully mechanically functional weapons built using primitive placeholder geometry (`DEV_Placeholder_*` convention) — see `CombatSystem.md`. Needs a zero-cost sourcing pass, same shape as the P0 pass for characters/environment. |

**Budget directive (2026-08-22): Ömer confirmed ZERO budget — no paid assets to be purchased.** All P0 categories re-researched for free-only alternatives. Paid candidates from the first pass remain documented below for future reference (e.g. if budget opens up later) but are NOT to be bought.

**Phase 2 — Production Prep (2026-08-22):** Full technical audit + Unity integration pass completed. See `Documents/P0_Asset_Audit.md` for full per-asset technical detail (import status, materials, colliders, triangle counts, rig/animation status). Test scene: `Assets/_TRLM/Scenes/Tests/AssetTest.unity`. Production prefabs: `Assets/_TRLM/Prefabs/`. Summary of fixes applied this pass:
- **Rowboat & Wolf:** materials were `Standard` shader (renders PINK under this project's pure-URP pipeline) — converted in place to `Universal Render Pipeline/Lit`.
- **Wolf & Abandoned House:** both had broken import scale (wolf was ~7m tall, house was ~558m across) — fixed via `ModelImporter.globalScale` (0.15 and 0.2 respectively).
- **Ocean:** `Water Template Clear.prefab` had a null material reference (broken link from the GitHub clone) — reassigned.
- **Terrain:** authored 5 new URP/Lit materials from the raw CC0 textures (none existed before).
- **Forest:** confirmed BLOCKED — needs Blender installed to convert the .blend source pack to FBX.
- **Elias/Companions:** confirmed the free Reallusion bases have zero identity-shaping blend shapes (only facial-animation/viseme shapes) and no diffuse skin texture in the export — 5 distinct characters are NOT achievable from this asset alone without Reallusion's Unity plugin or new sourcing.

---

## 1. Old Wooden Rowboat

### Candidate A — Wooden row boat - Game Asset — **IMPORTED**
**Category:** Prop / Vehicle
**Source:** Unity Asset Store · Publisher: Marpa Studio
**Original URL:** https://assetstore.unity.com/packages/3d/vehicles/sea/wooden-row-boat-game-asset-304388
**License:** Standard Unity Asset Store EULA
**Commercial Use:** YES
**Attribution Required:** NO
**Downloaded:** YES (Ömer claimed via Unity account, 2026-08-22)
**Original File Format:** Unity package (FBX mesh + Built-In/URP/HDRP material variants + prefabs)
**Unity Destination:** `Assets/ThirdParty/Props/Boat/Marpa Studio/` (moved from project root import location to match folder convention)
**Triangle Count:** Not disclosed by publisher — verify in Unity Editor
**Texture Resolution:** Albedo/AO/Height/Metallic/Normal maps present (resolution not specified)
**Rigged:** N/A
**Animations Included:** N/A
**LOD Included:** Unknown — verify after import
**Optimization Required:** Verify in test scene
**Notes:** Includes separate `BoatWood` (plain wood) and `BoatPaint` (painted) prefab variants, with Built-In, URP, and HDRP material sets. URP variant should be used for TRLM. Not yet tested in a scene.

### Candidate B — Wooden Boat | Lowpoly Free (SHORTLIST)
**Source:** Sketchfab · https://sketchfab.com/3d-models/wooden-boat-lowpoly-free-a430d37b027b4185bc0d191dac56b816
**License:** CC-BY (attribution required)
**Commercial Use:** YES
**Attribution Required:** YES
**Downloaded:** NO — Sketchfab downloads require a logged-in account
**Notes:** 3,200 tris, 4K PBR. Good backup if Candidate A doesn't fit; requires attribution credit in-game/credits screen.

### Candidate C — "Stylized Wooden Rowboat & Oars" (itch.io)
**Source:** https://zfrdigitalarts.itch.io/stylized-wooden-rowboat-free
**License:** `LICENSE_UNVERIFIED` — page has no explicit commercial-use statement
**Commercial Use:** UNCLEAR
**Downloaded:** NO
**Notes:** Has separate animatable oars (valuable), but do not use until creator confirms commercial license in writing.

---

## 2. Ocean / Water Solution — IMPORTED

**Asset Name:** Uber Stylized Water
**Category:** Environment / Shader (Ocean-Water)
**Source:** GitHub
**Original Author:** MatrixRex (and bundled MIT deps by Cyanilux: ShaderGraphVariables, URP_ShaderGraphCustomLighting)
**Original URL:** https://github.com/MatrixRex/Uber-Stylized-Water
**License:** MIT (verified directly in repo `LICENSE` file; bundled third-party sub-packages also confirmed MIT)
**Commercial Use:** YES
**Attribution Required:** NO (MIT — notice must be preserved in source, not in-game)
**Downloaded:** YES — `git clone` (no login/purchase needed)
**Original File Format:** Unity Shader Graph package (.shadergraph, .shadersubgraph, prefabs, materials, demo scene)
**Unity Destination:** `Assets/ThirdParty/Environment/Water/Uber Stylized Water/`
**Source archive preserved at:** `Assets_Source/Ocean/Uber-Stylized-Water/` (full cloned repo incl. demo scene, docs)
**Triangle Count:** N/A (shader, not mesh)
**Texture Resolution:** Foam/noise/caustic textures included, resolution not specified by author (small utility textures)
**Rigged:** N/A
**Animations Included:** N/A
**LOD Included:** N/A
**Optimization Required:** `OPTIMIZATION_REQUIRED` — evaluate performance on target hardware; this is a stylized wave shader (Gerstner-based), not a true FFT ocean. Shoreline foam, planar reflection, and 7 pre-built water template materials (Tropical/Clear/Murky/Wavy/OldSchool/Anime/Genshin) are included — pick/tune one for TRLM's realistic tone (Clear or Murky templates are the most realistic starting points).
**Notes:** No rain-compatibility system built in — will need a separate wet-surface/rain shader layered on top later. Not yet wired into a scene or tested in-editor; next step is to open a test scene and validate visually before use in production maps.

---

## 3. Forest Environment

### Candidate A — Forest Environment: Dynamic Nature (SHORTLIST / USER_ACTION_REQUIRED)
**Source:** Unity Asset Store · https://assetstore.unity.com/packages/3d/vegetation/forest-environment-dynamic-nature-150668
**License:** Standard Unity Asset Store EULA
**Commercial Use:** YES
**Downloaded:** NO — paid asset, requires purchase
**Technical:** 100% photo-scanned; full LOD + GPU instancing; URP/HDRP/Built-in; ~3.4 GB; Unity Awards 2019 winner
**Notes:** Strongest recommendation — cohesive photo-scanned ecosystem (trees, bushes, grass, rocks, mushrooms). **USER_ACTION_REQUIRED**: needs purchase on Unity Asset Store (price not confirmed by agent — check listing).

### Candidate B — Realistic Forest Asset Pack (SHORTLIST)
**Source:** https://assetstore.unity.com/packages/3d/vegetation/realistic-forest-asset-pack-282815
**License:** Standard Unity Asset Store EULA · **Commercial Use:** YES · **Cost:** $20
**Notes:** Smaller/cheaper, lacks ground-cover ecosystem completeness — good budget fallback.

### Candidate C — Real Landscapes: Valley Forest (SHORTLIST)
**Source:** https://assetstore.unity.com/packages/3d/environments/landscapes/real-landscapes-valley-forest-194338
**License:** Standard Unity Asset Store EULA · **Commercial Use:** YES · **Cost:** $69 (seen on sale $20.70)
**Notes:** 170+ prefabs, includes full terrain + snow system; autumn/winter-leaning; good if seasonal variation wanted.

### Candidate D — Poly Haven individual tree/grass models (SHORTLIST)
**Source:** https://polyhaven.com/a/pine_tree_01, /a/fir_tree_01, /models/nature
**License:** CC0 · **Commercial Use:** YES · **Downloaded:** NO
**Notes:** Free and CC0, but no pre-built pack — would require manual ecosystem assembly (high triangle counts, needs LOD work). Good filler/supplement source, not a full solution.

---

## 4. Terrain Materials — IMPORTED (5/5 surfaces, all CC0)

All five downloaded directly (no login/account required) at 2K resolution (Diffuse/Albedo, Normal-GL, Roughness, AO maps).

| Surface | Asset | Source | License | Unity Destination |
|---|---|---|---|---|
| Forest dirt | Forest Floor | Poly Haven (polyhaven.com/a/forest_floor) | CC0 | `Assets/ThirdParty/Environment/Terrain/forest_dirt/` |
| Mud | Mud Forest | Poly Haven (polyhaven.com/a/mud_forest) | CC0 | `Assets/ThirdParty/Environment/Terrain/mud/` |
| Grass | Grass001 | ambientCG (ambientcg.com/view?id=Grass001) | CC0 | `Assets/ThirdParty/Environment/Terrain/grass/` |
| Rock (+ moss/grass blend) | Aerial Grass Rock | Poly Haven (polyhaven.com/a/aerial_grass_rock) | CC0 | `Assets/ThirdParty/Environment/Terrain/rock_grass/` |
| Wet ground | Brown Mud 02 | Poly Haven (polyhaven.com/a/brown_mud_02) | CC0 | `Assets/ThirdParty/Environment/Terrain/wet_ground/` |
| Bonus (extra dirt variant) | Dirt Floor | Poly Haven (polyhaven.com/a/dirt_floor) | CC0 | `Assets/ThirdParty/Environment/Terrain/dirt_floor/` |

**Commercial Use:** YES (all CC0 / public domain — no attribution required, no restrictions)
**Attribution Required:** NO
**Original File Format:** JPG (Diffuse, Normal-GL, Roughness, AO; ambientCG set also includes Displacement)
**Source archives preserved at:** `Assets_Source/Terrain/<slug>/` and `Assets_Source/Terrain/grass_ambientcg/`
**Optimization Required:** NO — already 2K, appropriately sized for terrain use
**Notes:** Not yet wired into Unity Terrain Layers or a URP/Lit material — next step is creating `TerrainLayer` assets and/or PBR materials from these maps, and setting correct texture import settings (sRGB for diffuse/AO off, linear + normal-map flag for normal maps).

---

## 5. Abandoned House

### Candidate A — Abandoned House - Full Version (SHORTLIST / USER_ACTION_REQUIRED)
**Source:** Unity Asset Store · https://assetstore.unity.com/packages/3d/environments/urban/abandoned-house-full-version-182010
**Publisher:** VIS Games
**License:** Standard Unity Asset Store EULA (Single Entity) · **Commercial Use:** YES
**Technical:** ~5.0 GB, Built-in/URP/HDRP, extensive multi-room interior, modular doors/windows/furniture
**Downloaded:** NO — paid, requires purchase
**Notes:** Strongest fit for the first loot/safe location. **USER_ACTION_REQUIRED**: needs Ömer's Asset Store purchase.

### Candidate B — 3D Realistic Old Abandoned House Interior Asset Pack (SHORTLIST)
**Source:** Fab · https://www.fab.com/listings/8a3b3f7c-497a-41fb-90f9-8c05f8e32f37
**License:** Fab commercial license (tier selected at checkout) · **Commercial Use:** YES
**Notes:** Highly modular, FBX+Blender; full spec sheet could not be verified (fetch blocked) — inspect on Fab before buying.

### Candidate C — Old | House | Wooden | Enterable | Rusty (SHORTLIST)
**Source:** Sketchfab · https://sketchfab.com/3d-models/old-house-wooden-enterable-rusty-7f496b3002d24341a618408cb8221e8c
**License:** CC-BY (attribution to YadroGames required) · **Commercial Use:** YES
**Technical:** 24.2k tris, PBR, enterable but smaller/sparser interior — best as a secondary location, not primary.

### Candidate D — Abandoned House (With Exterior, Interior And Adjustable Furniture) (SHORTLIST — budget option)
**Source:** https://assetstore.unity.com/packages/3d/props/interior/abandoned-house-with-exterior-interior-and-adjustable-furniture-257467
**License:** Standard Unity Asset Store EULA · **Commercial Use:** YES
**Technical:** 687 MB (much lighter than Candidate A), bedroom/bathroom/kitchen, adjustable furniture, no confirmed URP support (older asset — verify before buying).

---

## 6. Main Character — Elias Ward (SHORTLIST — awaiting Ömer's decision, do NOT auto-select)

### 1st choice — Realistic Modern Character Pack
**Source:** Unity Asset Store · https://assetstore.unity.com/packages/3d/characters/realistic-modern-character-pack-329789
**License:** Standard Unity Asset Store EULA · **Commercial Use:** YES · **Cost:** $29.99
**Technical:** 11 characters, FBX, Humanoid/Mecanim rig, modern casual clothing (fits survival tone), URP/HDRP/Built-in confirmed, released Feb 2026 (newest/most Unity-6-native of the three)
**Pros:** Cheapest, newest, explicit URP support, includes NPC variety from same pack
**Cons:** Polycount/texture resolution not publicly disclosed — needs visual preview before commit

### 2nd choice — Real Human Player 02
**Source:** https://assetstore.unity.com/packages/3d/characters/humanoids/humans/real-human-player-02-162313
**Publisher:** Pyramis Arts · **License:** Standard Unity Asset Store EULA · **Commercial Use:** YES · **Cost:** $60
**Technical:** Established publisher, high file size (649.7 MB) suggests hero-grade face/hand detail; from 2020 — pipeline support (URP) unconfirmed, may need testing/LOD work.

### 3rd choice — Real People: Males
**Source:** https://assetstore.unity.com/packages/3d/characters/humanoids/humans/real-people-males-3866
**Publisher:** 3DRT (established studio) · **License:** Standard Unity Asset Store EULA · **Commercial Use:** YES · **Cost:** $49
**Technical:** 3 base body types, LOD system (1600/500 tris), 20+ skin textures, but base-mesh/nude — needs custom clothing added. Best as an NPC-army fallback rather than a hero-ready pack.

**Action needed from Ömer:** preview all three on the Asset Store (video/character previews), confirm which male model best matches Elias Ward's intended look, then approve one for purchase + import.

---

## 7. Companion Characters — Mira, Jonah, Lena, Noah (SHORTLIST — awaiting Ömer's decision)

### Recommended primary — ActorCore (Reallusion)
**Source:** https://actorcore.reallusion.com/ (+ free Auto Setup for Unity plugin)
**License:** Reallusion Content EULA (royalty-free; free mass-distribution license required for games — apply separately)
**Commercial Use:** YES (pending free license application)
**Notes:** Photogrammetry-scanned + Character-Creator-built realistic humans, 100+ library, guaranteed rig/style consistency, full facial blendshapes, URP/HDRP-safe export. Best realism match for TRLM. Could also source Elias Ward from the same library for full-cast consistency (bonus, not required).
**Action needed:** verify 4 suitable character archetypes exist in the current library, and apply for Reallusion's free commercial mass-distribution license before use.

### Budget alternative — City People Mega-Pack
**Source:** Fab / Unity Asset Store · https://assetstore.unity.com/packages/3d/characters/city-people-mega-pack-203329
**License:** Fab / Unity Asset Store commercial license · **Commercial Use:** YES · **Cost:** ~$25–40
**Notes:** 118 characters, mixed ages/genders, includes 22 animations, Biped rig — lower-poly/stylized-realistic hybrid rather than photoreal. Good value, consistent style, easy to pick 4 (+Elias) from one pack.

### Rejected — Male Mega Realistic Character Pack 01
**Source:** Fab · **Reason:** male-only, cannot cover Mira/Lena (female companions) alone.

**Action needed from Ömer:** choose ActorCore (higher realism, license paperwork) vs. City People Mega-Pack (cheaper, faster, less photoreal) as the companion-character pipeline.

---

## 8. Wolf

### 1st choice — Wolf Realistic (SHORTLIST / USER_ACTION_REQUIRED)
**Source:** Unity Asset Store · https://assetstore.unity.com/packages/3d/characters/animals/mammals/wolf-realistic-135224
**Publisher:** Red Deer · **License:** Standard Unity Asset Store EULA · **Commercial Use:** YES · **Cost:** $30
**Technical:** 57-bone rig, 4-level LOD (12,700 → 2,250 tris), 4K textures, 3 color variants, URP-compatible
**Notes:** Best balance of realism, performance (multi-wolf scenes), and price. **USER_ACTION_REQUIRED** — needs Asset Store purchase.

### 2nd choice — Realistic Wolf (MalberS Animations)
**Source:** https://assetstore.unity.com/packages/3d/characters/animals/realistic-wolf-190336
**License:** Standard Unity Asset Store EULA · **Commercial Use:** YES (excludes NFT/blockchain/metaverse/3D-print-for-sale use) · **Cost:** $69.99 + $49.99 required "Animal Controller" dependency ($119.98 total)
**Technical:** 100+ AAA-quality animations, 4K textures, cub variant included; heavier — no confirmed LOD system, likely too costly per-wolf for multiple simultaneous instances.
**Notes:** Premium option for hero/cinematic wolf shots only, not for pack encounters.

### Rejected — Stylized Wolf 3D Model (asset 314033)
**Reason:** Built-in render pipeline only — incompatible with TRLM's URP pipeline.

---

## FREE-ONLY Re-Research (2026-08-22)

### 1. Rowboat — free candidate unchanged
Already free: **"Wooden row boat - Game Asset"** (Unity Asset Store, $0, standard EULA) — see Section 1 above. Still `USER_ACTION_REQUIRED` (needs one-time free claim via Unity account, no payment).

### 2. Ocean — unchanged
Already free/MIT and already imported (Section 2).

### 3. Forest Environment — IMPORTED (CC0)
**Asset Name:** Free Vegetation Asset Pack
**Source:** OpenGameArt.org · https://opengameart.org/content/free-vegetation-asset-pack
**License:** CC0 · **Commercial Use:** YES · **Attribution:** NO
**Downloaded:** YES (direct download, no login) → `Assets_Source/Forest/free_vegetation_pack.zip`
**Unity Destination:** `Assets/ThirdParty/Environment/Forest_CC0/`
**Original File Format:** Blender source (.blend) + separate textures (bark, branches, leaves, mushrooms) — NOT pre-exported to FBX
**Rigged:** N/A · **LOD Included:** YES (multiple LOD tiers per model per source docs)
**Optimization Required:** `OPTIMIZATION_REQUIRED` — small pack (~5-6 base models: tree w/ and w/o leaves, saplings, branches, bush, mushrooms, cut trunk). Needs Blender export to FBX (or Unity's built-in .blend importer, which requires Blender installed on this machine) before use, and needs supplementing — this alone is not a full forest (no ground-cover grass/branches variety at the density a paid pack would give).
**Notes:** Secondary free option surfaced but not downloaded — **Poly Haven individual tree models** (CC0, polyhaven.com/models/nature/trees) can supplement this pack with higher-detail hero trees; recommend adding 5-10 of those by hand later for visual variety. **Honest quality note:** visibly lower density/variety than the paid "Forest Environment: Dynamic Nature" pack ($ — not purchased per zero-budget directive).

### 4. Terrain Materials — unchanged
Already free/CC0 and already imported (Section 4).

### 5. Abandoned House — **IMPORTED**
**Asset Name:** Abandoned House Asset Pack
**Source:** itch.io · https://elbolilloduro.itch.io/abandoned-house · Author: Elbolilloduro
**License:** CC0 · **Commercial Use:** YES · **Attribution Required:** NO · **Cost:** $0
**Downloaded:** YES (Ömer, 2026-08-22) → `Assets_Source/Buildings/AbandonedHouse/Abandoned_House.rar`
**Unity Destination:** `Assets/ThirdParty/Environment/AbandonedHouse/` (Models/Abandoned_House.fbx + Textures/ — 77 texture files)
**Original File Format:** FBX, DAE, GLB, Blend (all included in archive; FBX imported)
**Technical:** Low-poly atmospheric style (NOT photoreal — trade-off vs. the paid VIS Games pick), full interior (bedroom/kitchen/dining/living), includes van + street props
**Rigged:** N/A · **LOD Included:** Unknown, verify
**Optimization Required:** Verify material/shader setup under URP after import — textures are loose PNG/JPG files, need proper Unity import settings (sRGB, compression) and URP/Lit material assignment; original materials from the FBX may reference Built-in shaders and need reassigning.
**Runner-up (rejected style-wise):** Sketchfab "Abandoned House" by Sengchor (CC-BY, 12.2k tris — too low-poly, sparse) and "Oldhouse (CC0)" (mislabeled, actually CC-BY, 372k tris but interior access unconfirmed).

### 6. Main Character (Elias Ward) — **IMPORTED** (base mesh only, see honest caveat below)
**Asset Name:** Reallusion Character Creator — Free 3D Character Base
**Source:** https://www.reallusion.com/character-creator/free-3d-character-base.html
**License:** Reallusion free base-model license · **Commercial Use:** YES (explicit: filmmaking/animation/gaming) · **Attribution Required:** NO · **Cost:** $0
**Downloaded:** YES (Ömer, free account, 2026-08-22) → `Assets_Source/Characters/CC_character_base/CC_character_base.zip` (670 MB, all 5 bases + Read Me.txt)
**Unity Destination:** `Assets/ThirdParty/Characters/CC_Base/03_Neutral_M/` (realistic male base — proposed for Elias Ward)
**Original File Format:** FBX + textures (also OBJ, ZTL, Topology Maps included in the source zip, not copied into Unity)
**Rigged:** YES — full skeletal rig, 150+ facial morphs · **Texture Resolution:** high-res (per source docs, exact px not stated)
**Clothing:** NONE — base mesh is nude; needs clothing sourced separately (free Character Creator trial's 390+ clothing items, or dressed manually in Unity/Blender)
**Optimization Required:** Verify Unity Humanoid rig auto-detection on import; may need Avatar configuration pass.
**Honest caveat:** Only **3 of the 5** free bases are realistic-style (`01_CC3_Base_Plus`, `02_Neutral_F`, `03_Neutral_M` — imported); the other 2 are Toon variants and were excluded as off-brand for TRLM. This means there is only **one realistic male base and one realistic female base** — not enough distinct faces for Elias + 4 companions without real customization work in Reallusion's Character Creator software (which requires either the paid app or its limited free trial) to differentiate Mira/Jonah/Lena/Noah's appearances. Right now, any two male characters (e.g. Elias + Jonah) would look identical if both use `03_Neutral_M` untouched. Flagging this now rather than after the fact — see Section 7 for the same constraint on companions.

### 7. Companion Characters — **PARTIALLY IMPORTED**, gap identified
**Source:** same Reallusion download as Section 6 → `Assets_Source/Characters/CC_character_base/`
**Unity Destination:** `Assets/ThirdParty/Characters/CC_Base/01_CC3_Base_Plus/` and `02_Neutral_F/` (realistic bases available for reuse as companions)
**License/Commercial Use/Cost:** same as Section 6 — YES / $0
**Gap:** Only 3 realistic bases exist total (1 male, 1 female, 1 base/neutral) — **not enough for 4 distinct companions + Elias (5 needed) without visual customization.** Reusing `03_Neutral_M` for both Elias and a male companion (e.g. Jonah) would make them look identical out of the box.
**Recommended next step (needs Ömer's decision):**
1. **Mixamo** (mixamo.com, free Adobe account, large free character library, commercial use confirmed) — pull 2-3 additional free realistic humans to fill out the roster; requires manual curation for style consistency since the library isn't uniform.
2. **Manual customization** — if Ömer or someone on the team has access to Reallusion's free Character Creator trial, the same 3 base meshes can be recolored/reshaped into distinct-looking Mira/Jonah/Lena/Noah without buying new assets.
3. **Accept visual reuse** — use texture/material tinting (skin tone, hair color swap) on the existing 3 bases as a cheap way to differentiate characters without new downloads.
**Rejected:** Vitruvian Project (CC0, GitHub — promising license but couldn't verify character count/rig via fetch), Sketchfab CC0 Character Kit (single kit, not 4 distinct people), OpenGameArt CC0 Humans (low-poly/inconsistent).

### 8. Wolf — IMPORTED (CC0, but needs rigging work)
**Asset Name:** Wolf (OpenGameArt CC0)
**Source:** OpenGameArt.org · https://opengameart.org/content/wolf-2
**License:** CC0 · **Commercial Use:** YES · **Attribution:** NO
**Downloaded:** YES (direct download, no login) → `Assets_Source/Animals/Wolf/Wolf_0.zip`
**Unity Destination:** `Assets/ThirdParty/Animals/Wolf_CC0/`
**Original File Format:** Collada (.dae)
**Rigged:** **NO** · **Animations Included:** **NONE**
**Optimization Required:** `OPTIMIZATION_REQUIRED` — realistic proportions and clean topology per creator, but this is a bare mesh only. Needs full rig + animation pipeline (idle/walk/run/attack/hit/death) built from scratch or via a rigging tool (e.g. Blender + Rigify, or Mixamo auto-rigger if topology qualifies) before it's usable in-game.
**Honest note:** every other free wolf candidate found (Sketchfab "Wolf with Animations" by 3DHaupt = non-commercial only; several other Sketchfab wolves = license unverified) was rejected — this CC0 mesh was the only one with an unambiguous commercial license. The paid pick, "Wolf Realistic" by Red Deer ($30, rigged + 4 LODs + animations), remains documented in Section 8 above as the fast-path option if budget ever opens.

---

## Summary of Immediate Actions Needed from Ömer

**All P0 acquisition targets are now downloaded and imported — zero budget spent.** (2026-08-22)

| # | Category | Status |
|---|---|---|
| 1 | Rowboat | ✅ Imported — `Assets/ThirdParty/Props/Boat/` |
| 2 | Ocean/Water | ✅ Imported — `Assets/ThirdParty/Environment/Water/` |
| 3 | Forest | ✅ Imported (base tier) — `Assets/ThirdParty/Environment/Forest_CC0/` |
| 4 | Terrain Materials | ✅ Imported — `Assets/ThirdParty/Environment/Terrain/` |
| 5 | Abandoned House | ✅ Imported — `Assets/ThirdParty/Environment/AbandonedHouse/` |
| 6 | Elias Ward | ✅ Base mesh imported — `Assets/ThirdParty/Characters/CC_Base/03_Neutral_M/` |
| 7 | Companions | ⚠️ Partial — only 3 realistic bases exist total, not enough for 4 distinct companions (see Section 7 gap + options) |
| 8 | Wolf | ✅ Imported (unrigged) — `Assets/ThirdParty/Animals/Wolf_CC0/` |

**Remaining production work (not acquisition, follow-up tasks):**
- Wolf mesh needs rigging + animations built from scratch (Blender/Rigify or Mixamo auto-rigger).
- Forest pack needs Blender→FBX export and supplementing with more variety (e.g. hand-picked free Poly Haven trees).
- Abandoned House materials need URP/Lit reassignment (original FBX materials may reference Built-in shaders).
- Companion roster needs a decision from Ömer (Mixamo supplement vs. manual customization vs. texture-tint reuse — see Section 7).
- None of the imported assets have been tested in a scene yet — next step is building `Assets/_TRLM/Scenes/Tests/AssetTest.unity` to verify scale, materials, and shader compatibility per the mission's testing requirement.
