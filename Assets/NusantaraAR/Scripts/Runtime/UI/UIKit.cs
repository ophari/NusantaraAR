using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Token desain: gading (latar), terakota (aksen, ala terakota Majapahit), nila (indigo batik, bar navigasi).
    /// Emas hanya untuk titik hotspot. Kontras teks diperiksa <c>UiThemeTests</c> (WCAG AA), termasuk teks di atas kaca
    /// pada latar kamera terburuk (hitam/putih).
    /// </summary>
    public static class Theme
    {
        public static readonly Color Bg = Hex(0xF4EFE8);
        public static readonly Color Stage = Hex(0xE8E0D5);
        public static readonly Color Surface = Hex(0xFFFCF8);
        public static readonly Color Line = Hex(0xE3D9CC);
        public static readonly Color Ink = Hex(0x2A2420);
        public static readonly Color InkMuted = Hex(0x5E534C);
        public static readonly Color Accent = Hex(0xB85A3C);
        /// <summary>Terakota tua untuk teks (Accent terlalu terang untuk teks kecil di atas kaca).</summary>
        public static readonly Color AccentText = Hex(0x8F4128);
        public static readonly Color AccentSoft = Hex(0xE28D66);
        public static readonly Color OnAccent = Color.white;
        public static readonly Color Nav = Hex(0x1E2B3F);
        public static readonly Color NavIcon = Hex(0xEDE6DC);
        public static readonly Color NavActive = Hex(0xF2A57F);
        public static readonly Color Gold = Hex(0xD4AF37);

        public static readonly Color Scrim = new Color(0.08f, 0.07f, 0.06f, 0.45f);
        public static readonly Color ShadowTint = new Color(0.2f, 0.12f, 0.08f, 0.2f);
        public static readonly Color GlassHighlight = new Color(1f, 1f, 1f, 0.75f);

        /// <summary>Kekuatan tint kaca di atas blur untuk panel berteks (0 = blur murni, 1 = warna penuh).</summary>
        public const float GlassStrength = 0.68f;
        /// <summary>Tombol ikon tanpa teks: blur lebih tampak.</summary>
        public const float GlassChromeStrength = 0.55f;
        public const float NavGlassStrength = 0.94f;

        public const float Body = 36f;
        public const float Small = 30f;
        public const float Button = 34f;
        public const float Heading = 44f;
        public const float Title = 60f;
        public const float Caption = 22f;

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>"#RRGGBB" untuk rich text TextMeshPro.</summary>
        public static string HexOf(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // ------------------------------------------------------------------ WCAG

        public static float Luminance(Color c) =>
            0.2126f * Mathf.GammaToLinearSpace(c.r) + 0.7152f * Mathf.GammaToLinearSpace(c.g) + 0.0722f * Mathf.GammaToLinearSpace(c.b);

        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        /// <summary>Warna akhir kaca di atas latar tertentu (dicampur di ruang linear seperti shader UIGlass di proyek Linear).</summary>
        public static Color GlassOver(Color tint, float strength, Color behind)
        {
            float Mix(float t, float b) =>
                Mathf.LinearToGammaSpace(Mathf.Lerp(Mathf.GammaToLinearSpace(b), Mathf.GammaToLinearSpace(t), strength));
            return new Color(Mix(tint.r, behind.r), Mix(tint.g, behind.g), Mix(tint.b, behind.b), 1f);
        }
    }

    /// <summary>Sprite prosedural (tanpa file gambar): kotak bersudut bulat 9-slice, garis tepi, bayangan lembut, lingkaran.</summary>
    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite RoundedRect(int radius = 28)
        {
            string key = "rr" + radius;
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            int size = radius * 2 + 4;
            float c = size * 0.5f, inner = c - radius;
            s = Sliced(key, size, radius + 1, (x, y) =>
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x - c) - inner);
                float dy = Mathf.Max(0f, Mathf.Abs(y - c) - inner);
                return Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            });
            return s;
        }

        /// <summary>Garis tepi kotak bersudut bulat (garis sorot kaca, garis chip).</summary>
        public static Sprite RoundedOutline(int radius, float thickness = 2f)
        {
            string key = "ro" + radius + "_" + thickness;
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            int size = radius * 2 + 4;
            float c = size * 0.5f, half = c - 0.5f;
            s = Sliced(key, size, radius + 1, (x, y) =>
            {
                float sd = SdRoundBox(x - c, y - c, half, half, radius);
                return Mathf.Clamp01(0.5f - sd) * Mathf.Clamp01(sd + thickness + 0.5f);
            });
            return s;
        }

        /// <summary>
        /// Bayangan lembut 9-slice. Rect bayangan = rect target diperlebar <paramref name="blur"/> di tiap sisi;
        /// tepi target berada tepat di pertengahan pudar, sudutnya sama dengan <see cref="RoundedRect"/>.
        /// </summary>
        public static Sprite Shadow(int radius, int blur)
        {
            string key = "sh" + radius + "_" + blur;
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            int size = (radius + blur) * 2 + 4;
            float c = size * 0.5f, half = c - blur;
            s = Sliced(key, size, radius + blur + 1, (x, y) =>
            {
                float sd = SdRoundBox(x - c, y - c, half, half, radius);
                float t = Mathf.Clamp01((sd + blur * 0.35f) / (blur * 1.35f));
                return (1f - t) * (1f - t) * (1f - t * 0.5f);
            });
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

        /// <summary>Jarak bertanda ke kotak bersudut bulat berpusat di (0,0) dengan setengah ukuran (hw, hh).</summary>
        public static float SdRoundBox(float x, float y, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x) - (hw - r), qy = Mathf.Abs(y) - (hh - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Sprite Sliced(string key, int size, float border, Func<float, float, float> alpha)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f)) * 255));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
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

    /// <summary>
    /// Solid = kartu gading, Glass = kaca buram berteks, GlassChrome = kaca tombol ikon, NavGlass = kaca nila,
    /// Accent = terakota, Outline = gading bergaris tepi (chip).
    /// </summary>
    public enum SurfaceStyle { Solid, Glass, GlassChrome, NavGlass, Accent, Outline }

    /// <summary>
    /// Bagian panel berelevasi. Akar (transparan, penerima sentuhan) memegang tata letak dan konten; anak <c>Shadow</c>,
    /// <c>Fill</c>, <c>Highlight</c> diabaikan layout group. Warna panel diganti lewat <see cref="fill"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class Surface : MonoBehaviour
    {
        public Image root, shadow, fill, highlight;

        public static Image FillOf(Component c) =>
            c != null && c.TryGetComponent(out Surface s) ? s.fill : c != null ? c.GetComponent<Image>() : null;
    }

    /// <summary>Pembuat UI uGUI dari kode (tanpa YAML scene/prefab).</summary>
    public static class UIKit
    {
        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            // UV1 membawa kekuatan tint kaca (GlassSurface).
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
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

        /// <summary>Panel berelevasi (bayangan + isi + garis sorot). Mengembalikan akar; isi ada di <see cref="Surface.fill"/>.</summary>
        public static Image Surface(Transform parent, string name, SurfaceStyle style, int radius = 32, bool raycast = true,
            bool shadow = true)
        {
            var root = Panel(parent, name, new Color(1f, 1f, 1f, 0f), false, raycast);
            root.canvasRenderer.cullTransparentMesh = true;
            var parts = root.gameObject.AddComponent<Surface>();
            parts.root = root;

            if (shadow)
            {
                int blur = Mathf.Clamp(radius / 2 + 10, 14, 30);
                parts.shadow = Panel(root.transform, "Shadow", Theme.ShadowTint, false, false);
                parts.shadow.sprite = SpriteFactory.Shadow(radius, blur);
                parts.shadow.type = Image.Type.Sliced;
                float drop = blur * 0.35f;
                Stretch(parts.shadow.rectTransform, -blur, -blur, -blur + drop, -blur - drop);
                IgnoreLayout(parts.shadow);
            }

            Color fillColor;
            switch (style)
            {
                case SurfaceStyle.NavGlass: fillColor = Theme.Nav; break;
                case SurfaceStyle.Accent: fillColor = Theme.Accent; break;
                default: fillColor = Theme.Surface; break;
            }
            parts.fill = Panel(root.transform, "Fill", fillColor, true, false, radius);
            Stretch(parts.fill.rectTransform);
            IgnoreLayout(parts.fill);
            if (style == SurfaceStyle.Glass || style == SurfaceStyle.GlassChrome || style == SurfaceStyle.NavGlass)
            {
                var glass = parts.fill.gameObject.AddComponent<GlassSurface>();
                glass.Strength = style == SurfaceStyle.Glass ? Theme.GlassStrength
                    : style == SurfaceStyle.GlassChrome ? Theme.GlassChromeStrength : Theme.NavGlassStrength;
            }

            Color? edge = style == SurfaceStyle.Glass || style == SurfaceStyle.GlassChrome ? Theme.GlassHighlight
                : style == SurfaceStyle.NavGlass ? new Color(1f, 1f, 1f, 0.12f)
                : style == SurfaceStyle.Outline ? Theme.Line
                : (Color?)null;
            if (edge.HasValue)
            {
                parts.highlight = Panel(root.transform, "Highlight", edge.Value, false, false);
                parts.highlight.sprite = SpriteFactory.RoundedOutline(radius, style == SurfaceStyle.Outline ? 2.5f : 2f);
                parts.highlight.type = Image.Type.Sliced;
                Stretch(parts.highlight.rectTransform);
                IgnoreLayout(parts.highlight);
            }
            return root;
        }

        static void IgnoreLayout(Component c) => c.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

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

        public static Image IconImage(Transform parent, Icon icon, float size, Color color, string name = "Icon")
        {
            var img = Panel(parent, name, color, false, false);
            img.sprite = IconFactory.Get(icon);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        static Button AddButton(Image root, Graphic target, UnityAction onClick)
        {
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.82f, 0.8f, 0.78f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            button.onClick.AddListener(() => AudioManager.Instance.Click());
            return button;
        }

        public static Button Button(Transform parent, string name, string label, ButtonStyle style, UnityAction onClick,
            out TextMeshProUGUI labelText, float fontSize = Theme.Button)
        {
            Image root;
            Graphic target;
            Color fg;
            switch (style)
            {
                case ButtonStyle.Primary:
                    root = Surface(parent, name, SurfaceStyle.Accent, 44);
                    fg = Theme.OnAccent;
                    break;
                case ButtonStyle.Secondary:
                    root = Surface(parent, name, SurfaceStyle.Glass, 44);
                    fg = Theme.Ink;
                    break;
                case ButtonStyle.Chip:
                    root = Surface(parent, name, SurfaceStyle.Outline, 44, true, false);
                    fg = Theme.Ink;
                    break;
                default:
                    root = Panel(parent, name, new Color(1f, 1f, 1f, 0f), false, true);
                    root.canvasRenderer.cullTransparentMesh = true;
                    fg = Theme.InkMuted;
                    break;
            }

            labelText = Text(root.transform, "Label", label, fontSize, fg, TextAlignmentOptions.Center,
                style == ButtonStyle.Primary ? FontStyles.Bold : FontStyles.Normal);
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(labelText.rectTransform, 20, 20, 4, 4);
            target = style == ButtonStyle.Ghost ? (Graphic)labelText : root.GetComponent<Surface>().fill;
            return AddButton(root, target, onClick);
        }

        public static Button Button(Transform parent, string name, string label, ButtonStyle style, UnityAction onClick) =>
            Button(parent, name, label, style, onClick, out _);

        /// <summary>Tombol utama dengan ikon di kiri label (mis. "Scan QR (AR)").</summary>
        public static Button IconTextButton(Transform parent, string name, Icon icon, string label, ButtonStyle style,
            UnityAction onClick, out TextMeshProUGUI labelText, float fontSize = Theme.Button)
        {
            var b = Button(parent, name, label, style, onClick, out labelText, fontSize);
            var row = Rect("Content", b.transform);
            Stretch(row, 20, 20, 0, 0);
            var h = HRow(row, 14f, null, false);
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            var img = IconImage(row, icon, fontSize * 1.25f, labelText.color);
            Layout(img, fontSize * 1.25f, fontSize * 1.25f);
            labelText.transform.SetParent(row, false);
            labelText.overflowMode = TextOverflowModes.Overflow;
            return b;
        }

        /// <summary>Tombol ikon bulat (circle) atau kotak bulat, gaya kaca.</summary>
        public static Button IconButton(Transform parent, string name, Icon icon, UnityAction onClick, float size = 96f,
            SurfaceStyle style = SurfaceStyle.GlassChrome, bool circle = true)
        {
            var root = Surface(parent, name, style, circle ? Mathf.RoundToInt(size * 0.5f) : 28);
            root.rectTransform.sizeDelta = new Vector2(size, size);
            var iconColor = style == SurfaceStyle.Accent ? Theme.OnAccent : style == SurfaceStyle.NavGlass ? Theme.NavIcon : Theme.Ink;
            IconImage(root.transform, icon, size * 0.46f, iconColor);
            return AddButton(root, root.GetComponent<Surface>().fill, onClick);
        }

        /// <summary>Tombol rel: kotak kaca dengan ikon di atas dan keterangan kecil di bawah.</summary>
        public static Button RailButton(Transform parent, string name, Icon icon, string label, UnityAction onClick,
            out Image iconImage, out TextMeshProUGUI labelText, float width = 136f, float height = 128f)
        {
            var root = Surface(parent, name, SurfaceStyle.Glass, 32);
            root.rectTransform.sizeDelta = new Vector2(width, height);
            iconImage = IconImage(root.transform, icon, 50f, Theme.Ink);
            Place(iconImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(50f, 50f));
            labelText = Text(root.transform, "Label", label, Theme.Caption, Theme.Ink, TextAlignmentOptions.Bottom);
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 16f;
            labelText.fontSizeMax = Theme.Caption;
            Stretch(labelText.rectTransform, 8, 8, 76, 14);
            return AddButton(root, root.GetComponent<Surface>().fill, onClick);
        }

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
            hit.canvasRenderer.cullTransparentMesh = true;
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
            // Track tipis 14 unit di tengah; knob 48 unit berbayangan.
            var bg = Panel(root, "Background", Theme.Line, true, true, 8);
            MidBand(bg.rectTransform, 0f, 14f);
            var fillArea = MidBand(Rect("Fill Area", root), 0f, 14f);
            var fill = Panel(fillArea, "Fill", Theme.Accent, true, false, 8);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = MidBand(Rect("Handle Area", root), -48f, 48f);
            var handle = Surface(handleArea, "Handle", SurfaceStyle.Solid, 24);
            handle.rectTransform.sizeDelta = new Vector2(48, 0);
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle.GetComponent<Surface>().fill;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.interactable = interactable;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        /// <summary>Pita mendatar selebar induk (+ <paramref name="widthDelta"/>) dengan tinggi tetap, di tengah vertikal.</summary>
        static RectTransform MidBand(RectTransform rt, float widthDelta, float height)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(widthDelta, height);
            return rt;
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
