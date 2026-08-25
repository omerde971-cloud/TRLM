using TRLM.AI.Human;
using TRLM.Boat;
using TRLM.Dialogue;
using TRLM.Inventory;
using TRLM.Save;
using TRLM.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace TRLM.EditorTools
{
    public static class Sprint11Phase2WorldBuilder
    {
        private const string ScenePath = "Assets/_TRLM/Scenes/Production/20_Island_Blockout.unity";

        [MenuItem("TRLM/Build Sprint 11 Phase 2 World Route")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath);

            var root = GameObject.Find("S11_Phase2_PlayableRoute") ?? new GameObject("S11_Phase2_PlayableRoute");
            EnsureIslandDialogue(root.transform);
            CleanupPrototypeVisibility();
            GroundFirstRouteObjects();
            BuildLandingAndForestBeats(root.transform);
            BuildFirstClue(root.transform);
            BuildFirstLootHouse(root.transform);
            BuildWolfForeshadowAndEncounter(root.transform);
            BuildSettlementSafeHouse(root.transform);
            BuildCaveStaging(root.transform);
            BuildSoldierFoundation(root.transform);
            TuneVisualQuality();

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TRLM] Sprint 11 Phase 2 world route built.");
        }

        private static void EnsureIslandDialogue(Transform parent)
        {
            var systems = FindOrCreate("S11_DialogueSystems", parent);
            var dialogue = systems.GetComponent<DialogueSystem>() ?? systems.AddComponent<DialogueSystem>();

            var canvas = GameObject.Find("IslandSubtitleCanvas") ??
                         new GameObject("IslandSubtitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(systems.transform, false);
            var c = canvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 80;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panel = FindOrCreate("IslandSubtitlePanel", canvas.transform);
            EnsureComponent<RectTransform>(panel);
            EnsureComponent<Image>(panel).color = new Color(0.02f, 0.025f, 0.028f, 0.86f);
            EnsureComponent<CanvasGroup>(panel);
            var ui = EnsureComponent<TRLM.UI.SubtitleUI>(panel);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 70f);
            rt.sizeDelta = new Vector2(1180f, 128f);

            var speaker = FindOrCreateText("Speaker", panel.transform, new Vector2(0f, 0.58f), new Vector2(1f, 1f), 20);
            speaker.color = new Color(0.95f, 0.88f, 0.72f, 1f);
            AddOutline(speaker, new Vector2(1.4f, -1.4f));
            var line = FindOrCreateText("Line", panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.72f), 32);
            line.color = Color.white;
            line.fontStyle = FontStyle.Bold;
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            AddOutline(line, new Vector2(1.8f, -1.8f));

            var so = new SerializedObject(ui);
            so.FindProperty("dialogueSystem").objectReferenceValue = dialogue;
            so.FindProperty("speakerText").objectReferenceValue = speaker;
            so.FindProperty("lineText").objectReferenceValue = line;
            so.FindProperty("backing").objectReferenceValue = panel.GetComponent<Image>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CleanupPrototypeVisibility()
        {
            var duplicateBoat = GameObject.Find("PF_Rowboat");
            if (duplicateBoat != null) duplicateBoat.SetActive(false);

            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null) continue;
                string name = renderer.gameObject.name;
                if (name.Contains("Marker") || name.Contains("visualPlaceholder") || name.Contains("DEV_Placeholder_FireVisual"))
                    renderer.enabled = false;

                if (name.Contains("TreeCard") || name.Contains("Background_Tree_Atlas"))
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static void GroundFirstRouteObjects()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                string n = t.name;
                bool target =
                    n.Contains("Tree") || n.Contains("Rock") || n.Contains("House") ||
                    n.Contains("Settlement") || n.Contains("Loot_") || n.Contains("Story_") ||
                    n.Contains("SafeHouse") || n.Contains("Landmark_Cave");
                if (!target) continue;
                if (t.GetComponent<Terrain>() != null || t.GetComponent<Camera>() != null) continue;
                GroundTransform(t, 0f);
            }
        }

        private static void BuildLandingAndForestBeats(Transform parent)
        {
            var root = FindOrCreate("Route_01_LandingToSettlement_Composition", parent);

            PlaceRockCluster(root.transform, "Landing_Shoreline_Rocks", new Vector3(400f, 0f, 8f), 9, 18f, 0.6f);
            PlaceRockCluster(root.transform, "Landing_Waterline_Mask_West", new Vector3(375f, 0f, -2f), 7, 15f, 0.45f);
            PlaceRockCluster(root.transform, "Landing_Waterline_Mask_East", new Vector3(425f, 0f, -2f), 7, 15f, 0.45f);
            PlaceTreeCluster(root.transform, "CoastalSparse_EastCluster", new Vector3(430f, 0f, 55f), 7, 18f, 0.85f);
            PlaceTreeCluster(root.transform, "CoastalSparse_WestCluster", new Vector3(370f, 0f, 62f), 6, 18f, 0.8f);
            PlaceTreeCluster(root.transform, "ForestTightening_LeftWall", new Vector3(365f, 0f, 118f), 12, 24f, 1.05f);
            PlaceTreeCluster(root.transform, "ForestTightening_RightWall", new Vector3(435f, 0f, 122f), 12, 24f, 1.05f);
            PlaceFallenLog(root.transform, "FallenLog_PathEdge_01", new Vector3(393f, 0f, 105f), 25f, 6.5f);
            PlaceFallenLog(root.transform, "FallenLog_PathEdge_02", new Vector3(413f, 0f, 145f), -18f, 5.5f);
            PlaceRockCluster(root.transform, "SettlementReveal_RockEdges", new Vector3(405f, 0f, 154f), 8, 16f, 0.65f);
        }

        private static void BuildFirstClue(Transform parent)
        {
            var root = FindOrCreate("Story_FirstClue_OldResearchMarker", parent);
            root.transform.position = Grounded(new Vector3(402f, 0f, 54f), 0.05f);

            var post = Primitive("OldResearchMarker_Post", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.45f, 0f), new Vector3(0.16f, 1.45f, 0.16f), Mat("S11_WetWood", new Color(0.24f, 0.16f, 0.10f)));
            var map = Primitive("DamagedMapFragment", PrimitiveType.Cube, root.transform, new Vector3(0.38f, 1.1f, -0.05f), new Vector3(1.15f, 0.72f, 0.045f), Mat("S11_FadedPaper", new Color(0.65f, 0.56f, 0.38f)));
            map.transform.localRotation = Quaternion.Euler(0f, 18f, 5f);
            Primitive("PaintedRouteSymbol", PrimitiveType.Cube, root.transform, new Vector3(0.42f, 1.13f, -0.1f), new Vector3(0.22f, 0.22f, 0.05f), Mat("S11_RedOchre", new Color(0.5f, 0.06f, 0.035f)));
            EditorUtility.SetDirty(post);

            AddSequenceTrigger(root, "FirstClue_Reaction", new Vector3(0f, 1f, 0f), new Vector3(10f, 3f, 10f),
                Line("s11_island_clue_001", DialogueSpeaker.Mira, "Bu boya yeni değil. Ama işaret bizim haritadakiyle aynı.", "This paint is not fresh. But the mark matches our map.", DialogueEmotion.Focused, "controlled discovery", "first_clue"),
                Line("s11_island_clue_002", DialogueSpeaker.Jonah, "Yani bizden önce biri aynı fikre kapılmış.", "So someone had the same bad idea before us.", DialogueEmotion.Uneasy, "dry but tense", "first_clue"));
        }

        private static void BuildFirstLootHouse(Transform parent)
        {
            var root = FindOrCreate("Story_FirstLootableHouse", parent);
            root.transform.position = Grounded(new Vector3(420f, 0f, 170f), 0.05f);

            var door = Primitive("FirstHouse_Door_Interactable", PrimitiveType.Cube, root.transform, new Vector3(-1.45f, 1.1f, -2.05f), new Vector3(0.08f, 2.0f, 0.9f), Mat("S11_DoorWood", new Color(0.28f, 0.18f, 0.1f)));
            EnsureComponent<TRLM.Interaction.TestDoor>(door);
            var table = Primitive("FirstHouse_SearchTable", PrimitiveType.Cube, root.transform, new Vector3(0.8f, 0.45f, 0.4f), new Vector3(1.7f, 0.18f, 0.8f), Mat("S11_WetWood", new Color(0.24f, 0.16f, 0.10f)));
            Primitive("FirstHouse_TableLeg_01", PrimitiveType.Cylinder, table.transform, new Vector3(-0.65f, -0.35f, -0.25f), new Vector3(0.08f, 0.35f, 0.08f), Mat("S11_WetWood", new Color(0.24f, 0.16f, 0.10f)));
            Primitive("FirstHouse_TableLeg_02", PrimitiveType.Cylinder, table.transform, new Vector3(0.65f, -0.35f, -0.25f), new Vector3(0.08f, 0.35f, 0.08f), Mat("S11_WetWood", new Color(0.24f, 0.16f, 0.10f)));

            Pickup(root.transform, "Pickup_Water_FirstHouse", "Assets/_TRLM/ScriptableObjects/Items/BottledWater.asset", new Vector3(0.35f, 0.75f, 0.2f), new Color(0.35f, 0.65f, 0.9f));
            Pickup(root.transform, "Pickup_Food_FirstHouse", "Assets/_TRLM/ScriptableObjects/Items/FoodRation.asset", new Vector3(0.8f, 0.75f, 0.32f), new Color(0.72f, 0.54f, 0.25f));
            Pickup(root.transform, "Pickup_Bandage_FirstHouse", "Assets/_TRLM/ScriptableObjects/Items/Bandage.asset", new Vector3(1.15f, 0.75f, 0.08f), Color.white);
            Pickup(root.transform, "Pickup_Battery_FirstHouse", "Assets/_TRLM/ScriptableObjects/Items/Battery.asset", new Vector3(1.35f, 0.75f, 0.46f), new Color(0.12f, 0.12f, 0.12f));

            AddSequenceTrigger(root, "FirstHouse_Inspect_Reaction", new Vector3(0f, 1f, 0f), new Vector3(11f, 3f, 11f),
                Line("s11_house_001", DialogueSpeaker.Noah, "Alınabilecek az şey var. Birileri buradan hızlı çıkmış.", "There is not much worth taking. Someone left this place fast.", DialogueEmotion.Focused, "quiet assessment", "first_house"),
                Line("s11_house_002", DialogueSpeaker.Lena, "Hızlı çıkıp kapıyı açık bırakmak... bu hiç iyi değil.", "Leaving fast enough to leave the door open... that is not great.", DialogueEmotion.Nervous, "low", "first_house"));
        }

        private static void BuildWolfForeshadowAndEncounter(Transform parent)
        {
            var root = FindOrCreate("Story_FirstWolfForeshadow_Encounter", parent);

            var tracks = FindOrCreate("WolfTracks_DisturbedMud", root.transform);
            tracks.transform.position = Grounded(new Vector3(405f, 0f, 238f), 0.03f);
            for (int i = 0; i < 6; i++)
                Primitive("Track_Paw_" + i, PrimitiveType.Cube, tracks.transform, new Vector3((i % 2 == 0 ? -0.25f : 0.25f), 0.02f, i * 0.55f), new Vector3(0.22f, 0.025f, 0.32f), Mat("S11_DarkMud", new Color(0.08f, 0.065f, 0.045f)));

            AddSequenceTrigger(root, "WolfForeshadow_Reaction", Grounded(new Vector3(405f, 0f, 238f), 1f) - root.transform.position, new Vector3(16f, 4f, 16f),
                Line("s11_wolf_foreshadow_001", DialogueSpeaker.Jonah, "Durun. Bunlar köpek izi değil.", "Wait. These are not dog tracks.", DialogueEmotion.Urgent, "hushed warning", "wolf_foreshadow"),
                Line("s11_wolf_foreshadow_002", DialogueSpeaker.Mira, "Taze. En fazla birkaç saat.", "Fresh. A few hours at most.", DialogueEmotion.Focused, "clinical", "wolf_foreshadow"));

            var wolfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_TRLM/Prefabs/Animals/PF_Wolf.prefab");
            var wolf = GameObject.Find("S11_FirstWolf_Encounter");
            if (wolf == null && wolfPrefab != null)
            {
                wolf = (GameObject)PrefabUtility.InstantiatePrefab(wolfPrefab);
                wolf.name = "S11_FirstWolf_Encounter";
            }
            if (wolf != null)
            {
                wolf.transform.SetParent(root.transform, true);
                wolf.transform.position = Grounded(new Vector3(400f, 0f, 330f), 0.15f);
                wolf.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                EditorUtility.SetDirty(wolf);
            }

            AddSequenceTrigger(root, "PostWolf_TeamReaction", Grounded(new Vector3(410f, 0f, 365f), 1f) - root.transform.position, new Vector3(20f, 4f, 20f),
                Line("s11_post_wolf_001", DialogueSpeaker.Lena, "Bunu videoya alsaydım bile kimse inanmazdı.", "Even if I had filmed that, nobody would believe it.", DialogueEmotion.Nervous, "breathless", "post_wolf"),
                Line("s11_post_wolf_002", DialogueSpeaker.Elias, "Herkes iyi mi? Bundan sonra yakın kalıyoruz.", "Everyone okay? From now on we stay close.", DialogueEmotion.Determined, "protective", "post_wolf"),
                Line("s11_post_wolf_003", DialogueSpeaker.Noah, "Ada bizi korkutmaya çalışmıyor. Sadece umursamıyor.", "The island is not trying to scare us. It simply does not care.", DialogueEmotion.Uneasy, "flat truth", "post_wolf"));
        }

        private static void BuildSettlementSafeHouse(Transform parent)
        {
            var root = FindOrCreate("Settlement_Readability_And_SafeHouse", parent);
            root.transform.position = Grounded(new Vector3(423f, 0f, 173f), 0.05f);

            PlaceRockCluster(root.transform, "Settlement_Debris_NotTreasure", new Vector3(0f, 0f, 0f), 7, 7f, 0.35f);
            Primitive("SafeHouse_Firepit_Ring", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(1.1f, 0.08f, 1.1f), Mat("S11_CharcoalStone", new Color(0.06f, 0.055f, 0.05f)));
            Primitive("SafeHouse_ColdAsh", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.13f, 0f), new Vector3(0.7f, 0.025f, 0.7f), Mat("S11_Ash", new Color(0.22f, 0.22f, 0.2f)));

            var area = GameObject.Find("SafeHouse_01_SettlementHouse") ?? root;
            EnsureComponent<SafeHouseArea>(area);
            EnsureComponent<ManualSaveZone>(area);
            var col = EnsureBoxCollider(area);
            col.isTrigger = true;
            col.size = new Vector3(10f, 5f, 10f);

            var bed = Primitive("SafeHouse_SleepBedroll", PrimitiveType.Cube, root.transform, new Vector3(-1.6f, 0.16f, 1.2f), new Vector3(1.9f, 0.12f, 0.85f), Mat("S11_Bedroll", new Color(0.18f, 0.25f, 0.22f)));
            var sleep = EnsureComponent<SleepInteraction>(bed);
            var so = new SerializedObject(sleep);
            so.FindProperty("safeHouseArea").objectReferenceValue = area.GetComponent<SafeHouseArea>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCaveStaging(Transform parent)
        {
            var root = FindOrCreate("CaveEntrance_VisualStaging", parent);
            root.transform.position = Grounded(new Vector3(400f, 0f, 790f), 0.1f);

            PlaceRockCluster(root.transform, "CaveMouth_RockFrame_Left", new Vector3(-4f, 0f, 0f), 8, 6f, 1.2f);
            PlaceRockCluster(root.transform, "CaveMouth_RockFrame_Right", new Vector3(4f, 0f, 0f), 8, 6f, 1.2f);
            var darkness = Primitive("CaveMouth_DarkDepth", PrimitiveType.Cube, root.transform, new Vector3(0f, 2.2f, 1.1f), new Vector3(5.5f, 3.8f, 0.25f), Mat("S11_CaveDarkness", new Color(0.005f, 0.006f, 0.008f)));
            darkness.GetComponent<Collider>().enabled = false;

            AddSequenceTrigger(root, "CaveMotivation_Reaction", new Vector3(0f, 1.5f, -18f), new Vector3(22f, 5f, 18f),
                Line("s11_cave_001", DialogueSpeaker.Mira, "Dağdaki yarık... işaretlerin hepsi oraya bakıyor.", "That split in the mountain... every mark points there.", DialogueEmotion.Focused, "quiet certainty", "cave_motivation"),
                Line("s11_cave_002", DialogueSpeaker.Elias, "O zaman cevabı evlerde değil, yukarıda arıyoruz.", "Then the answer is not in the houses. It is up there.", DialogueEmotion.Determined, "resolved", "cave_motivation"));
        }

        private static void BuildSoldierFoundation(Transform parent)
        {
            var root = FindOrCreate("S11_IsolatedSoldierTestZone", parent);
            root.transform.position = Grounded(new Vector3(505f, 0f, 602f), 0.05f);

            var p0 = FindOrCreate("SoldierPatrol_A", root.transform);
            var p1 = FindOrCreate("SoldierPatrol_B", root.transform);
            p0.transform.position = Grounded(new Vector3(500f, 0f, 598f), 0.05f);
            p1.transform.position = Grounded(new Vector3(512f, 0f, 606f), 0.05f);

            var soldier = GameObject.Find("S11_Soldier_AI_Foundation");
            if (soldier == null)
                soldier = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            soldier.name = "S11_Soldier_AI_Foundation";
            soldier.transform.SetParent(root.transform, true);
            soldier.transform.position = p0.transform.position + Vector3.up;
            soldier.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            soldier.GetComponent<Renderer>().sharedMaterial = Mat("S11_SoldierDrab", new Color(0.19f, 0.22f, 0.17f));
            EnsureComponent<NavMeshAgent>(soldier);
            EnsureComponent<TRLM.Survival.HealthSystem>(soldier);
            var ai = EnsureComponent<SoldierAI>(soldier);
            var so = new SerializedObject(ai);
            var patrol = so.FindProperty("patrolPoints");
            patrol.arraySize = 2;
            patrol.GetArrayElementAtIndex(0).objectReferenceValue = p0.transform;
            patrol.GetArrayElementAtIndex(1).objectReferenceValue = p1.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void TuneVisualQuality()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.62f, 0.64f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0035f;
            QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 4);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        }

        private static void AddSequenceTrigger(GameObject root, string name, Vector3 localPosition, Vector3 size, params DialogueLine[] lines)
        {
            var go = FindOrCreate(name, root.transform);
            go.transform.localPosition = localPosition;
            var col = EnsureBoxCollider(go);
            col.isTrigger = true;
            col.size = size;
            var trigger = EnsureComponent<DialogueSequenceTrigger>(go);
            var so = new SerializedObject(trigger);
            var arr = so.FindProperty("lines");
            arr.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
                WriteLine(arr.GetArrayElementAtIndex(i), lines[i]);
            so.FindProperty("triggerOnEnter").boolValue = true;
            so.FindProperty("oneShot").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DialogueLine Line(string id, DialogueSpeaker speaker, string tr, string en, DialogueEmotion emotion, string delivery, string trigger)
        {
            return new DialogueLine
            {
                id = id,
                speaker = speaker,
                turkishText = tr,
                englishSubtitle = en,
                emotion = emotion,
                delivery = delivery,
                scene = "20_Island_Blockout",
                trigger = trigger,
                durationOverride = Mathf.Clamp(en.Length / 15f + 0.8f, 3f, 7f),
                subtitlePriority = SubtitlePriority.Contextual,
                oneShot = true
            };
        }

        private static void WriteLine(SerializedProperty p, DialogueLine line)
        {
            p.FindPropertyRelative("id").stringValue = line.id;
            p.FindPropertyRelative("speaker").enumValueIndex = (int)line.speaker;
            p.FindPropertyRelative("turkishText").stringValue = line.turkishText;
            p.FindPropertyRelative("englishSubtitle").stringValue = line.englishSubtitle;
            p.FindPropertyRelative("emotion").enumValueIndex = (int)line.emotion;
            p.FindPropertyRelative("delivery").stringValue = line.delivery;
            p.FindPropertyRelative("scene").stringValue = line.scene;
            p.FindPropertyRelative("trigger").stringValue = line.trigger;
            p.FindPropertyRelative("durationOverride").floatValue = line.durationOverride;
            p.FindPropertyRelative("subtitlePriority").intValue = (int)line.subtitlePriority;
            p.FindPropertyRelative("oneShot").boolValue = true;
        }

        private static void Pickup(Transform parent, string name, string itemPath, Vector3 localPosition, Color color)
        {
            var go = Primitive(name, PrimitiveType.Cylinder, parent, localPosition, new Vector3(0.22f, 0.12f, 0.22f), Mat(name + "_Mat", color));
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0) go.layer = interactableLayer;
            var pickup = EnsureComponent<PickupItem>(go);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            if (item != null) pickup.Configure(item, 1);
            EnsureComponent<TRLM.Core.PersistentObjectId>(go);
        }

        private static void PlaceTreeCluster(Transform parent, string name, Vector3 center, int count, float radius, float scale)
        {
            var root = FindOrCreate(name, parent);
            var prefabs = new[]
            {
                "Assets/_TRLM/Prefabs/Environment/PF_Tree_TypeA_01.prefab",
                "Assets/_TRLM/Prefabs/Environment/PF_Tree_TypeA_02.prefab",
                "Assets/_TRLM/Prefabs/Environment/PF_Tree_TypeB_01.prefab"
            };

            for (int i = 0; i < count; i++)
            {
                string childName = name + "_Tree_" + i;
                var existing = root.transform.Find(childName)?.gameObject;
                if (existing == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i % prefabs.Length]);
                    existing = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    existing.name = childName;
                    existing.transform.SetParent(root.transform, true);
                }

                float angle = i * 137.5f * Mathf.Deg2Rad;
                float r = radius * (0.35f + 0.65f * ((i * 37) % 100) / 100f);
                var pos = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                existing.transform.position = Grounded(pos, 0f);
                existing.transform.rotation = Quaternion.Euler(0f, (i * 41) % 360, 0f);
                existing.transform.localScale = Vector3.one * scale * (0.85f + (i % 4) * 0.11f);
                EditorUtility.SetDirty(existing);
            }
        }

        private static void PlaceRockCluster(Transform parent, string name, Vector3 center, int count, float radius, float scale)
        {
            var root = FindOrCreate(name, parent);
            for (int i = 0; i < count; i++)
            {
                string childName = name + "_Rock_" + i;
                var go = root.transform.Find(childName)?.gameObject;
                if (go == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_TRLM/Prefabs/Environment/PF_Rock_Stylized_0" + ((i % 9) + 1) + ".prefab");
                    go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = childName;
                    go.transform.SetParent(root.transform, true);
                }

                float angle = i * 91f * Mathf.Deg2Rad;
                float r = radius * (0.25f + 0.75f * ((i * 29) % 100) / 100f);
                var pos = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                go.transform.position = Grounded(pos, 0f);
                go.transform.rotation = Quaternion.Euler((i * 13) % 20, (i * 53) % 360, (i * 7) % 16);
                go.transform.localScale = Vector3.one * scale * (0.7f + (i % 5) * 0.18f);
                EditorUtility.SetDirty(go);
            }
        }

        private static void PlaceFallenLog(Transform parent, string name, Vector3 position, float yaw, float length)
        {
            var go = Primitive(name, PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(0.28f, length * 0.5f, 0.28f), Mat("S11_WetWood", new Color(0.24f, 0.16f, 0.10f)));
            go.transform.position = Grounded(position, 0.22f);
            go.transform.rotation = Quaternion.Euler(86f, yaw, 0f);
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material mat)
        {
            var existing = parent.Find(name)?.gameObject;
            var go = existing ?? GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = mat;
            return go;
        }

        private static Vector3 Grounded(Vector3 position, float offset)
        {
            if (Terrain.activeTerrain != null)
                position.y = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.transform.position.y + offset;
            return position;
        }

        private static void GroundTransform(Transform t, float offset)
        {
            if (Terrain.activeTerrain == null) return;
            var pos = t.position;
            pos.y = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y + offset;
            t.position = pos;
            EditorUtility.SetDirty(t);
        }

        private static Material Mat(string name, Color color)
        {
            const string folder = "Assets/_TRLM/Materials/Sprint11";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_TRLM/Materials"))
                    AssetDatabase.CreateFolder("Assets/_TRLM", "Materials");
                AssetDatabase.CreateFolder("Assets/_TRLM/Materials", "Sprint11");
            }

            string path = folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.name = name;
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject FindOrCreate(string name, Transform parent)
        {
            var existing = parent != null ? parent.Find(name)?.gameObject : GameObject.Find(name);
            if (existing == null) existing = GameObject.Find(name);
            if (existing != null)
            {
                if (parent != null) existing.transform.SetParent(parent, true);
                return existing;
            }

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            return go.GetComponent<T>() ?? go.AddComponent<T>();
        }

        private static BoxCollider EnsureBoxCollider(GameObject go)
        {
            var box = go.GetComponent<BoxCollider>();
            if (box != null) return box;
            return go.AddComponent<BoxCollider>();
        }

        private static Text FindOrCreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, int size)
        {
            var go = FindOrCreate(name, parent);
            EnsureComponent<RectTransform>(go);
            var text = EnsureComponent<Text>(go);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(42f, 8f);
            rt.offsetMax = new Vector2(-42f, -8f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static void AddOutline(Text text, Vector2 distance)
        {
            var outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }
    }
}
