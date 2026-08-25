using System.Collections.Generic;
using TRLM.Progression;
using TRLM.Notebook;
using TRLM.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TRLM.EditorTools
{
    /// <summary>
    /// Sprint 3 — builds the playable CAVE INTERIOR out of existing rock/dark materials and wires the
    /// gameplay beats into the EXISTING systems (ObjectiveSystem, ProphecyNotebook, PlayerEquipment,
    /// StoryFlags, cinematic). Deliberately re-runnable and idempotent: it clears and rebuilds
    /// <c>CAVE_Interior</c> each run. The hierarchy is split into a replaceable VISUAL SHELL and a
    /// stable GAMEPLAY layer so the rock-pack shell can later be swapped for a premium cave asset
    /// WITHOUT touching triggers / objectives / pickups.
    /// </summary>
    public static class Sprint3CaveBuilder
    {
        private const string ScenePath = "Assets/_TRLM/Scenes/Production/20_Island_Blockout.unity";
        private const string RootName = "CAVE_Interior";

        // Material + asset paths
        private const string MatFloor = "Assets/_TRLM/Materials/Sprint11/S11_DarkMud.mat";
        private const string MatWall = "Assets/_TRLM/Materials/Sprint11/S11_CharcoalStone.mat";
        private const string MatCeil = "Assets/_TRLM/Materials/Sprint11/S11_CaveDarkness.mat";
        private const string ShotgunPath = "Assets/_TRLM/ScriptableObjects/Weapons/WPN_Shotgun.asset";
        private const string AmmoPath = "Assets/_TRLM/ScriptableObjects/Items/Ammo_12Gauge.asset";
        private const string PagePath = "Assets/_TRLM/ScriptableObjects/Notebook/ProphecyPage_TornFirstLeaf.asset";
        private const string RockFmt = "Assets/_TRLM/Prefabs/Environment/PF_Rock_Stylized_{0:00}.prefab";

        private static Material _mFloor, _mWall, _mCeil;
        private static Transform _shell, _play;
        private static int _rng;

        [MenuItem("TRLM/Sprint 3/Build Cave Interior")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath);

            _mFloor = AssetDatabase.LoadAssetAtPath<Material>(MatFloor);
            _mWall = AssetDatabase.LoadAssetAtPath<Material>(MatWall);
            _mCeil = AssetDatabase.LoadAssetAtPath<Material>(MatCeil);
            _rng = 12345;

            // Fresh root
            var existing = GameObject.Find("/" + RootName);
            if (existing != null) Object.DestroyImmediate(existing);
            var root = new GameObject(RootName);
            _shell = new GameObject("VisualShell").transform; _shell.SetParent(root.transform, false);
            _play = new GameObject("Gameplay").transform; _play.SetParent(root.transform, false);

            BuildShell();
            BuildLighting();
            BuildAudio();
            BuildEnterTrigger();
            RelocatePage();
            BuildWeaponCache();
            BuildThresholdAndContinuation();
            ConfigureNotebook();
            ReframeCinematic();

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            // NOTE: SaveScene intentionally NOT called here — saving the large scene inside the same
            // pipeline call can reset the MCP connection. Save separately after the build returns.
            Debug.Log("[TRLM][Sprint3] Cave interior built + wired. Root='" + RootName + "'. Save + bake NavMesh next.");
        }

        // ---------- rooms ----------
        // x0,x1,z0,z1, floorY, ceilY
        private struct Room { public float x0, x1, z0, z1, fy, cy; public string name; }
        private static readonly Room A = new Room { name = "A_Threshold", x0 = 396, x1 = 405, z0 = 792, z1 = 806, fy = 224.0f, cy = 229.0f };
        private static readonly Room B = new Room { name = "B_Descent", x0 = 397, x1 = 404, z0 = 806, z1 = 818, fy = 223.7f, cy = 228.0f };
        private static readonly Room C = new Room { name = "C_Discovery", x0 = 393, x1 = 408, z0 = 818, z1 = 834, fy = 223.5f, cy = 231.0f };
        private static readonly Room D = new Room { name = "D_WeaponAlcove", x0 = 408, x1 = 415, z0 = 821, z1 = 829, fy = 223.5f, cy = 228.0f };
        private static readonly Room E = new Room { name = "E_Exit", x0 = 396, x1 = 401, z0 = 834, z1 = 842, fy = 223.6f, cy = 228.0f };

        private static void BuildOuterShell()
        {
            // A single light-tight enclosure OVER the varied interior ceilings, so inter-room
            // ceiling-height gaps and side gaps cannot leak sky/sun. Mouth (south) left open.
            const float minX = 392f, maxX = 416f, minZ = 792f, maxZ = 843f, baseY = 222.8f, roofY = 231.8f;
            float h = roofY - baseY, my = baseY + h / 2f;
            Box(_shell, "Outer_Roof", (minX + maxX) / 2f, roofY, (minZ + maxZ) / 2f, maxX - minX + 2f, 0.8f, maxZ - minZ + 2f, _mCeil);
            Box(_shell, "Outer_West", minX, my, (minZ + maxZ) / 2f, 0.8f, h, maxZ - minZ, _mCeil);
            Box(_shell, "Outer_East", maxX, my, (minZ + maxZ) / 2f, 0.8f, h, maxZ - minZ, _mCeil);
            Box(_shell, "Outer_North", (minX + maxX) / 2f, my, maxZ, maxX - minX, h, 0.8f, _mCeil);
            // South flanks around the mouth opening x[396,405]
            Box(_shell, "Outer_SouthW", (minX + 396f) / 2f, my, minZ, 396f - minX, h, 0.8f, _mCeil);
            Box(_shell, "Outer_SouthE", (405f + maxX) / 2f, my, minZ, maxX - 405f, h, 0.8f, _mCeil);
        }

        private static void BuildShell()
        {
            const float t = 0.6f;
            BuildOuterShell();
            foreach (var r in new[] { A, B, C, D, E })
            {
                float mx = (r.x0 + r.x1) / 2f, mz = (r.z0 + r.z1) / 2f, w = r.x1 - r.x0, d = r.z1 - r.z0, h = r.cy - r.fy;
                Box(_shell, r.name + "_Floor", mx, r.fy - 0.25f, mz, w, 0.5f, d, _mFloor);
                Box(_shell, r.name + "_Ceil", mx, r.cy, mz, w + 1f, t, d + 1f, _mCeil);
            }
            // Perimeter walls (explicit segments; gaps = doorways). WallZ = wall at fixed x running along z.
            // A: west/east full; south open (mouth); north split around B doorway (x397..404)
            WallZ(A, 396, A.z0, A.z1); WallZ(A, 405, A.z0, A.z1);
            WallX(A, 806, 396, 397); WallX(A, 806, 404, 405);
            // B: west/east; south open (to A); north open (to C)
            WallZ(B, 397, B.z0, B.z1); WallZ(B, 404, B.z0, B.z1);
            // C: west full; east split around D doorway (z821..829); south split around B doorway (x397..404); north split around E doorway (x396..401)
            WallZ(C, 393, C.z0, C.z1);
            WallZ(C, 408, 818, 821); WallZ(C, 408, 829, 834);
            WallX(C, 818, 393, 397); WallX(C, 818, 404, 408);
            WallX(C, 834, 393, 396); WallX(C, 834, 401, 408);
            // D: east/north/south; west open (to C)
            WallZ(D, 415, D.z0, D.z1); WallX(D, 821, 408, 415); WallX(D, 829, 408, 415);
            // E: west/east/north(cap); south open (to C)
            WallZ(E, 396, E.z0, E.z1); WallZ(E, 401, E.z0, E.z1); WallX(E, 842, 396, 401);

            // Decorative rock dressing along walls + floor rubble + ceiling nubs (silhouette in the dark)
            foreach (var r in new[] { A, B, C, D, E })
            {
                RockLine(r.name + "_RockW", r.x0 + 0.4f, r.z0 + 1f, r.z1 - 1f, r.fy, true);
                RockLine(r.name + "_RockE", r.x1 - 0.4f, r.z0 + 1f, r.z1 - 1f, r.fy, false);
                RockRubble(r.name + "_Rubble", (r.x0 + r.x1) / 2f, (r.z0 + r.z1) / 2f, (r.x1 - r.x0), (r.z1 - r.z0), r.fy, 4);
            }
            // Foreground framing rocks just inside the mouth + a rock ledge in the discovery chamber
            PlaceRock("Frame_L", 396.6f, A.fy, 793.5f, 2.4f);
            PlaceRock("Frame_R", 404.4f, A.fy, 793.5f, 2.6f);
            PlaceRock("PageLedge", 400f, C.fy, 827f, 1.4f);
            // Blocked "deeper cave" foreshadow at the exit cap
            PlaceRock("DeepBlock_1", 398f, E.fy, 841f, 2.0f);
            PlaceRock("DeepBlock_2", 399.4f, E.fy, 841.3f, 1.7f);
        }

        private static void BuildLighting()
        {
            var lroot = new GameObject("Lighting").transform; lroot.SetParent(_play, false);
            // Daylight spill at the mouth (cool), gives the "outside bright -> adapt" read
            AddLight(lroot, "L_MouthGlow", 400, 226.6f, 795, new Color(0.72f, 0.80f, 0.95f), 2.4f, 12f, LightType.Point);
            // Minimal fill so the descent is navigable, not black
            AddLight(lroot, "L_DescentFill", 400.5f, 226f, 812, new Color(0.55f, 0.62f, 0.78f), 1.3f, 11f, LightType.Point);
            // Soft ambient lift in the discovery chamber so it reads as "dark but playable", not black
            AddLight(lroot, "L_ChamberFill", 400f, 227.2f, 824, new Color(0.5f, 0.58f, 0.72f), 0.8f, 15f, LightType.Point);
            // Key readable light in the discovery chamber — a warm "shaft" spotlight from a ceiling crack
            var shaft = AddLight(lroot, "L_ChamberShaft", 400, 230.2f, 824, new Color(0.98f, 0.88f, 0.66f), 3.4f, 16f, LightType.Spot);
            shaft.transform.rotation = Quaternion.Euler(90f, 0f, 0f); shaft.spotAngle = 46f; shaft.innerSpotAngle = 24f;
            // Small warm accent on the prophecy page ledge
            AddLight(lroot, "L_PageAccent", 400, 224.9f, 827, new Color(1f, 0.86f, 0.6f), 1.1f, 3.5f, LightType.Point);
            // Dim practical over the weapon cache alcove
            AddLight(lroot, "L_WeaponDim", 411.5f, 225.4f, 825, new Color(0.95f, 0.8f, 0.62f), 1.0f, 5.5f, LightType.Point);
            // Cold hint deeper in / at the exit continuation
            AddLight(lroot, "L_ExitHint", 398.5f, 225.2f, 838, new Color(0.5f, 0.62f, 0.8f), 0.55f, 6f, LightType.Point);
        }

        private static void BuildAudio()
        {
            var aroot = new GameObject("Audio").transform; aroot.SetParent(_play, false);
            // Cave reverb across the whole interior
            var rev = new GameObject("CaveReverbZone"); rev.transform.SetParent(aroot, false);
            rev.transform.position = new Vector3(400, 225.5f, 822);
            var rz = rev.AddComponent<AudioReverbZone>();
            rz.reverbPreset = AudioReverbPreset.Cave; rz.minDistance = 8f; rz.maxDistance = 30f;
            // Low interior ambience (3D so it fades near the mouth). Reuse existing wind loop as a hollow drone.
            var amb = new GameObject("CaveAmbience"); amb.transform.SetParent(aroot, false);
            amb.transform.position = new Vector3(400, 225.5f, 824);
            var src = amb.AddComponent<AudioSource>();
            var clip = FindClip("SFX_WIND_GUST_LOOP") ?? FindClip("SFX_FOREST_NIGHT_LOOP");
            src.clip = clip; src.loop = true; src.playOnAwake = true; src.volume = 0.28f;
            src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 4f; src.maxDistance = 26f;
        }

        private static void BuildEnterTrigger()
        {
            var go = new GameObject("CaveEnterTrigger"); go.transform.SetParent(_play, false);
            go.transform.position = new Vector3(400, 225.5f, 800);
            var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(9f, 4f, 3f);
            var ret = go.AddComponent<RegionEntryTrigger>();
            Set(ret, "step", (int)ObjectiveStep.EnterCave);
            Set(ret, "playerTag", "Player");
            Set(ret, "regionName", "Cave Interior");
            // Keep the companion squad holding at the mouth (solo discovery beat) via existing command API.
            go.AddComponent<CompanionCaveGate>();
        }

        private static void RelocatePage()
        {
            // Guarantee exactly ONE page pickup: destroy the original (at the mouth) or any prior copy,
            // then recreate a single instance on the ledge inside the discovery chamber. Recreating each
            // build keeps this idempotent (the previous approach parented it under the root, so a rebuild
            // destroyed it). PersistentObjectId is auto-added and stable within a session.
            foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
                if (tr.name == "ProphecyPagePickup_TornFirstLeaf" && tr.hideFlags == HideFlags.None && tr.gameObject.scene.IsValid())
                    Object.DestroyImmediate(tr.gameObject);

            var page = AssetDatabase.LoadAssetAtPath<ProphecyPage>(PagePath);
            var go = new GameObject("ProphecyPagePickup_TornFirstLeaf");
            go.transform.SetParent(_play, false);
            go.transform.position = new Vector3(400f, 224.85f, 827f);
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "PageVisual"; vis.transform.SetParent(go.transform, false);
            vis.transform.localPosition = Vector3.zero; vis.transform.localScale = new Vector3(0.34f, 0.02f, 0.24f);
            vis.transform.localRotation = Quaternion.Euler(0f, 18f, 4f);
            Object.DestroyImmediate(vis.GetComponent<BoxCollider>());
            var col = go.AddComponent<BoxCollider>(); col.size = new Vector3(0.6f, 0.5f, 0.6f); col.center = new Vector3(0, 0.15f, 0);
            var pickup = go.AddComponent<ProphecyPagePickup>();
            Set(pickup, "page", page);
        }

        private static void BuildWeaponCache()
        {
            var go = new GameObject("WeaponCache_Shotgun"); go.transform.SetParent(_play, false);
            go.transform.position = new Vector3(411.5f, 224.0f, 825f);
            // crate visual
            Box(go.transform, "CacheCrate", 411.5f, 223.9f, 825f, 0.9f, 0.7f, 0.6f, _mWall);
            var gun = GameObject.CreatePrimitive(PrimitiveType.Cube); gun.name = "CacheShotgun_Placeholder";
            gun.transform.SetParent(go.transform, false); gun.transform.position = new Vector3(411.5f, 224.35f, 825f);
            gun.transform.localScale = new Vector3(0.09f, 0.09f, 0.9f); gun.transform.rotation = Quaternion.Euler(0, 20, 0);
            Object.DestroyImmediate(gun.GetComponent<BoxCollider>());
            // interaction collider on the cache root
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.4f, 0); col.size = new Vector3(1.2f, 1.0f, 1.0f);
            var cache = go.AddComponent<CaveWeaponCache>();
            Set(cache, "weapon", AssetDatabase.LoadAssetAtPath<Object>(ShotgunPath));
            Set(cache, "ammoItem", AssetDatabase.LoadAssetAtPath<Object>(AmmoPath));
            Set(cache, "ammoCount", 12);
            Set(cache, "takenFlag", "cave_weapon_taken");
            Set(cache, "promptText", "Take the shotgun");
        }

        private static void BuildThresholdAndContinuation()
        {
            var go = new GameObject("CaveThresholdTrigger"); go.transform.SetParent(_play, false);
            go.transform.position = new Vector3(398.5f, 225.5f, 839f);
            var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(4.5f, 4f, 3f);
            var ctt = go.AddComponent<CaveThresholdTrigger>();
            Set(ctt, "targetStep", (int)ObjectiveStep.CaveThresholdComplete);
            Set(ctt, "minimumStep", (int)ObjectiveStep.RecoverFirstProphecyPage);
            Set(ctt, "playerTag", "Player");
            Set(ctt, "setFlag", "cave_threshold_complete");
        }

        private static void ConfigureNotebook()
        {
            var nb = Object.FindFirstObjectByType<ProphecyNotebook>();
            if (nb == null) { Debug.LogWarning("[TRLM][Sprint3] ProphecyNotebook not found."); return; }
            Set(nb, "advanceObjectiveOnKeyProphecy", true);
            Set(nb, "keyProphecyObjective", (int)ObjectiveStep.RecoverFirstProphecyPage);
            // ensure the seed page is registered in allPages
            var page = AssetDatabase.LoadAssetAtPath<Object>(PagePath);
            var so = new SerializedObject(nb);
            var list = so.FindProperty("allPages");
            bool has = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == page) { has = true; break; }
            if (!has && page != null)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = page;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ReframeCinematic()
        {
            var cam = FindInactiveByName("CM_CaveReveal");
            if (cam == null) { Debug.LogWarning("[TRLM][Sprint3] CM_CaveReveal not found — cinematic reframe skipped."); return; }
            // Frame the dark mouth silhouette from a low approach angle, looking up into the cave.
            cam.transform.position = new Vector3(400f, 224.6f, 780.5f);
            cam.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
        }

        // ---------- helpers ----------
        private static void WallZ(Room r, float x, float z0, float z1)
        {
            float mz = (z0 + z1) / 2f, h = r.cy - r.fy;
            Box(_shell, r.name + "_WallZ" + Mathf.RoundToInt(x) + "_" + Mathf.RoundToInt(mz), x, r.fy + h / 2f, mz, 0.6f, h + 1f, z1 - z0, _mWall);
        }
        private static void WallX(Room r, float z, float x0, float x1)
        {
            float mx = (x0 + x1) / 2f, h = r.cy - r.fy;
            Box(_shell, r.name + "_WallX" + Mathf.RoundToInt(z) + "_" + Mathf.RoundToInt(mx), mx, r.fy + h / 2f, z, x1 - x0, h + 1f, 0.6f, _mWall);
        }

        private static GameObject Box(Transform parent, string name, float cx, float cy, float cz, float sx, float sy, float sz, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(cx, cy, cz);
            go.transform.localScale = new Vector3(sx, sy, sz);
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static void RockLine(string name, float x, float z0, float z1, float fy, bool west)
        {
            var parent = new GameObject(name).transform; parent.SetParent(_shell, false);
            int n = Mathf.Max(2, Mathf.RoundToInt((z1 - z0) / 2.2f));
            for (int i = 0; i <= n; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)n);
                float jitter = (Rand() - 0.5f) * 0.6f;
                var go = PlaceRock(name + "_" + i, x + jitter, fy + (Rand() - 0.3f) * 0.6f, z, 1.6f + Rand() * 1.4f);
                if (go != null) go.transform.rotation = Quaternion.Euler(Rand() * 30f, Rand() * 360f, Rand() * 30f);
            }
        }

        private static void RockRubble(string name, float mx, float mz, float w, float d, float fy, int count)
        {
            var parent = new GameObject(name).transform; parent.SetParent(_shell, false);
            for (int i = 0; i < count; i++)
            {
                float x = mx + (Rand() - 0.5f) * (w - 2f);
                float z = mz + (Rand() - 0.5f) * (d - 2f);
                var go = PlaceRock(name + "_" + i, x, fy - 0.1f, z, 0.5f + Rand() * 0.7f);
                if (go != null) go.transform.rotation = Quaternion.Euler(Rand() * 360f, Rand() * 360f, Rand() * 360f);
            }
        }

        private static GameObject PlaceRock(string name, float x, float y, float z, float scale)
        {
            int idx = 1 + Mathf.Abs((int)(Rand() * 100)) % 11;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(RockFmt, idx));
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name; go.transform.SetParent(_shell, false);
            go.transform.position = new Vector3(x, y, z);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static Light AddLight(Transform parent, string name, float x, float y, float z, Color c, float intensity, float range, LightType type)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, z);
            var l = go.AddComponent<Light>();
            l.type = type; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None; // perf: cave uses many small lights, keep them shadowless
            return l;
        }

        private static AudioClip FindClip(string namePart)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip " + namePart))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (c != null) return c;
            }
            return null;
        }

        private static GameObject FindInactiveByName(string name)
        {
            foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
                if (tr.name == name && tr.hideFlags == HideFlags.None && tr.gameObject.scene.IsValid())
                    return tr.gameObject;
            return null;
        }

        // Generic serialized-field setter (private [SerializeField] fields), contiguous enums use int index.
        private static void Set(Object comp, string prop, object val)
        {
            var so = new SerializedObject(comp);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning("[TRLM][Sprint3] missing prop '" + prop + "' on " + comp.GetType().Name); return; }
            switch (val)
            {
                case int i when p.propertyType == SerializedPropertyType.Enum: p.enumValueIndex = i; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
                case Object o: p.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float Rand()
        {
            _rng = (_rng * 1103515245 + 12345) & 0x7fffffff;
            return (_rng % 10000) / 10000f;
        }
    }
}
