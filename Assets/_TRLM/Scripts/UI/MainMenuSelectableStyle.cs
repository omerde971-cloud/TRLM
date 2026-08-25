using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TRLM.UI
{
    [RequireComponent(typeof(Button))]
    public class MainMenuSelectableStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private Text label;
        [SerializeField] private Text icon;
        [SerializeField] private Image underline;
        [SerializeField] private Image dot;
        [SerializeField] private bool secondary;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.76f, 0.78f, 0.76f, 0.62f);
        [SerializeField] private Color focusColor = new Color(0.925f, 0.882f, 0.82f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.78f, 0.73f, 0.66f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.62f, 0.65f, 0.63f, 0.42f);

        private Button button;
        private bool hover;
        private bool pressed;
        private bool selected;
        private Vector2 basePosition;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (content == null) content = transform as RectTransform;
            basePosition = content != null ? content.anchoredPosition : Vector2.zero;
            Apply(false);
        }

        private void OnEnable()
        {
            Apply(false);
        }

        private void Update()
        {
            Apply(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hover = true;
            Apply(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hover = false;
            pressed = false;
            Apply(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            Apply(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            Apply(true);
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            Apply(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            pressed = false;
            Apply(true);
        }

        private void Apply(bool animated)
        {
            if (button == null) button = GetComponent<Button>();

            bool enabledState = button == null || button.interactable;
            bool focused = enabledState && (hover || selected);
            Color color = enabledState ? (pressed ? pressedColor : focused ? focusColor : normalColor) : disabledColor;

            if (label != null) label.color = color;
            if (icon != null) icon.color = color;

            float underlineAlpha = focused ? 0.72f : 0.18f;
            if (!enabledState) underlineAlpha = 0.10f;
            if (secondary) underlineAlpha *= 0.55f;

            if (underline != null)
            {
                Color underlineColor = focusColor;
                underlineColor.a = underlineAlpha;
                underline.color = underlineColor;
            }

            if (dot != null)
            {
                Color dotColor = focusColor;
                dotColor.a = focused ? 1f : 0f;
                dot.color = dotColor;
            }

            if (content != null)
            {
                float targetOffset = focused ? 4f : 0f;
                if (!enabledState) targetOffset = 0f;
                Vector2 target = basePosition + Vector2.right * targetOffset;
                content.anchoredPosition = animated ? Vector2.Lerp(content.anchoredPosition, target, Time.unscaledDeltaTime * 16f) : target;
            }
        }
    }
}
