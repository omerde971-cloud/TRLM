# TRLM P0 Asset Audit

Technical validation pass on all P0 vertical-slice assets, performed inside the Unity Editor
(6000.4.8f1, URP) against `Assets/_TRLM/Scenes/Tests/AssetTest.unity`. Companion document to
`Documents/AssetRegistry.md`.

Date: 2026-08-22

---

## Summary Table

| Asset | Import OK | URP Material | Pink Mat | Scale | Collider | Tex Res | # Materials | Triangles | LOD | Rig | Animations | Optimization Needed |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Rowboat | YES | FIXED (was Standard, converted to URP/Lit) | Fixed (was pink-risk) | OK (1.49×0.91×4.08 m) | Added (BoxCollider) | 2K | 1 | 2,950 | No | N/A | N/A | No |
| Ocean/Water | YES | OK (custom URP shader, compiles clean) | No | N/A (plane) | None (not needed) | 1K (fx textures) | 1 per template (7 templates) | N/A (procedural surface) | N/A | N/A | N/A | Yes — see notes |
| Forest Vegetation | **BLOCKED** | N/A | N/A | N/A | N/A | N/A | 0 | 0 | Unknown | N/A | N/A | Yes — critical |
| Terrain Materials | YES | OK (5 new URP/Lit materials created) | No | N/A | N/A | 2K | 5 (newly authored) | N/A | N/A | N/A | N/A | No |
| Abandoned House | YES | OK (already URP/Lit on import) | No | FIXED (was 557×15×557 m, now correct ~112×3×112 m incl. backdrop; house itself ~9×2.5×9 m) | Partial (floor only) | 256×256 (low) | 79 | 69,698 | No | N/A | N/A | Yes — colliders, texture res |
| Human Character Base (Elias) | YES | Present but **untextured** (flat white) | Technically not pink, but visually broken | OK (1.94 m tall) | Added (Capsule, test only) | N/A — base color textures missing from export | 17 | 35,366 (×6 skinned meshes) | N/A | Skeleton present, no Animator/Avatar configured | 0 | Yes — critical |
| Wolf | YES | FIXED (was Standard, converted to URP/Lit) | Fixed (was pink-risk) | FIXED (was 1.91×6.69×9.13 m, now 0.29×1.00×1.37 m) | Added (BoxCollider, approx) | **None — flat color only, no texture maps at all** | 2 | 852 | No | **NONE — fully static mesh, no bones** | **NONE** | Yes — critical |

---

## 1. Rowboat

- **Import:** Clean, no console errors.
- **URP material:** The Built-In prefab variant (`BoatWood.prefab`) used the `Standard` shader, which renders **pink under this project's pure-URP pipeline** (confirmed `GraphicsSettings.currentRenderPipeline` = `PC_RPAsset`, a `UniversalRenderPipelineAsset`). Fixed by converting `BoatPainted.mat`, `BoatWoodBase.mat`, and `Paddle.mat` in place to `Universal Render Pipeline/Lit`, remapping `_MainTex`→`_BaseMap` and `_Color`→`_BaseColor`. The publisher also ships a separate `BoatURP.unitypackage` (in `Marpa Studio/URP/`) with pre-made URP materials — importing it headlessly via `AssetDatabase.ImportPackage` did not produce new assets (likely needs the interactive import dialog), so the direct shader-conversion fix was used instead. **Recommend re-attempting the official URP package import from inside the Editor UI later** for a cleaner, publisher-intended result.
- **Scale:** Correct out of the box — 1.49 × 0.91 × 4.08 m, appropriate for a small rowboat.
- **Collider:** None shipped. Added a test `BoxCollider` matching the render bounds.
- **Materials:** 1 (`BoatWoodBase`), 2K albedo/AO/height/metallic/normal maps present.
- **Triangles:** 2,950 — light.
- **LOD:** None found; not critical at this triangle count.
- **Note:** A stale scene-instance bug was observed once (MeshFilter briefly lost its mesh reference after unrelated reimports elsewhere in the project) — destroying and re-instantiating from the prefab fixed it. Flagging in case it recurs; not a fault in the source asset.
- **Verdict: PASS** (after fixes).

## 2. Ocean / Water Solution (Uber Stylized Water)

- **URP compatibility:** Confirmed — custom `UberStylizedWater` shader compiles with zero errors under URP 6000.4.8f1.
- **Shoreline suitability:** Has a dedicated shoreline system (`_ENABLESHORELINE`, `_SL_*` properties: speed, thickness, dissolve, trail) — confirmed visually in the test scene (foam line along the terrain sample edge). **Disabled by default** on the "Clear" template; a new `M_Ocean_TestConfig.mat` was created (waves + shoreline enabled) without touching the original template.
- **Performance:** Single shader pass, no expensive FFT simulation (this is a Gerstner/panning-texture stylized shader, not a physically simulated ocean) — should run well on mid-range hardware. No performance profiling was run (not possible without Play Mode + a hardware target); recommend a frame-time check once wired into a real scene with the target draw distance.
- **Reflection:** Planar reflection system present and functional (`_ENABLEPLANERREFLECTION`, `PlannerReflectionVolume` prefab). Added to the test scene — requires this volume component placed in every scene that uses reflective water.
- **Wave controls:** Full 2-layer Gerstner-style wave control (length/height/speed/direction/sharpness per layer). **Disabled by default** on most templates; must be manually enabled per-material.
- **Rain compatibility:** **None built in.** No rain/wetness/ripple-on-rain system in this package. Will need a separate solution later (e.g. a puddle/rain VFX layered on top).
- **Fix applied:** The `Water Template Clear.prefab`'s `MeshRenderer` had a **null material reference** (broken link, likely lost in the GitHub clone) — reassigned `UWa-Template-Clear.mat` both on the prefab and in-scene.
- **Verdict: PASS WITH LIMITATIONS** — strong shoreline/reflection/wave feature set for a free asset, but (a) waves/shoreline are off by default and must be configured per water body, (b) no rain integration, (c) this is stylized, not a physically-simulated ocean — sufficient for a Vertical Slice, likely needs revisiting for final visual quality.

## 3. Forest Vegetation — **BLOCKED**

- **Import:** **FAILED.** The downloaded pack (`Assets_Source/Forest/free_vegetation_pack.zip`) contains only `.blend` source files + loose textures — no FBX/OBJ. Unity's console shows repeated `"Blender could not be found. Make sure that Blender is installed..."` errors. **Blender is not installed on this machine**, so Unity cannot auto-convert the `.blend` files, and `Assets/ThirdParty/Environment/Forest_CC0/` currently contains **zero usable GameObjects** (confirmed via `find_assets`).
- **Test scene:** Populated with 4 primitive-cylinder placeholders (clearly labeled `TEST_Trees_PLACEHOLDER`) since no real tree assets exist to test.
- **Impact:** This is a **critical blocker** for the forest/vegetation category — nothing from this pack can be evaluated for triangle count, texture resolution, materials, or LOD until it's converted to FBX.
- **Fix options (not executed — needs Ömer decision):**
  1. Install Blender (free) on this machine so Unity's `.blend` importer can auto-convert on the fly (simplest).
  2. Manually export `free_vegetation_pack.blend` to FBX using Blender on any machine, then re-import the FBX.
  3. Replace with a free pack that ships FBX/OBJ directly (would need new research — not done per "don't download new assets unless critical").
- **Verdict: FAIL / BLOCKED** — no forest assets currently exist in Unity.

## 4. Terrain Materials

- **Import:** All 5 texture sets (forest_dirt, mud, grass, rock_grass, wet_ground) imported cleanly as loose JPGs, 2K resolution, CC0.
- **Materials:** None existed yet — created 5 new `Universal Render Pipeline/Lit` materials (`M_forest_dirt`, `M_mud`, `M_grass`, `M_rock_grass`, `M_wet_ground`) under `Assets/ThirdParty/Environment/Terrain/Materials/`, wiring Diffuse→BaseMap, Normal→BumpMap, AO→OcclusionMap. Roughness maps exist on disk but were **not** wired (URP/Lit needs a packed mask map or a smoothness-from-roughness invert shader trick — left as flat `_Smoothness = 0.25` for now; proper roughness-map wiring is a follow-up task, e.g. via Shader Graph or a mask texture).
- **Test scene:** 5 sample planes created and visually confirmed distinct textures render correctly (see screenshot taken during audit).
- **Not yet done:** Converting these into actual Unity `TerrainLayer` assets for use on a real `Terrain` object (currently only plain quad/plane materials) — needed before this can paint an actual heightmap terrain.
- **Verdict: PASS** for material correctness; **follow-up required** to build `TerrainLayer` assets and properly wire roughness.

## 5. Abandoned House

- **Import:** Clean FBX import, 77 texture files, 79 materials — all auto-assigned `Universal Render Pipeline/Lit` shader on import (no pink materials, no manual fix needed here).
- **Scale bug found & fixed:** Original import bounds were **557.74 × 15.19 × 557.74 m** — absurd for a small house. Root cause: the model includes a large background "Vecindario" (neighborhood) set-dressing group (~558 m across) meant to be seen at a distance through windows, plus the source file's scale metadata was off by ~5×. Applied `globalScale = 0.2` on the `ModelImporter`. After the fix: overall bounds ~112×3×112 m (dominated by the now-correctly-sized backdrop), and the actual **house structure itself is ~9×2.5×9 m** — realistic for a small rural house. Individual props (e.g. a bench, "Ban") went from 6.76 m to a plausible ~1.35 m.
- **Interior:** **Confirmed real** — the FBX hierarchy includes distinct room/furniture groups (`Baño` [bathroom], `Cajones_Puertas`, `Estanterias`, `Lavanderia`, `Sillas`, `Sillones`, `Mesas`, `Cuadros`, `Colchon`, `Refrigerador`, `Lavadora`, `Retrete`, `Bañera`, TV, radio, etc.) — genuinely furnished, not just an empty shell.
- **Door:** `Puerta` exists as a **separate object** in the hierarchy (not baked into a wall), so it can be rigged for opening later — but currently has no hinge/pivot/animation, it's a static mesh only.
- **Loot placement:** Plenty of surfaces (`Mesas`, `Estanterias`, `Cajones_Puertas`, `Libreros`) suitable for loot-spawn points once the level design pass begins.
- **Collider status:** The model ships a dedicated `Colision` group, but it currently only contains a **floor collision mesh (45 tris)** — no wall or furniture collision. Added a `MeshCollider` on that floor mesh for the test scene; **walls and furniture currently have no colliders at all** — a player could walk through walls. This needs proper collision authoring (either box colliders per room or a proper collision mesh) before it's usable as a safe-house.
- **Texture resolution:** 256×256 — on the low end even for props (mission target was 1K–2K); acceptable for a background/secondary location but will look soft up close.
- **Verdict: PASS WITH LIMITATIONS** — real interior confirmed usable as a loot location once (a) wall/furniture colliders are added and (b) the door is rigged to open. Scale and material issues are now fixed.

## 6. Human Character Base (Elias Ward candidate — `03_Neutral_M`)

- **Import:** Clean, no errors. Skeleton/rig present (Reallusion CC3+ standard skeleton), 6 skinned meshes (Body, Eye×2, EyeOcclusion, TearLine, Teeth, Tongue), 444 blend shapes.
- **URP material:** Shader is correctly set to `Universal Render Pipeline/Lit` — **but every skin/body material has no `_BaseMap` texture assigned, rendering flat white** (confirmed visually in test scene screenshot). Investigated the source texture folder: Reallusion exports **do not include a plain diffuse/albedo texture** for skin at all — instead they ship ~15 specialized maps per body part (`_ao`, `_BCBMap`, `_Blend Mask`, `_CFULCMask`, `_ENMask`, `_MicroNMask`, `_MNAOMask`, `_NBMap`, `_NMUILMask`, `_ResourceMap_Position`, `_ResourceMap_WSNormal`, `_roughness`, `_SpecMask`, `_SSSMap`, `_TransMap`) designed for Reallusion's proprietary "Digital Human Shader" (built for Unreal/HDRP-class multi-layer skin rendering), not a simple PBR workflow. **A plain FBX import cannot correctly render this character's skin** — it needs either Reallusion's official Unity plugin/shader package, or a custom shader graph built to consume these maps, or (fastest) sourcing/baking a plain diffuse texture separately.
- **Animator:** Not configured. The rig exists but no `Animator` component / `Avatar` was set up in this pass (out of scope for this audit — asset validation only, no gameplay rigging).
- **Task 5 — can 5 visually distinct characters be made from the 3 free bases?** **NO — not from Unity/FBX alone.** Inspected all 265-266 unique blend shape names per base (`01_CC3_Base_Plus`, `02_Neutral_F`, `03_Neutral_M`): every single one is a **facial-animation/expression/viseme shape** (lip-sync shapes like `V_Open`, `V_Tight_O`; ear-wiggle shapes like `Ear_Up_L`; eye/brow movement shapes). **None of them are identity-shaping morphs** (no nose-width, jaw-size, cheek-shape, height, or build sliders). Those sliders exist only inside Reallusion's Character Creator *software*, not in the exported FBX. Additionally, each base is **only the nude body+face** — no separate hair or clothing mesh is included at all. Practical conclusion:
  - Elias, Jonah, and any other character reusing `03_Neutral_M` untouched **will be geometrically identical** — no amount of Unity-side blend-shape puppeteering can change that.
  - The only real differentiation lever available right now is **texture/material tinting** (skin tone, eye color) plus **adding separate hair/clothing assets**, once the skin-texture issue above is also fixed.
- **Shortlist for 2 additional free character sources (not downloaded, per instructions):**
  1. **Mixamo** (mixamo.com) — free Adobe account, large free character library, confirmed commercial-use license. Would need 2 more visually distinct realistic humans manually curated from the library (style consistency needs checking per-model since the library isn't uniform).
  2. **Vitruvian Project** (CC0, GitHub: `WithinAmnesia/Vitruvian-Project`) — public-domain digital-human project; character count/rig details were not verifiable via automated fetch during the earlier research pass and would need manual inspection of the repo before committing.
- **Verdict: PASS WITH LIMITATIONS (base mesh) / FAIL (as a ready-to-use textured character)** — the mesh+rig is usable, but skin rendering is currently broken (flat white) and needs either Reallusion's Unity plugin or manual shader work before it looks like a character at all. The 5-distinct-character goal is **not achievable from this asset alone**.

## 7. Wolf (OpenGameArt CC0)

- **Import:** Clean, no errors after materials were extracted to standalone assets.
- **URP material:** Was `Standard` shader (pink-risk under this project's pure URP pipeline) — fixed by extracting embedded materials (`materialLocation: External`) and converting `Dark_Gray.mat` / `Dark_White...mat` to `Universal Render Pipeline/Lit`.
- **Scale bug found & fixed:** Original bounds were **1.91 × 6.69 × 9.13 m** — a wolf nearly 7 m tall. Applied `globalScale = 0.15` on the `ModelImporter`; corrected bounds are **0.29 × 1.00 × 1.37 m**, which is realistic for a wolf (proportions were already correct, it was a uniform scale error).
- **Texture:** **None at all.** Both materials are flat solid colors (`Dark_Gray` ≈ RGB 0.82, `Dark_White` ≈ same) with no diffuse/normal/roughness texture maps — confirmed by inspecting the material and the source `.dae`. This is a genuinely bare, textureless mesh.
- **Rig:** **NONE.** Confirmed via code inspection — 0 `SkinnedMeshRenderer` components, 0 bones. It's a single static `MeshFilter`/`MeshRenderer` (the internal mesh is literally named `"Cube"`, a leftover Blender default name).
- **Animator/Animation status (task 4 — exact list requested):**

  | Animation | Present? |
  |---|---|
  | Idle | **NO** |
  | Walk | **NO** |
  | Run | **NO** |
  | Attack | **NO** |
  | Hit | **NO** |
  | Death | **NO** |

  **Zero animations exist. Zero bones exist. This is a fully static prop mesh, not a game-ready animal.** Per instructions, no new animation assets were downloaded — this is reported only.
- **Triangles:** 852 — very light, good for multi-wolf scenes once/if it's rigged.
- **Verdict: FAIL as a game-ready wolf.** Usable only as a static decoration/corpse prop right now, or as raw geometry for someone to rig from scratch (Blender + Rigify, or attempt Mixamo's auto-rigger). Matches what was already flagged during acquisition (`OPTIMIZATION_REQUIRED`) — now confirmed and quantified.

---

## Cross-Cutting Notes

- **Render pipeline confirmed:** Project uses `PC_RPAsset` (`UniversalRenderPipelineAsset`) exclusively — any asset shipping `Standard`-shader materials **will render pink** unless converted (this applied to the Rowboat and Wolf; both fixed).
- **Vecindario backdrop:** Disabled as a **scene-instance override** in `AssetTest.unity` only (for a readable test-scene screenshot); the production prefab `PF_AbandonedHouse.prefab` keeps it **active** as originally authored, since it's likely intended as distant window-view scenery.
- **No production map was touched.** All work is confined to `Assets/ThirdParty/`, `Assets/_TRLM/Prefabs/`, and the new `Assets/_TRLM/Scenes/Tests/AssetTest.unity`.
- **No gameplay code was written.**
