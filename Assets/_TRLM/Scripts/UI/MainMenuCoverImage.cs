using UnityEngine;
using UnityEngine.UI;

namespace TRLM.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class MainMenuCoverImage : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [Range(0f, 1f)]
        [SerializeField] private float cropPivotX = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float cropPivotY = 0.5f;

        private RectTransform rectTransform;
        private Image image;

        private void Awake()
        {
            Cache();
            ApplyCover();
        }

        private void OnEnable()
        {
            Cache();
            ApplyCover();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyCover();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Cache();
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Cache();
                ApplyCover();
            };
        }
#endif

        private void Cache()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (image == null) image = GetComponent<Image>();
            if (viewport == null && rectTransform != null && rectTransform.parent is RectTransform parent)
            {
                viewport = parent;
            }
        }

        private void ApplyCover()
        {
            if (rectTransform == null || image == null || viewport == null || image.sprite == null) return;

            float parentWidth = viewport.rect.width;
            float parentHeight = viewport.rect.height;
            if (parentWidth <= 0f || parentHeight <= 0f) return;

            Rect spriteRect = image.sprite.rect;
            float spriteAspect = spriteRect.width / spriteRect.height;
            float parentAspect = parentWidth / parentHeight;

            float width = parentWidth;
            float height = parentHeight;

            if (parentAspect > spriteAspect)
            {
                height = parentWidth / spriteAspect;
            }
            else
            {
                width = parentHeight * spriteAspect;
            }

            Vector2 cropPivot = new Vector2(cropPivotX, cropPivotY);
            rectTransform.anchorMin = cropPivot;
            rectTransform.anchorMax = cropPivot;
            rectTransform.pivot = cropPivot;
            rectTransform.sizeDelta = new Vector2(width, height);
            rectTransform.anchoredPosition = Vector2.zero;
            image.preserveAspect = true;
        }
    }
}
