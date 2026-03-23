using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    public static class UIBuilderHelper
    {
        // ── Colours ────────────────────────────────────────────────────────────

        public static readonly Color ColBg = new(0.11f, 0.14f, 0.19f);
        public static readonly Color ColPanel = new(0.14f, 0.18f, 0.24f);
        public static readonly Color ColInput = new(0.08f, 0.10f, 0.14f);
        public static readonly Color ColBtn = new(0.24f, 0.39f, 0.55f);
        public static readonly Color ColBtnSecondary = new(0.18f, 0.28f, 0.41f);
        public static readonly Color ColBtnSmall = new(0.22f, 0.30f, 0.40f);
        public static readonly Color ColTextPrimary = new(0.91f, 0.93f, 0.96f);
        public static readonly Color ColTextSecondary = new(0.61f, 0.67f, 0.75f);
        public static readonly Color ColTextMuted = new(0.39f, 0.45f, 0.51f);
        public static readonly Color ColError = new(0.85f, 0.25f, 0.25f);
        public static readonly Color ColSuccess = new(0.25f, 0.75f, 0.35f);

        // ── Canvas ─────────────────────────────────────────────────────────────

        public static Canvas MakeCanvas(Transform parent, string name, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // ── EventSystem ────────────────────────────────────────────────────────

        public static void EnsureEventSystem()
        {
#if UNITY_EDITOR
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
#endif
        }

        // ── Elements ───────────────────────────────────────────────────────────

        public static TMP_Text MakeText(Transform parent, string name,
            float fontSize, FontStyles style, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            return text;
        }

        public static Button MakeButton(Transform parent, string name,
            string label, Color bgColor, Color textColor,
            float fontSize = 24, FontStyles fontStyle = FontStyles.Bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            StretchFill(textGo.GetComponent<RectTransform>());
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;

            return btn;
        }

        public static TMP_InputField MakeInputField(Transform parent, string name,
            string placeholder)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootImg = root.AddComponent<Image>();
            rootImg.color = ColInput;
            var field = root.AddComponent<TMP_InputField>();

            var font = TMP_Settings.defaultFontAsset;

            // Viewport
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(root.transform, false);
            viewport.AddComponent<RectMask2D>();
            var vpRect = viewport.GetComponent<RectTransform>();
            StretchFill(vpRect);
            vpRect.offsetMin = new Vector2(10, 4);
            vpRect.offsetMax = new Vector2(-10, -4);

            // Text
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(viewport.transform, false);
            StretchFill(textGo.GetComponent<RectTransform>());
            var textComp = textGo.AddComponent<TextMeshProUGUI>();
            textComp.font = font;
            textComp.fontSize = 22;
            textComp.color = ColTextPrimary;
            textComp.alignment = TextAlignmentOptions.MidlineLeft;

            // Placeholder
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(viewport.transform, false);
            StretchFill(phGo.GetComponent<RectTransform>());
            var phComp = phGo.AddComponent<TextMeshProUGUI>();
            phComp.font = font;
            phComp.fontSize = 22;
            phComp.color = ColTextMuted;
            phComp.fontStyle = FontStyles.Italic;
            phComp.alignment = TextAlignmentOptions.MidlineLeft;
            phComp.text = placeholder;

            field.textViewport = vpRect;
            field.textComponent = textComp;
            field.placeholder = phComp;
            field.fontAsset = font;

            return field;
        }

        public static GameObject MakeView(Transform parent, string name, Color? bgColor = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            StretchFill(go.GetComponent<RectTransform>());
            var img = go.AddComponent<Image>();
            img.color = bgColor ?? ColPanel;
            img.raycastTarget = true;
            return go;
        }

        // ── Layout helpers ─────────────────────────────────────────────────────

        public static LayoutElement AddLayout(GameObject go, float preferredHeight,
            float preferredWidth = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            return le;
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject go,
            TextAnchor alignment = TextAnchor.MiddleCenter, float spacing = 12f,
            RectOffset padding = null)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = alignment;
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(40, 40, 40, 40);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void SetAnchored(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
