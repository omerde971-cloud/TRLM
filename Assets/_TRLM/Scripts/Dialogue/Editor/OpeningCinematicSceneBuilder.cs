using TRLM.Dialogue;
using TRLM.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TRLM.EditorTools
{
    public static class OpeningCinematicSceneBuilder
    {
        private const string ScenePath = "Assets/_TRLM/Scenes/Production/05_Neighborhood_Cinematic.unity";

        [MenuItem("TRLM/Build Sprint 11 Opening Cinematic")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath);

            var root = GameObject.Find("NEIGHBORHOOD_CINEMATIC");
            if (root == null)
                root = new GameObject("NEIGHBORHOOD_CINEMATIC");

            var systems = FindOrCreate("CinematicSystems", root.transform);
            var dialogueSystem = systems.GetComponent<DialogueSystem>() ?? systems.AddComponent<DialogueSystem>();

            var camera = FindOrCreateCamera("OpeningCinematicCamera", root.transform);
            var controller = systems.GetComponent<OpeningCinematicController>() ?? systems.AddComponent<OpeningCinematicController>();
            ConfigureController(controller, camera);
            ConfigureCinematicProps();
            EnsureSubtitleUi(dialogueSystem);
            DisablePlaceholderPreparation();

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(systems);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TRLM] Sprint 11 opening cinematic controller built.");
        }

        private static void ConfigureController(OpeningCinematicController controller, Camera camera)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("cinematicCamera").objectReferenceValue = camera;
            so.FindProperty("islandSceneName").stringValue = "20_Island_Blockout";
            so.FindProperty("playOnStart").boolValue = true;
            so.FindProperty("elias").objectReferenceValue = FindTransform("Elias");
            so.FindProperty("mira").objectReferenceValue = FindTransform("Mira");
            so.FindProperty("jonah").objectReferenceValue = FindTransform("Jonah");
            so.FindProperty("lena").objectReferenceValue = FindTransform("Lena");
            so.FindProperty("noah").objectReferenceValue = FindTransform("Noah");
            so.FindProperty("gearFocus").objectReferenceValue = FindTransform("EquipmentLoadPoint") ?? FindTransform("Gear_DuffelBag_01");
            so.FindProperty("mapFocus").objectReferenceValue = FindTransform("Porch_Roof") ?? FindTransform("House_Facade");
            so.FindProperty("boatFocus").objectReferenceValue = FindTransform("DeparturePoint") ?? FindTransform("Rowboat");

            var beats = so.FindProperty("beats");
            beats.arraySize = 7;
            SetBeat(beats.GetArrayElementAtIndex(0), "wide_establish", FindTransform("Cam_01_WideEstablish"), 11f, Lines(
                Line("s11_open_001", DialogueSpeaker.Lena, "Herkes burada mı? Kamerayı açmadım, söz.", "Everyone here? I swear the camera is off.", DialogueEmotion.Playful, "quiet teasing", "wide_establish"),
                Line("s11_open_002", DialogueSpeaker.Jonah, "Lena, kamerayı sakladığın yüzünden belli oluyor.", "Lena, your hiding-the-camera face is terrible.", DialogueEmotion.Warm, "dry joke", "wide_establish")));
            SetBeat(beats.GetArrayElementAtIndex(1), "gear_load", FindTransform("Cam_02_LoadingCloseup"), 13f, Lines(
                Line("s11_open_003", DialogueSpeaker.Noah, "Rota kısa değil. Fazla çanta alırsak kıyıda kalırız.", "The route is not short. Too many bags and we lose time on the shore.", DialogueEmotion.Focused, "practical", "gear_load"),
                Line("s11_open_004", DialogueSpeaker.Elias, "O zaman Mira'nın taş koleksiyonunu burada bırakıyoruz.", "Then Mira's rock collection stays here.", DialogueEmotion.Playful, "soft joke", "gear_load"),
                Line("s11_open_005", DialogueSpeaker.Mira, "Onlar örnek. Ve ikisi hayatımızı kurtarabilir.", "They are samples. And two of them might save our lives.", DialogueEmotion.Determined, "matter-of-fact", "gear_load")));
            SetBeat(beats.GetArrayElementAtIndex(2), "symbol_book", FindTransform("Cam_03_PorchFriends"), 14f, Lines(
                Line("s11_open_006", DialogueSpeaker.Mira, "Bu işaret aynı dağa çıkıyor. Haritadaki eski patikayla örtüşüyor.", "This symbol points to the same mountain. It lines up with the old trail on the map.", DialogueEmotion.Focused, "low certainty", "symbol_book"),
                Line("s11_open_007", DialogueSpeaker.Jonah, "Eski patikalar genelde bir sebepten eski kalır.", "Old trails usually stay old for a reason.", DialogueEmotion.Uneasy, "protective", "symbol_book")));
            SetBeat(beats.GetArrayElementAtIndex(3), "secrecy", FindTransform("Cam_01_WideEstablish"), 13f, Lines(
                Line("s11_open_008", DialogueSpeaker.Elias, "Kimseye söylemedik. Telefonlar kapalı. Kıyıya indik mi geri dönüş zor.", "We told no one. Phones off. Once we reach the shore, turning back gets hard.", DialogueEmotion.Uneasy, "steady but honest", "secrecy"),
                Line("s11_open_009", DialogueSpeaker.Noah, "Geri dönüş için değil, doğru dönüş için hazırlanıyoruz.", "We are not preparing to turn back. We are preparing to return right.", DialogueEmotion.Determined, "calm guide", "secrecy")));
            SetBeat(beats.GetArrayElementAtIndex(4), "friends", FindTransform("Cam_03_PorchFriends"), 12f, Lines(
                Line("s11_open_010", DialogueSpeaker.Lena, "Elias bunu çocukken anlattığında daha az yasadışı gelmişti.", "When Elias told this story as a kid, it sounded less illegal.", DialogueEmotion.Warm, "fond teasing", "friends"),
                Line("s11_open_011", DialogueSpeaker.Elias, "Çocukken daha iyi yalan söylüyordum.", "I was better at lying as a kid.", DialogueEmotion.Playful, "small smile", "friends")));
            SetBeat(beats.GetArrayElementAtIndex(5), "unease", FindTransform("Cam_02_LoadingCloseup"), 14f, Lines(
                Line("s11_open_012", DialogueSpeaker.Mira, "Efsane yanlış olabilir. İşaretler yanlış değil.", "The legend may be wrong. The symbols are not.", DialogueEmotion.Focused, "quiet conviction", "unease"),
                Line("s11_open_013", DialogueSpeaker.Jonah, "Beni asıl korkutan da bu.", "That is exactly what worries me.", DialogueEmotion.Nervous, "under breath", "unease")));
            SetBeat(beats.GetArrayElementAtIndex(6), "departure", FindTransform("Cam_01_WideEstablish"), 12f, Lines(
                Line("s11_open_014", DialogueSpeaker.Noah, "Gelgit dönmeden çıkarsak sabaha adadayız.", "If we leave before the tide turns, we reach the island by morning.", DialogueEmotion.Urgent, "route-aware", "departure"),
                Line("s11_open_015", DialogueSpeaker.Lena, "Tamam. Kayıtta değilim. Ama bunu unutmayacağım.", "Okay. I am not recording. But I am not forgetting this.", DialogueEmotion.Warm, "soft", "departure")));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DialogueLine[] Lines(params DialogueLine[] lines) => lines;

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
                scene = "05_Neighborhood_Cinematic",
                trigger = trigger,
                subtitlePriority = SubtitlePriority.Cinematic,
                durationOverride = Mathf.Clamp(en.Length / 15f + 0.8f, 3.0f, 7.5f)
            };
        }

        private static void SetBeat(SerializedProperty beat, string id, Transform camera, float hold, DialogueLine[] lines)
        {
            beat.FindPropertyRelative("id").stringValue = id;
            beat.FindPropertyRelative("cameraMarker").objectReferenceValue = camera;
            beat.FindPropertyRelative("holdSeconds").floatValue = hold;
            var lineArray = beat.FindPropertyRelative("lines");
            lineArray.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lineArray.GetArrayElementAtIndex(i);
                line.FindPropertyRelative("id").stringValue = lines[i].id;
                line.FindPropertyRelative("speaker").enumValueIndex = (int)lines[i].speaker;
                line.FindPropertyRelative("turkishText").stringValue = lines[i].turkishText;
                line.FindPropertyRelative("englishSubtitle").stringValue = lines[i].englishSubtitle;
                line.FindPropertyRelative("emotion").enumValueIndex = (int)lines[i].emotion;
                line.FindPropertyRelative("delivery").stringValue = lines[i].delivery;
                line.FindPropertyRelative("scene").stringValue = lines[i].scene;
                line.FindPropertyRelative("trigger").stringValue = lines[i].trigger;
                line.FindPropertyRelative("durationOverride").floatValue = lines[i].durationOverride;
                line.FindPropertyRelative("subtitlePriority").intValue = (int)lines[i].subtitlePriority;
                line.FindPropertyRelative("oneShot").boolValue = false;
            }
        }

        private static void EnsureSubtitleUi(DialogueSystem dialogueSystem)
        {
            var canvas = GameObject.Find("SubtitleCanvas") ?? new GameObject("SubtitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = canvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 80;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panel = GameObject.Find("SubtitlePanel") ?? new GameObject("SubtitlePanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(TRLM.UI.SubtitleUI));
            panel.transform.SetParent(canvas.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0f);
            panelRt.anchorMax = new Vector2(0.5f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = new Vector2(0f, 70f);
            panelRt.sizeDelta = new Vector2(1180f, 128f);
            panel.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.028f, 0.86f);

            var speaker = FindOrCreateText("Speaker", panel.transform, new Vector2(0f, 0.58f), new Vector2(1f, 1f), 20, TextAnchor.MiddleCenter);
            speaker.color = new Color(0.95f, 0.88f, 0.72f, 1f);
            AddReadableOutline(speaker, new Vector2(1.4f, -1.4f));
            var line = FindOrCreateText("Line", panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.72f), 32, TextAnchor.MiddleCenter);
            line.color = Color.white;
            line.fontStyle = FontStyle.Bold;
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            AddReadableOutline(line, new Vector2(1.8f, -1.8f));

            var subtitle = panel.GetComponent<TRLM.UI.SubtitleUI>();
            var so = new SerializedObject(subtitle);
            so.FindProperty("dialogueSystem").objectReferenceValue = dialogueSystem;
            so.FindProperty("speakerText").objectReferenceValue = speaker;
            so.FindProperty("lineText").objectReferenceValue = line;
            so.FindProperty("backing").objectReferenceValue = panel.GetComponent<Image>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCinematicProps()
        {
            var rowboat = GameObject.Find("Rowboat_OnTrailer");
            if (rowboat == null) return;

            rowboat.transform.SetPositionAndRotation(new Vector3(-6f, 1.3f, -1.5f), Quaternion.Euler(0f, 15f, 0f));

            var rb = rowboat.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                EditorUtility.SetDirty(rb);
            }

            var controller = rowboat.GetComponent<TRLM.Boat.RowboatController>();
            if (controller != null)
            {
                controller.enabled = false;
                EditorUtility.SetDirty(controller);
            }
        }

        private static Text FindOrCreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, int size, TextAnchor alignment)
        {
            var child = parent.Find(name)?.gameObject ?? new GameObject(name, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(parent, false);
            var rt = child.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(42f, 8f);
            rt.offsetMax = new Vector2(-42f, -8f);

            var text = child.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void AddReadableOutline(Text text, Vector2 distance)
        {
            var outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static void DisablePlaceholderPreparation()
        {
            var prep = Object.FindFirstObjectByType<PreparationSequence>();
            if (prep != null) prep.enabled = false;
        }

        private static Camera FindOrCreateCamera(string name, Transform parent)
        {
            var go = GameObject.Find(name) ?? new GameObject(name, typeof(Camera), typeof(AudioListener));
            go.transform.SetParent(parent, false);
            var camera = go.GetComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 250f;
            return camera;
        }

        private static Transform FindTransform(string name) => GameObject.Find(name)?.transform;

        private static GameObject FindOrCreate(string name, Transform parent)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
