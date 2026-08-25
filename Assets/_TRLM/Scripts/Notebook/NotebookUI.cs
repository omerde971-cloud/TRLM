using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TRLM.Notebook
{
    /// <summary>
    /// Code-built uGUI for the Kehanet Defteri — the whole hierarchy is constructed in Awake so
    /// the orchestrator drops ONE GameObject (NotebookController + this) into the scene with zero
    /// prefab/art dependencies. Styling goal: an aged in-world artifact, not an RPG menu — dark
    /// vignette, leather frame, warm parchment, sepia ink, Turkish body with the English
    /// translation set apart as a lighter italic block (project text convention). Uses legacy
    /// UnityEngine.UI.Text to match SubtitleUI. All page rendering happens only on page change;
    /// Update just eases the CanvasGroup on unscaled time so the book animates while timeScale=0.
    /// </summary>
    public class NotebookUI : MonoBehaviour
    {
        // ---- Palette (aged artifact, warm and desaturated) ----
        private static readonly Color Vignette = new Color(0f, 0f, 0f, 0.66f);
        private static readonly Color Leather = new Color(0.22f, 0.15f, 0.10f, 1f);
        private static readonly Color Parchment = new Color(0.85f, 0.78f, 0.62f, 1f);
        private static readonly Color ParchmentAged = new Color(0.71f, 0.64f, 0.51f, 1f); // missing-page tint
        private static readonly Color Ink = new Color(0.22f, 0.15f, 0.09f, 1f);
        private static readonly Color InkFaded = new Color(0.36f, 0.28f, 0.19f, 0.92f);
        private static readonly Color InkGhost = new Color(0.30f, 0.24f, 0.16f, 0.45f);
        private static readonly Color DividerColor = new Color(0.30f, 0.22f, 0.13f, 0.55f);

        [SerializeField] private float fadeSeconds = 0.18f;

        private ProphecyNotebook notebook;
        private Font font;
        private CanvasGroup group;
        private float fadeTarget;
        private float fadeVelocity;
        private int pageIndex; // index into notebook.OrderedPages

        // Built widgets rewritten per page-change (never per frame)
        private Image parchmentImage;
        private Text headerCountText;
        private Text pageTitleTrText;
        private Text pageTitleEnText;
        private Image illustrationImage;
        private Text bodyTrText;
        private Text bodyEnText;
        private Image bodyDivider;
        private Text missingGlyphText;
        private Text missingLabelText;
        private Text pageNumberText;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildHierarchy();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private void Start()
        {
            notebook = ProphecyNotebook.Instance;
            if (notebook == null)
                notebook = FindFirstObjectByType<ProphecyNotebook>();
        }

        private void Update()
        {
            // Unscaled: the book must ease in/out while the world sits at timeScale 0.
            float dt = Time.unscaledDeltaTime;
            group.alpha = Mathf.SmoothDamp(group.alpha, fadeTarget, ref fadeVelocity, fadeSeconds, Mathf.Infinity, dt);
            if (fadeTarget <= 0f && group.alpha < 0.01f && group.blocksRaycasts)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        // ---------------------------------------------------------------- Public API (NotebookController)

        public void Show(ProphecyPage focus)
        {
            if (notebook == null) notebook = ProphecyNotebook.Instance;
            if (focus != null) SnapIndexTo(focus);
            ClampIndex();
            RenderCurrentPage();
            fadeTarget = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        public void Hide()
        {
            fadeTarget = 0f;
        }

        public void FocusPage(ProphecyPage page)
        {
            if (page == null) return;
            SnapIndexTo(page);
            RenderCurrentPage();
        }

        public void StepPage(int delta)
        {
            if (notebook == null || notebook.OrderedPages.Count == 0) return;
            int count = notebook.OrderedPages.Count;
            pageIndex = ((pageIndex + delta) % count + count) % count; // wrap both directions
            RenderCurrentPage();
        }

        // ---------------------------------------------------------------- Rendering

        private void SnapIndexTo(ProphecyPage page)
        {
            if (notebook == null) return;
            var pages = notebook.OrderedPages;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] == page) { pageIndex = i; return; }
            }
        }

        private void ClampIndex()
        {
            int count = notebook != null ? notebook.OrderedPages.Count : 0;
            pageIndex = count == 0 ? 0 : Mathf.Clamp(pageIndex, 0, count - 1);
        }

        private void RenderCurrentPage()
        {
            if (notebook == null || notebook.OrderedPages.Count == 0)
            {
                // No catalog authored yet — show the book as entirely empty rather than erroring.
                SetMissingVisible(true);
                SetContentVisible(false);
                parchmentImage.color = ParchmentAged;
                missingLabelText.text = "Defter boş.\nThe notebook is empty.";
                headerCountText.text = string.Empty;
                pageNumberText.text = string.Empty;
                return;
            }

            var pages = notebook.OrderedPages;
            var page = pages[pageIndex];
            bool found = notebook.HasPage(page.id);

            headerCountText.text = $"{notebook.CollectedCount} / {pages.Count} sayfa bulundu — pages found";
            pageNumberText.text = $"Sayfa {pageIndex + 1} / {pages.Count}";

            if (!found)
            {
                // Locked slot: aged, emptied, content NEVER revealed — only that something is missing.
                SetContentVisible(false);
                SetMissingVisible(true);
                parchmentImage.color = ParchmentAged;
                missingLabelText.text = "Kayıp sayfa — henüz bulunmadı.\nMissing page — not yet found.";
                return;
            }

            SetMissingVisible(false);
            SetContentVisible(true);
            parchmentImage.color = Parchment;

            // Dagger, not a star/gem icon: present in the builtin LegacyRuntime font AND reads as a
            // manuscript margin-mark, matching the artifact fiction.
            string keyMark = page.isKeyProphecy ? "† " : string.Empty;
            pageTitleTrText.text = keyMark + (page.titleTurkish ?? string.Empty);
            pageTitleEnText.text = page.titleEnglish ?? string.Empty;
            bodyTrText.text = page.bodyTurkish ?? string.Empty;
            bodyEnText.text = page.bodyEnglish ?? string.Empty;

            bool hasIllustration = page.illustration != null;
            illustrationImage.gameObject.SetActive(hasIllustration);
            if (hasIllustration)
            {
                illustrationImage.sprite = page.illustration;
                illustrationImage.preserveAspect = true;
            }
        }

        private void SetContentVisible(bool visible)
        {
            pageTitleTrText.gameObject.SetActive(visible);
            pageTitleEnText.gameObject.SetActive(visible);
            bodyTrText.gameObject.SetActive(visible);
            bodyEnText.gameObject.SetActive(visible);
            bodyDivider.gameObject.SetActive(visible);
            if (!visible) illustrationImage.gameObject.SetActive(false);
        }

        private void SetMissingVisible(bool visible)
        {
            missingGlyphText.gameObject.SetActive(visible);
            missingLabelText.gameObject.SetActive(visible);
        }

        // ---------------------------------------------------------------- Hierarchy construction

        private void BuildHierarchy()
        {
            EnsureEventSystem();

            // Canvas root — above HUD/subtitles.
            var canvasGO = new GameObject("NotebookCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGO.GetComponent<CanvasGroup>();

            // Darkened vignette so the world reads as "behind" the artifact.
            MakeImage(canvasGO.transform, "Vignette", Vignette, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Leather frame (slightly larger than the parchment = a worn binding edge).
            var frame = MakeImage(canvasGO.transform, "LeatherFrame", Leather, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 680f));
            // Parchment sheet.
            parchmentImage = MakeImage(frame.transform, "Parchment", Parchment, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 640f));
            var p = parchmentImage.transform;

            // -- Letterhead --
            MakeText(p, "HeaderTr", "KEHANET DEFTERİ", 30, FontStyle.Bold, Ink, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(860f, 36f));
            MakeText(p, "HeaderEn", "The Prophecy Notebook", 16, FontStyle.Italic, InkFaded, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(860f, 22f));
            headerCountText = MakeText(p, "HeaderCount", string.Empty, 13, FontStyle.Normal, InkGhost, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(860f, 18f));
            MakeDivider(p, "HeaderRule", new Vector2(0f, -104f), 760f);

            // -- Page content --
            pageTitleTrText = MakeText(p, "PageTitleTr", string.Empty, 24, FontStyle.Bold, Ink, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(820f, 30f));
            pageTitleEnText = MakeText(p, "PageTitleEn", string.Empty, 15, FontStyle.Italic, InkFaded, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(820f, 20f));

            var illGO = new GameObject("Illustration", typeof(Image));
            illGO.transform.SetParent(p, false);
            illustrationImage = illGO.GetComponent<Image>();
            illustrationImage.color = new Color(1f, 1f, 1f, 0.9f); // slight fade — old ink, not a crisp icon
            var illRt = (RectTransform)illGO.transform;
            illRt.anchorMin = illRt.anchorMax = new Vector2(0.5f, 1f);
            illRt.pivot = new Vector2(0.5f, 1f);
            illRt.anchoredPosition = new Vector2(0f, -176f);
            illRt.sizeDelta = new Vector2(260f, 150f);
            illGO.SetActive(false);

            bodyTrText = MakeText(p, "BodyTr", string.Empty, 19, FontStyle.Normal, Ink, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -338f), new Vector2(760f, 120f));
            bodyDivider = MakeDivider(p, "BodyRule", new Vector2(0f, -462f), 420f);
            // English translation clearly set apart: lighter faded ink, italic, below its own rule.
            bodyEnText = MakeText(p, "BodyEn", string.Empty, 16, FontStyle.Italic, InkFaded, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -474f), new Vector2(760f, 110f));

            // -- Missing-page placeholder (locked slot) --
            missingGlyphText = MakeText(p, "MissingGlyph", "?", 120, FontStyle.Bold, InkGhost, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(300f, 150f));
            missingLabelText = MakeText(p, "MissingLabel", string.Empty, 18, FontStyle.Italic, InkFaded, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(700f, 60f));
            missingGlyphText.gameObject.SetActive(false);
            missingLabelText.gameObject.SetActive(false);

            // -- Footer: prev / page number / next + close hint --
            MakePageButton(p, "PrevButton", "‹", new Vector2(-330f, 34f), -1); // ‹
            MakePageButton(p, "NextButton", "›", new Vector2(330f, 34f), +1);  // ›
            pageNumberText = MakeText(p, "PageNumber", string.Empty, 16, FontStyle.Normal, Ink, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(300f, 24f));
            MakeText(p, "CloseHint", "J / Esc — Kapat (Close)    ‹ › — Sayfa çevir (Turn page)", 12, FontStyle.Normal, InkGhost, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(700f, 16f));
        }

        /// <summary>uGUI Buttons need an EventSystem; scenes driven purely by PlayerInputHandler may
        /// not have one, so create an Input System-module one on demand (never a duplicate).</summary>
        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform, false);
        }

        private Image MakeImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size; // (0,0) with stretch anchors = full-parent fill
            return img;
        }

        private Text MakeText(Transform parent, string name, string content, int size, FontStyle style, Color color, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 rectSize)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = rectSize;
            return text;
        }

        private Image MakeDivider(Transform parent, string name, Vector2 pos, float width)
        {
            return MakeImage(parent, name, DividerColor, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(width, 2f));
        }

        private void MakePageButton(Transform parent, string name, string glyph, Vector2 pos, int direction)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.30f, 0.22f, 0.13f, 0.18f); // faint worn thumb-mark, not a chrome button
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(56f, 44f);

            var label = MakeText(go.transform, "Glyph", glyph, 30, FontStyle.Bold, Ink, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 44f));
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var button = go.GetComponent<Button>();
            int dir = direction; // captured copy for the closure
            button.onClick.AddListener(() => StepPage(dir));
        }
    }
}
