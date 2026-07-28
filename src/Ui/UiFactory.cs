using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EMI
{
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
