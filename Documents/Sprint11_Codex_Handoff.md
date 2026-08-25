# TRLM Sprint 11 Codex Handoff

Date: 2026-08-23

Foundation checkpoint: `d872a41 Sprint 11 playable reconstruction foundations`

## Implemented

- Main menu rebuild was accepted before this continuation.
- Final scene-flow blocker fixed and certified:
  - Build Settings now start at `00_MainMenu`, then `05_Neighborhood_Cinematic`, then `20_Island_Blockout`; `SampleScene` is no longer the enabled production index 0 scene.
  - Runtime scene loads now go through `TRLM.Flow.SceneFlow`, with development-only logs for from/to/reason/caller/transition id/timestamp/status.
  - Main Menu New Game, Opening cinematic completion, Continue/Load Slot, and Preparation fallback are routed through the same scene-flow gate.
  - Opening re-entry is rejected unless it is the authored New Game transition from Main Menu.
  - Opening cinematic completion is debounced to prevent duplicate island loads.
- `05_Neighborhood_Cinematic` remains the playable New Game entry:
  - `OpeningCinematicController` drives authored camera beats, subtitle timing, objective advance, and island scene load.
  - English subtitles are readable in Play Mode; Turkish VO text is manifest-authored but audio is not generated in-repo.
- `20_Island_Blockout` Phase 2 route was built with `Sprint11Phase2WorldBuilder`:
  - Landing/coastal shoreline rock masking and sparse-to-dense forest composition beats.
  - First environmental clue: `Story_FirstClue_OldResearchMarker`.
  - First lootable house loop: `Story_FirstLootableHouse`, interactable door, inspect reaction, and four limited pickups.
  - Wolf foreshadowing: tracks/disturbed mud plus companion reactions.
  - First authored wolf encounter: `S11_FirstWolf_Encounter`.
  - Post-wolf team reaction trigger.
  - Settlement readability pass and first safe-house activation.
  - Cave/mountain destination staging with visible dark cave-mouth threshold.
  - One isolated soldier foundation encounter: `S11_IsolatedSoldierTestZone`.
- World cleanup:
  - Duplicate root `PF_Rowboat` disabled so `Rowboat_SeaApproach` is the playable production boat.
  - Visible marker/debug renderers hidden for normal gameplay.
  - Tree-card/background tree shadow casting disabled in scene pass.
  - Route-adjacent trees/rocks/houses/story props grounded against terrain.
  - `DebugHUD` now starts hidden by default and remains F3-toggleable.
- Wolf AI:
  - Wolves can target Elias/player or living companions.
  - Added stable `committedTarget` behavior with target memory.
  - Dead targets are released/ignored.
  - Existing `CanSeePlayer` API remains for older callers.
- Soldier AI:
  - Added `SoldierAI` with `Patrol`, `Suspicious`, `Investigate`, `Alert`, `Combat`, `Search`, `Return`.
  - Uses FOV/LOS vision and existing `NoiseEvents` hearing.
  - Running/gunshots can matter through existing noise emitters.
  - Lethal damage stops AI.

## Playtest Path

1. Main Menu.
2. New Game.
3. Opening cinematic.
4. Island load.
5. Rowboat/landing beach.
6. First clue marker near coastal route.
7. Enter/inspect first house.
8. Collect limited loot: water, food, bandage, battery.
9. Follow forest route to wolf tracks.
10. First wolf encounter.
11. Post-wolf team reaction.
12. Settlement and safe house.
13. Cave/mountain staging cue.
14. Optional isolated soldier test zone near mountain checkpoint.

## Expected Behavior

- Normal player HUD should not show the developer debug panel until F3 is pressed.
- Landing route should read as open coast into sparse trees, then tighter forest, clue, house, threat, settlement, and mountain motivation.
- Clue/house/wolf/cave triggers play short English subtitles via the island `DialogueSystem`.
- First house provides limited contextual loot and should not over-reward the player.
- Safe house supports existing shelter/manual-save/sleep hooks.
- Wolf can perceive player and all four companions; line of sight blocks attacks; death disables AI.
- Soldier remains isolated and should not turn TRLM into a shooter-focused opening.

## Known Asset Debt

- `ANIMATION_ASSET_MISSING`: wolf remains gameplay-functional but not truly rigged/animated.
- Character animation and body presentation remain placeholder-level in several route shots.
- Turkish VO audio is not generated or assigned.
- Several story props are primitive-authored but dressed/materialed enough for a production prototype.

## Known Tech Debt

- `Sprint11Phase2WorldBuilder` is an editor builder, not a long-term level-authoring pipeline.
- `SoldierAI` is a minimum foundation and does not yet use a real weapon animation/cover system.
- Wolf perception currently scans companions when acquiring targets; current scene scale is fine, but larger packs/teams should use cached target registries.

## Known Visual Debt

- Terrain still has visible steep cuts/texture repetition in early coastal areas.
- Coastline remains improved but not final-quality foam/water blending.
- Settlement is more readable, but some inherited structures are still blockout-adjacent.
- Cave staging is only an entrance silhouette, not a dungeon.
- Scene-view QA screenshots include editor gizmos; Game View screenshot is the better visual proof.

## Performance Hotspots

- Coastal forest with four companions: watch NavMeshAgent repath and foliage/card overdraw.
- Settlement/safe house: watch lights/fire/weather interaction and pickup count.
- Wolf encounter: watch `WolfPerception` target acquisition and companion AI at the same time.
- Weather/rain: watch `WetSurfaceResponse`, rain VFX, fog, and transparent foliage shimmer.
- Rockfall pooled rocks now use fitted primitive `SphereCollider`s instead of forcing high-poly dressing meshes into convex hulls, removing the `SM_Rocks_03` / `SM_Rocks_04` convex warning source.

## Regression Risks

- Main Menu continuous flow is certified in this handoff, but a human first-person QA pass should still inspect route feel, collision, and visual readability.
- Disabling duplicate `PF_Rowboat` assumes `Rowboat_SeaApproach` is the only intended production boat.
- Safe-house collider sharing with existing scene object should be checked in first-person for doorway/interior feel.
- Quality settings were updated for AA/aniso; confirm target platforms tolerate the change.

## Claude Review Priorities

- Independently play Main Menu -> New Game -> Opening -> Island and verify no return to cinematic occurs.
- Walk the first 30-45 minute route in first person and flag every visible floating/buried object.
- Confirm first house doorway/interior collision has no invisible blocker.
- Verify wolf cannot damage through walls and does not switch targets every frame.
- Verify soldier hearing/vision is acceptable but not over-prominent.
- Review route visual quality: coastline, terrain seams, settlement readability, cave silhouette.
- Confirm `ANIMATION_ASSET_MISSING` is accepted as asset debt, not reported as complete.

## QA Artifacts

- `Assets/Screenshots/sprint11_opening_playmode_final_fixed.png`
- `Assets/Screenshots/sprint11_island_scene_companions_recovery.png`
- `Assets/Screenshots/sprint11_phase2_first_clue_game_final.png`
- `Assets/Screenshots/sprint11_phase2_loot_house.png`
- `Assets/Screenshots/sprint11_phase2_wolf_route.png`
- `Assets/Screenshots/sprint11_phase2_cave_staging.png`

## Verified

- Final continuous scene-flow/playthrough certification:
  - Main Menu Play Mode: New Game invoked through the real UI button.
  - SceneFlow log observed `00_MainMenu -> 05_Neighborhood_Cinematic` with reason `NewGameFromMainMenu`.
  - Opening cinematic completed through its own controller and loaded `20_Island_Blockout`.
  - No return to `05_Neighborhood_Cinematic` occurred after island load.
  - Island stage records: `PreparationComplete` on arrival, `ReachLandingZone` after boat/landing, `EnterCoastalForest` through region trigger, `AcquireEssentialLoot` after first-house pickups, `WolfThreat` from wolf AI watcher, `ReachSafeHouse`, then `SliceComplete` by cave staging pass.
  - Player remained present; all 4 companions remained active during the route probe.
- Scene-flow regression tests:
  - `ProductionBuildScenes_StartAtMainMenuAndFollowAuthoredRoute`: passed.
  - `ProductionScripts_LoadScenesOnlyThroughSceneFlowGate`: passed.
- Full EditMode suite: 13 passed, 0 failed.
- Opening audio listener smoke:
  - `05_Neighborhood_Cinematic` now has exactly 1 enabled `AudioListener`.
  - Main Menu -> New Game smoke showed no duplicate AudioListener warning.
- Unity console: 0 red gameplay exceptions after final builder pass.
- Direct island Play Mode smoke:
  - Scene: `20_Island_Blockout`.
  - Dialogue sequence triggers: 5.
  - First-house pickups: 4.
  - Soldier state: `Patrol`.
  - Wolf state: `Idle`.
- Wolf perception probe:
  - `PF_Player`: visible.
  - `PF_Mira`: visible.
  - `PF_Jonah_Companion`: visible.
  - `PF_Lena`: visible.
  - `PF_Noah`: visible.
- Wolf and soldier lethal damage probe:
  - `dead=true`.
  - AI component disabled.
