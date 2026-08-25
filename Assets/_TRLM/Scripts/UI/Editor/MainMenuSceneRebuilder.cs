using System;
using System.IO;
using TRLM.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TRLM.EditorTools
{
    public static class MainMenuSceneRebuilder
    {
        private const string ScenePath = "Assets/_TRLM/Scenes/Production/00_MainMenu.unity";
        private const string BackgroundPath = "Assets/_TRLM/UI/MainMenu/MainMenu_Background.png";
        private const string VignettePath = "Assets/_TRLM/UI/MainMenu/MM_Vignette.png";
        private const string ScanlinePath = "Assets/_TRLM/UI/MainMenu/MM_Scanlines.png";
        private const string TopoPath = "Assets/_TRLM/UI/MainMenu/MM_TopoDots.png";
        private const string PanelPath = "Assets/_TRLM/UI/MainMenu/MM_PanelFalloff.png";

        [MenuItem("TRLM/Rebuild Stitch Main Menu")]
        public static void Rebuild()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            BuildGeneratedSprites();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvas = GameObject.Find("Canvas") ?? new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var unityCanvas = canvas.GetComponent<Canvas>();
            unityCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            unityCanvas.sortingOrder = 0;

            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(canvas.transform.GetChild(i).gameObject);
            }

            var backgroundRoot = Ui("BackgroundRoot", canvas.transform);
            Stretch(backgroundRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var background = Ui("Background", backgroundRoot.transform, typeof(Image), typeof(MainMenuCoverImage), typeof(MainMenuAtmosphere));
            Stretch(background.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            backgroundImage.preserveAspect = true;
            backgroundImage.raycastTarget = false;

            var cover = background.GetComponent<MainMenuCoverImage>();
            var coverSo = new SerializedObject(cover);
            coverSo.FindProperty("viewport").objectReferenceValue = backgroundRoot.GetComponent<RectTransform>();
            coverSo.FindProperty("cropPivotX").floatValue = 1f;
            coverSo.FindProperty("cropPivotY").floatValue = 0.5f;
            coverSo.ApplyModifiedPropertiesWithoutUndo();

            var atmosphere = background.GetComponent<MainMenuAtmosphere>();
            var atmosphereSo = new SerializedObject(atmosphere);
            atmosphereSo.FindProperty("background").objectReferenceValue = background.GetComponent<RectTransform>();
            atmosphereSo.FindProperty("panAmplitudePixels").floatValue = 8f;
            atmosphereSo.FindProperty("panPeriodSeconds").floatValue = 70f;
            atmosphereSo.FindProperty("scaleAmplitude").floatValue = 0.006f;
            atmosphereSo.FindProperty("scalePeriodSeconds").floatValue = 90f;
            atmosphereSo.ApplyModifiedPropertiesWithoutUndo();

            AddFullImage("ColdMultiplyWash", backgroundRoot.transform, ColorFromHex("#121416", 0.20f), null);
            AddFullImage("Vignette", backgroundRoot.transform, Color.white, AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath));

            var panel = Ui("SideNav", canvas.transform, typeof(Image));
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(400f, 0f);
            panelRt.localScale = Vector3.one;
            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;
            panelImage.raycastTarget = true;

            var topo = AddFullImage("TopoTexture", panel.transform, Color.white, AssetDatabase.LoadAssetAtPath<Sprite>(TopoPath));
            topo.type = Image.Type.Tiled;

            var border = Ui("RightBorder", panel.transform, typeof(Image));
            var borderRt = border.GetComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(1f, 0f);
            borderRt.anchorMax = new Vector2(1f, 1f);
            borderRt.pivot = new Vector2(1f, 0.5f);
            borderRt.anchoredPosition = Vector2.zero;
            borderRt.sizeDelta = new Vector2(1f, 0f);
            border.GetComponent<Image>().color = ColorFromHex("#434845", 0.22f);

            var mark = Ui("TRLMMark", panel.transform);
            var markRt = mark.GetComponent<RectTransform>();
            markRt.anchorMin = new Vector2(0f, 1f);
            markRt.anchorMax = new Vector2(1f, 1f);
            markRt.pivot = new Vector2(0f, 1f);
            markRt.offsetMin = new Vector2(40f, -178f);
            markRt.offsetMax = new Vector2(-40f, -112f);
            AddText(mark, "T  R  L  M", 43, ColorFromHex("#BFC9C2", 0.94f), TextAnchor.MiddleLeft, font);

            var mainGroup = Ui("MainActions", panel.transform);
            var mainGroupRt = mainGroup.GetComponent<RectTransform>();
            mainGroupRt.anchorMin = new Vector2(0f, 0.5f);
            mainGroupRt.anchorMax = new Vector2(1f, 0.5f);
            mainGroupRt.pivot = new Vector2(0.5f, 0.5f);
            mainGroupRt.anchoredPosition = new Vector2(0f, -6f);
            mainGroupRt.sizeDelta = new Vector2(-80f, 220f);

            var continueButton = CreateButton("ContinueButton", mainGroup.transform, ">", "CONTINUE", false, 0f, 45f, font);
            var newGameButton = CreateButton("NewGameButton", mainGroup.transform, "+", "NEW GAME", false, -58f, 45f, font);
            var loadGameButton = CreateButton("LoadGameButton", mainGroup.transform, "[]", "LOAD GAME", false, -116f, 45f, font);
            var settingsButton = CreateButton("SettingsButton", mainGroup.transform, "*", "SETTINGS", false, -174f, 45f, font);

            var footer = Ui("FooterActions", panel.transform);
            var footerRt = footer.GetComponent<RectTransform>();
            footerRt.anchorMin = new Vector2(0f, 0f);
            footerRt.anchorMax = new Vector2(1f, 0f);
            footerRt.pivot = new Vector2(0.5f, 0f);
            footerRt.offsetMin = new Vector2(40f, 40f);
            footerRt.offsetMax = new Vector2(-40f, 156f);

            var footerRule = Ui("FooterRule", footer.transform, typeof(Image));
            var footerRuleRt = footerRule.GetComponent<RectTransform>();
            footerRuleRt.anchorMin = new Vector2(0f, 1f);
            footerRuleRt.anchorMax = new Vector2(1f, 1f);
            footerRuleRt.pivot = new Vector2(0.5f, 1f);
            footerRuleRt.anchoredPosition = Vector2.zero;
            footerRuleRt.sizeDelta = new Vector2(0f, 1f);
            footerRule.GetComponent<Image>().color = ColorFromHex("#434845", 0.16f);

            var creditsButton = CreateButton("CreditsButton", footer.transform, "i", "CREDITS", true, -30f, 34f, font);
            var quitButton = CreateButton("QuitButton", footer.transform, "|", "QUIT GAME", true, -90f, 34f, font);

            var loadPanel = CreatePanel("LoadGamePanel", "LOAD GAME", string.Empty, canvas.transform, font);
            var slotRows = BuildSlotRows(loadPanel.transform, font);
            var loadBackButton = MakeBackButton(loadPanel.transform, font);

            var settingsPanel = CreatePanel("SettingsPanel", "SETTINGS", "Audio, graphics, gameplay, controls, and accessibility settings are staged here for Sprint integration. This panel preserves the flow without pretending the full settings stack exists yet.", canvas.transform, font);
            var settingsBackButton = MakeBackButton(settingsPanel.transform, font);

            var creditsPanel = CreatePanel("CreditsPanel", "CREDITS", "THE ROAD LEADING TO THE MOUNTAIN\n\nA cold survival expedition by the TRLM team.\n\nMain menu visual target: Stitch / Obsidian Path.", canvas.transform, font);
            var creditsBackButton = MakeBackButton(creditsPanel.transform, font);

            var scanlines = AddFullImage("ScanlineOverlay", canvas.transform, Color.white, AssetDatabase.LoadAssetAtPath<Sprite>(ScanlinePath));
            scanlines.type = Image.Type.Tiled;

            var controllerGo = GameObject.Find("MenuController") ?? new GameObject("MenuController");
            var controller = controllerGo.GetComponent<MainMenuController>() ?? controllerGo.AddComponent<MainMenuController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("continueButton").objectReferenceValue = continueButton;
            controllerSo.FindProperty("newGameButton").objectReferenceValue = newGameButton;
            controllerSo.FindProperty("loadGameButton").objectReferenceValue = loadGameButton;
            controllerSo.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            controllerSo.FindProperty("creditsButton").objectReferenceValue = creditsButton;
            controllerSo.FindProperty("quitButton").objectReferenceValue = quitButton;
            controllerSo.FindProperty("loadGamePanel").objectReferenceValue = loadPanel.GetComponent<CanvasGroup>();
            controllerSo.FindProperty("settingsPanel").objectReferenceValue = settingsPanel.GetComponent<CanvasGroup>();
            controllerSo.FindProperty("creditsPanel").objectReferenceValue = creditsPanel.GetComponent<CanvasGroup>();
            controllerSo.FindProperty("loadGameBackButton").objectReferenceValue = loadBackButton;
            controllerSo.FindProperty("settingsBackButton").objectReferenceValue = settingsBackButton;
            controllerSo.FindProperty("creditsBackButton").objectReferenceValue = creditsBackButton;
            controllerSo.FindProperty("slotRowsContainer").objectReferenceValue = slotRows.transform;
            controllerSo.FindProperty("newGameSceneName").stringValue = "20_Island_Blockout";
            controllerSo.FindProperty("fallbackContinueSceneName").stringValue = "20_Island_Blockout";
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            LinkVertical(continueButton, settingsButton, newGameButton);
            LinkVertical(newGameButton, continueButton, loadGameButton);
            LinkVertical(loadGameButton, newGameButton, settingsButton);
            LinkVertical(settingsButton, loadGameButton, continueButton);
            LinkVertical(creditsButton, quitButton, quitButton);
            LinkVertical(quitButton, creditsButton, creditsButton);

            var eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
            if (eventSystem != null)
            {
                eventSystem.firstSelectedGameObject = continueButton.gameObject;
            }

            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(controllerGo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TRLM] Rebuilt Stitch-aligned main menu scene.");
        }

        private static GameObject BuildSlotRows(Transform parent, Font font)
        {
            var slotRows = Ui("SlotRows", parent);
            var rt = slotRows.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(40f, 110f);
            rt.offsetMax = new Vector2(-40f, -124f);

            var layout = slotRows.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            for (int i = 1; i <= 6; i++)
            {
                var slot = Ui("Slot" + i, slotRows.transform, typeof(Image), typeof(Button), typeof(LayoutElement));
                var image = slot.GetComponent<Image>();
                image.color = ColorFromHex("#E2E2E5", 0.06f);
                image.raycastTarget = true;

                var button = slot.GetComponent<Button>();
                button.targetGraphic = image;
                SetButtonColors(button, ColorFromHex("#ECE1D1", 0.92f), ColorFromHex("#CFC5B5", 0.9f), ColorFromHex("#C3C8C3", 0.26f));

                slot.GetComponent<LayoutElement>().preferredHeight = 52f;

                var label = Ui("Label", slot.transform);
                Stretch(label.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));
                AddText(label, i == 1 ? "AUTOSAVE" : "SLOT " + (i - 1), 15, ColorFromHex("#E2E2E5", 0.92f), TextAnchor.MiddleLeft, font, FontStyle.Bold);

                var detail = Ui("Detail", slot.transform);
                Stretch(detail.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(135f, 0f), new Vector2(-18f, 0f));
                AddText(detail, "EMPTY", 13, ColorFromHex("#C3C8C3", 0.58f), TextAnchor.MiddleRight, font);
            }

            return slotRows;
        }

        private static GameObject CreatePanel(string name, string title, string body, Transform parent, Font font)
        {
            var panel = Ui(name, parent, typeof(Image), typeof(CanvasGroup));
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(430f, 0f);
            rt.sizeDelta = new Vector2(560f, -160f);

            var image = panel.GetComponent<Image>();
            image.color = ColorFromHex("#0C0E10", 0.88f);
            image.raycastTarget = true;

            var titleGo = Ui("Title", panel.transform);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(40f, -102f);
            titleRt.offsetMax = new Vector2(-40f, -42f);
            AddText(titleGo, title, 28, ColorFromHex("#ECE1D1", 0.96f), TextAnchor.MiddleLeft, font);

            var bodyGo = Ui("Body", panel.transform);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(40f, 96f);
            bodyRt.offsetMax = new Vector2(-40f, -128f);
            var bodyText = AddText(bodyGo, body, 16, ColorFromHex("#C3C8C3", 0.72f), TextAnchor.UpperLeft, font);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Truncate;

            panel.SetActive(false);
            return panel;
        }

        private static Button MakeBackButton(Transform parent, Font font)
        {
            var button = CreateButton("BackButton", parent, "<", "BACK", true, -20f, 36f, font);
            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(40f, 34f);
            rt.sizeDelta = new Vector2(160f, 36f);
            return button;
        }

        private static Button CreateButton(string name, Transform parent, string icon, string label, bool secondary, float y, float height, Font font)
        {
            var row = Ui(name, parent, typeof(Image), typeof(Button), typeof(MainMenuSelectableStyle));
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, y - height);
            rt.offsetMax = new Vector2(0f, y);

            var image = row.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            var button = row.GetComponent<Button>();
            button.targetGraphic = image;
            SetButtonColors(button, ColorFromHex("#ECE1D1", 1f), ColorFromHex("#CFC5B5", 1f), ColorFromHex("#C3C8C3", 0.42f));

            var content = Ui("Content", row.transform);
            Stretch(content.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var iconGo = Ui("Icon", content.transform);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(8f, 0f);
            iconRt.sizeDelta = new Vector2(24f, 24f);
            var iconText = AddText(iconGo, icon, secondary ? 17 : 20, ColorFromHex("#C3C8C3", secondary ? 0.50f : 0.60f), TextAnchor.MiddleCenter, font);

            var labelGo = Ui("Label", content.transform);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(40f, 0f);
            labelRt.offsetMax = new Vector2(-28f, 0f);
            var labelText = AddText(labelGo, label, secondary ? 13 : 17, ColorFromHex("#C3C8C3", secondary ? 0.50f : 0.60f), TextAnchor.MiddleLeft, font, secondary ? FontStyle.Normal : FontStyle.Bold);

            var underline = Ui("Underline", row.transform, typeof(Image));
            var underlineRt = underline.GetComponent<RectTransform>();
            underlineRt.anchorMin = Vector2.zero;
            underlineRt.anchorMax = new Vector2(1f, 0f);
            underlineRt.pivot = new Vector2(0.5f, 0f);
            underlineRt.offsetMin = Vector2.zero;
            underlineRt.offsetMax = new Vector2(0f, 1f);
            var underlineImage = underline.GetComponent<Image>();
            underlineImage.color = ColorFromHex("#ECE1D1", 0.18f);
            underlineImage.raycastTarget = false;

            var dot = Ui("SelectedDot", row.transform, typeof(Image));
            var dotRt = dot.GetComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(1f, 0.5f);
            dotRt.anchorMax = new Vector2(1f, 0.5f);
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.anchoredPosition = new Vector2(-142f, 0f);
            dotRt.sizeDelta = new Vector2(7f, 7f);
            var dotImage = dot.GetComponent<Image>();
            dotImage.color = ColorFromHex("#ECE1D1", 0f);
            dotImage.raycastTarget = false;

            var style = row.GetComponent<MainMenuSelectableStyle>();
            var so = new SerializedObject(style);
            so.FindProperty("content").objectReferenceValue = content.GetComponent<RectTransform>();
            so.FindProperty("label").objectReferenceValue = labelText;
            so.FindProperty("icon").objectReferenceValue = iconText;
            so.FindProperty("underline").objectReferenceValue = underlineImage;
            so.FindProperty("dot").objectReferenceValue = dotImage;
            so.FindProperty("secondary").boolValue = secondary;
            so.ApplyModifiedPropertiesWithoutUndo();

            return button;
        }

        private static void LinkVertical(Button button, Button up, Button down)
        {
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            button.navigation = navigation;
        }

        private static void SetButtonColors(Button button, Color focus, Color pressed, Color disabled)
        {
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = focus;
            colors.selectedColor = focus;
            colors.pressedColor = pressed;
            colors.disabledColor = disabled;
            colors.fadeDuration = 0.10f;
            button.colors = colors;
        }

        private static Image AddFullImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = Ui(name, parent, typeof(Image));
            Stretch(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(GameObject go, string text, int size, Color color, TextAnchor anchor, Font font, FontStyle style = FontStyle.Normal)
        {
            var uiText = go.AddComponent<Text>();
            uiText.font = font;
            uiText.text = text;
            uiText.fontSize = size;
            uiText.fontStyle = style;
            uiText.color = color;
            uiText.alignment = anchor;
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            uiText.raycastTarget = false;
            return uiText;
        }

        private static GameObject Ui(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (var component in components)
            {
                go.AddComponent(component);
            }

            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.anchoredPosition3D = Vector3.zero;
        }

        private static Color ColorFromHex(string hex, float alpha)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            color.a = alpha;
            return color;
        }

        private static void BuildGeneratedSprites()
        {
            SaveTexture(VignettePath, 512, 512, (x, y) =>
            {
                float nx = ((x + 0.5f) / 512f) * 2f - 1f;
                float ny = ((y + 0.5f) / 512f) * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.SmoothStep(0.42f, 1.15f, distance) * 0.82f;
                return new Color(0.047f, 0.055f, 0.063f, alpha);
            });

            SaveTexture(ScanlinePath, 8, 8, (x, y) => y < 2 ? new Color(0f, 0f, 0f, 0.13f) : new Color(1f, 1f, 1f, 0f));

            SaveTexture(TopoPath, 128, 128, (x, y) =>
            {
                float alpha = 0f;
                var centers = new[]
                {
                    new Vector2(12f, 18f), new Vector2(48f, 21f), new Vector2(85f, 8f), new Vector2(112f, 34f),
                    new Vector2(24f, 73f), new Vector2(65f, 62f), new Vector2(104f, 94f), new Vector2(42f, 112f)
                };

                foreach (var center in centers)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance < 5f) alpha = Mathf.Max(alpha, 0.030f * (1f - distance / 5f));
                    if (distance > 12f && distance < 13.2f) alpha = Mathf.Max(alpha, 0.018f);
                }

                return new Color(0.749f, 0.788f, 0.761f, alpha);
            });

            SaveTexture(PanelPath, 512, 16, (x, y) =>
            {
                float t = x / 511f;
                float alpha = Mathf.Lerp(0.86f, 0.63f, Mathf.SmoothStep(0f, 1f, t));
                return new Color(0.047f, 0.055f, 0.063f, alpha);
            });
        }

        private static void SaveTexture(string assetPath, int width, int height, Func<int, int, Color> pixel)
        {
            string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, pixel(x, y));
                }
            }

            texture.Apply();
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
    }
}
