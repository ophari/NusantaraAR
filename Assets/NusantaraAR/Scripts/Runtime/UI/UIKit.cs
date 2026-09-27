using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>Token desain PRD §5.1. Panel berisi teks memakai opasitas >= 90% (lolos WCAG AA di atas feed kamera).</summary>
    public static class Theme
    {
        public static readonly Color Gold = Hex(0xD4AF37);
        public static readonly Color Teak = Hex(0x1E1B18);
        public static readonly Color Border = Hex(0x3A342D);
        public static readonly Color Parchment = Hex(0xF5F3EF);
        public static readonly Color Stone = Hex(0xA8A29E);

        /// <summary>Panel berisi teks: opasitas 92%.</summary>
        public static Color TextPanel => WithAlpha(Teak, 0.92f);
        /// <summary>Elemen tanpa teks (halo hotspot, dll): boleh 80%.</summary>
        public static Color Chrome => WithAlpha(Teak, 0.8f);
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.55f);

        public const float Body = 36f;
        public const float Small = 30f;
        public const float Button = 34f;
        public const float Heading = 44f;
        public const float Title = 60f;

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>Sprite prosedural (tanpa file gambar): kotak bersudut bulat 9-slice dan lingkaran.</summary>
    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite RoundedRect(int radius = 28)
        {
            string key = "rr" + radius;
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            int size = radius * 2 + 4;
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float c = size * 0.5f, inner = c - radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - c) - inner);
                float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - c) - inner);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = radius + 1;
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            s.name = key;
            Cache[key] = s;
            return s;
        }

        /// <summary>Lingkaran penuh (ring = false) atau cincin dengan ketebalan relatif.</summary>
        public static Sprite Circle(bool ring = false, float ringThickness = 0.14f)
        {
            string key = ring ? "ring" + ringThickness : "circle";
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            const int size = 128;
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float c = size * 0.5f, r = c - 1f, rIn = r - ringThickness * size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (ring) a *= Mathf.Clamp01(d - rIn + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            s.name = key;
            Cache[key] = s;
            return s;
        }

        static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
    }

    public enum ButtonStyle { Primary, Secondary, Ghost, Chip }

    /// <summary>Pembuat UI uGUI dari kode (tanpa YAML scene/prefab).</summary>
    public static class UIKit
    {
        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Lebar dikunci 1080 unit: layout potret tetap muat di layar 9:19,5 (lebih ramping dari 9:16).
            // (PRD §9.1 menyebut match 0.5; dengan 0.5 lebar kanvas turun ke ~980 unit di HP modern.)
            scaler.matchWidthOrHeight = 0f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Menempatkan rect pada titik jangkar (anchor = pivot) dengan ukuran tetap.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool rounded = true, bool raycast = true, int radius = 28)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (rounded)
            {
                img.sprite = SpriteFactory.RoundedRect(radius);
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            // Set font eksplisit: TMP di bawah induk nonaktif belum menjalankan Awake (font null).
            if (t.font == null) t.font = TMP_Settings.defaultFontAsset;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, ButtonStyle style, UnityAction onClick,
            out TextMeshProUGUI labelText, float fontSize = Theme.Button)
        {
            Color bg, fg;
            switch (style)
            {
                case ButtonStyle.Primary: bg = Theme.Gold; fg = Theme.Teak; break;
                case ButtonStyle.Secondary: bg = Theme.TextPanel; fg = Theme.Parchment; break;
                case ButtonStyle.Chip: bg = Theme.WithAlpha(Theme.Border, 0.95f); fg = Theme.Parchment; break;
                default: bg = new Color(0, 0, 0, 0); fg = Theme.Parchment; break;
            }
            var img = Panel(parent, name, bg, style != ButtonStyle.Ghost, true, style == ButtonStyle.Chip ? 40 : 28);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            button.onClick.AddListener(() => AudioManager.Instance.Click());

            labelText = Text(img.transform, "Label", label, fontSize, fg, TextAlignmentOptions.Center,
                style == ButtonStyle.Primary ? FontStyles.Bold : FontStyles.Normal);
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(labelText.rectTransform, 16, 16, 4, 4);
            return button;
        }

        public static Button Button(Transform parent, string name, string label, ButtonStyle style, UnityAction onClick) =>
            Button(parent, name, label, style, onClick, out _);

        public static LayoutElement Layout(Component c, float preferredHeight = -1, float preferredWidth = -1, float flexibleWidth = -1)
        {
            if (!c.gameObject.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static HorizontalLayoutGroup HRow(RectTransform rt, float spacing, RectOffset padding = null, bool expandWidth = true)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = true;
            return h;
        }

        public static VerticalLayoutGroup VColumn(RectTransform rt, float spacing, RectOffset padding = null)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset();
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        /// <summary>ScrollRect vertikal; konten memakai VerticalLayoutGroup + ContentSizeFitter.</summary>
        public static ScrollRect VerticalScroll(Transform parent, string name, out RectTransform content, float spacing = 20f, RectOffset padding = null)
        {
            var root = Rect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>(); // area geser menerima raycast
            hit.color = new Color(0, 0, 0, 0);
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VColumn(content, spacing, padding);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        public static Slider Slider(Transform parent, string name, float value, UnityAction<float> onChanged, bool interactable = true)
        {
            var root = Rect(name, parent);
            var slider = root.gameObject.AddComponent<Slider>();
            var bg = Panel(root, "Background", Theme.WithAlpha(Theme.Border, 1f), true, true, 10);
            Stretch(bg.rectTransform, 0, 0, 22, 22);
            var fillArea = Rect("Fill Area", root);
            Stretch(fillArea, 0, 0, 22, 22);
            var fill = Panel(fillArea, "Fill", Theme.Gold, true, false, 10);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Rect("Handle Area", root);
            Stretch(handleArea, 20, 20, 0, 0);
            var handle = Panel(handleArea, "Handle", Theme.Parchment, false, true);
            handle.sprite = SpriteFactory.Circle();
            handle.rectTransform.sizeDelta = new Vector2(44, 0);
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.interactable = interactable;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static void SetVisible(Component c, bool visible)
        {
            if (c != null && c.gameObject.activeSelf != visible) c.gameObject.SetActive(visible);
        }
    }
}
