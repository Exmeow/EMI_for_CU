using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EMI
{
    /// <summary>
    /// EMI 的运行时 UI 构造工具，统一配色、布局约定和射线设置，避免页面各自拼装不同结构。
    /// </summary>
    internal static class UiFactory
    {
        public static readonly Color Black = new Color(0.015f, 0.018f, 0.018f, 0.98f);
        public static readonly Color RaisedBlack = new Color(0.055f, 0.065f, 0.065f, 0.98f);
        public static readonly Color White = new Color(0.94f, 0.95f, 0.93f, 1f);
        public static readonly Color Muted = new Color(0.55f, 0.58f, 0.56f, 1f);
        public static readonly Color Green = new Color(0.28f, 1f, 0.22f, 1f);
        public static readonly Color Red = new Color(1f, 0.28f, 0.25f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.86f, 0.22f, 1f);

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image CreatePanel(string name, Transform parent, Color color, bool outline = false)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            if (outline)
            {
                Outline effect = rect.gameObject.AddComponent<Outline>();
                effect.effectColor = White;
                effect.effectDistance = new Vector2(1f, -1f);
                effect.useGraphicAlpha = true;
            }

            return image;
        }

        public static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = White;
            text.raycastTarget = false;
            text.richText = true;
            text.characterSpacing = 0f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string label,
            Action clicked,
            out Image image,
            out TextMeshProUGUI text)
        {
            image = CreatePanel(name, parent, RaisedBlack, true);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.68f, 0.76f, 0.7f, 1f);
            colors.pressedColor = new Color(0.3f, 0.8f, 0.36f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.65f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            text = CreateText("Label", image.transform, font, 18f, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 5f, 5f, 3f, 3f);
            text.text = label;

            if (clicked != null)
            {
                button.onClick.AddListener(() => clicked());
            }

            return button;
        }

        public static ScrollRect CreateScrollView(
            string name,
            Transform parent,
            out RectTransform content)
        {
            // 线性列表和网格使用相同的 Viewport/Content 结构，确保滚动位置恢复逻辑可以复用。
            Image background = CreatePanel(name, parent, Black);
            ScrollRect scroll = background.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            Image viewportImage = CreatePanel("Viewport", background.transform, Color.clear);
            RectTransform viewport = viewportImage.rectTransform;
            Stretch(viewport, 2f, 2f, 2f, 2f);
            viewportImage.gameObject.AddComponent<RectMask2D>();

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        public static ScrollRect CreateGridScrollView(
            string name,
            Transform parent,
            int columns,
            Vector2 cellSize,
            out RectTransform content)
        {
            Image background = CreatePanel(name, parent, Black);
            ScrollRect scroll = background.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            Image viewportImage = CreatePanel("Viewport", background.transform, Color.clear);
            RectTransform viewport = viewportImage.rectTransform;
            Stretch(viewport, 2f, 2f, 2f, 2f);
            viewportImage.gameObject.AddComponent<RectMask2D>();

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            GridLayoutGroup layout = content.gameObject.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = new Vector2(4f, 4f);
            layout.cellSize = cellSize;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Math.Max(1, columns);

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        public static TMP_InputField CreateInputField(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string placeholder,
            Action<string> changed,
            out Image background)
        {
            background = CreatePanel(name, parent, RaisedBlack, true);
            TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 80;

            Image viewportImage = CreatePanel("Viewport", background.transform, Color.clear);
            viewportImage.raycastTarget = false;
            Stretch(viewportImage.rectTransform, 9f, 34f, 4f, 4f);
            viewportImage.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI text = CreateText(
                "Text",
                viewportImage.transform,
                font,
                17f,
                TextAlignmentOptions.Left);
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            text.overflowMode = TextOverflowModes.Masking;

            TextMeshProUGUI hint = CreateText(
                "Placeholder",
                viewportImage.transform,
                font,
                17f,
                TextAlignmentOptions.Left);
            Stretch(hint.rectTransform, 0f, 0f, 0f, 0f);
            hint.color = Muted;
            hint.text = placeholder;

            input.textViewport = viewportImage.rectTransform;
            input.textComponent = text;
            input.placeholder = hint;
            if (changed != null)
            {
                input.onValueChanged.AddListener(value => changed(value));
            }

            return input;
        }

        public static void AddTooltip(GameObject target, string title, string description)
        {
            UITooltip tooltip = target.GetComponent<UITooltip>();
            if (tooltip == null)
            {
                tooltip = target.AddComponent<UITooltip>();
            }

            tooltip.skipLocale = true;
            tooltip.tipName = title;
            tooltip.tipDesc = description;
        }

        public static void BlockTooltipsBehind(GameObject target)
        {
            // 原版 tooltip 射线会穿过没有 UITooltip 的遮罩；空提示组件用于明确截断向后查找。
            AddTooltip(target, string.Empty, string.Empty);
        }

        public static void Stretch(
            RectTransform rect,
            float left = 0f,
            float right = 0f,
            float bottom = 0f,
            float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void SetActiveSprite(Image image, Sprite normal, Sprite selected, bool active)
        {
            Sprite sprite = active ? selected : normal;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = sprite != null ? Color.white : (active ? RaisedBlack : Black);
        }
    }
}
