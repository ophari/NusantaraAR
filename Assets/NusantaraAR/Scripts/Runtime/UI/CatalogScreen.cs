using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Layar 1: katalog koleksi (PRD §5.2). Kategori tanpa konten tidak ditampilkan. Latarnya transparan: gumpalan
    /// warna di belakang digambar kamera (<see cref="Backdrop"/>) sehingga kartu kaca mem-blurnya.
    /// Scan QR & Pengaturan ada di <see cref="BottomNav"/>.
    /// </summary>
    public class CatalogScreen : MonoBehaviour
    {
        ContentCatalog catalog;
        Action<ArtifactData> onSelect;
        RectTransform chips, list;
        ArtifactCategory? filter;

        public static CatalogScreen Create(RectTransform fullRoot, ContentCatalog catalog, Action<ArtifactData> onSelect)
        {
            // Transparan tapi tetap menahan sentuhan agar gestur 3D tidak aktif di belakang katalog.
            var bg = UIKit.Panel(fullRoot, "CatalogScreen", new Color(1f, 1f, 1f, 0f), false, true);
            bg.canvasRenderer.cullTransparentMesh = true;
            UIKit.Stretch(bg.rectTransform);
            var area = UIKit.Rect("Safe", bg.transform);
            area.gameObject.AddComponent<SafeArea>();
            var screen = bg.gameObject.AddComponent<CatalogScreen>();
            screen.catalog = catalog;
            screen.onSelect = onSelect;
            screen.Build(area);
            return screen;
        }

        void Build(RectTransform area)
        {
            var title = UIKit.Text(area, "Title", Locale.T("app.title"), Theme.Title, Theme.Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -52f), new Vector2(900f, 80f));
            var accent = UIKit.Panel(area, "Accent", Theme.Accent, true, false, 3);
            UIKit.Place(accent.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -138f), new Vector2(72f, 6f));
            var tagline = UIKit.Text(area, "Tagline", Locale.T("app.tagline"), Theme.Small, Theme.InkMuted);
            LocalizedLabel.Attach(tagline, "app.tagline");
            UIKit.Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -158f), new Vector2(960f, 50f));

            chips = UIKit.Rect("Categories", area);
            UIKit.Place(chips, new Vector2(0f, 1f), new Vector2(48f, -232f), new Vector2(984f, 84f));
            var row = UIKit.HRow(chips, 16f, null, false);
            row.childAlignment = TextAnchor.MiddleLeft;

            var scroll = UIKit.VerticalScroll(area, "List", out list, 32f, new RectOffset(48, 48, 16, 60));
            UIKit.Stretch((RectTransform)scroll.transform, 0, 0, 336, BottomNav.Height);

            Locale.Changed += Refresh;
            Refresh();
        }

        bool dirty;

        void OnDestroy() => Locale.Changed -= Refresh;

        void OnEnable()
        {
            if (dirty) Refresh();
        }

        void Refresh()
        {
            // Tunda pembangunan ulang selama layar tersembunyi (mis. bahasa diganti dari halaman detail).
            dirty = !gameObject.activeInHierarchy;
            if (dirty) return;
            UIKit.Clear(chips);
            UIKit.Clear(list);
            var categories = catalog != null ? catalog.NonEmptyCategories() : new System.Collections.Generic.List<ArtifactCategory>();
            if (categories.Count > 1) AddChip(Locale.T("catalog.all"), null);
            foreach (var c in categories) AddChip(Locale.T("cat." + c), c);

            int shown = 0;
            if (catalog != null)
            {
                foreach (var a in catalog.artifacts)
                {
                    if (a == null || (filter.HasValue && a.category != filter.Value)) continue;
                    AddCard(a);
                    shown++;
                }
            }
            if (shown == 0)
            {
                var empty = UIKit.Text(list, "Empty", Locale.T("catalog.empty"), Theme.Body, Theme.InkMuted);
                UIKit.Layout(empty, 80);
            }
        }

        void AddChip(string label, ArtifactCategory? category)
        {
            bool on = filter == category;
            var b = UIKit.Button(chips, "Chip", label, on ? ButtonStyle.Primary : ButtonStyle.Chip, () =>
            {
                filter = category;
                Refresh();
            }, out var l, 30f);
            UIKit.Layout(b, 80, Mathf.Max(180f, l.GetPreferredValues(label).x + 72f));
        }

        void AddCard(ArtifactData a)
        {
            var card = UIKit.Surface(list, "Card_" + a.artifactId, SurfaceStyle.Glass, 40);
            UIKit.Layout(card, 420);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Surface>().fill;
            var colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.83f, 0.8f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                AudioManager.Instance.Click();
                onSelect?.Invoke(a);
            });

            // Thumbnail bersudut bulat (Mask stensil pada bingkai).
            var thumbFrame = UIKit.Panel(card.transform, "Thumb", Theme.Stage, true, false, 30);
            UIKit.Place(thumbFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(300f, 372f));
            thumbFrame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            if (a.thumbnail != null)
            {
                var raw = UIKit.Rect("Image", thumbFrame.transform).gameObject.AddComponent<RawImage>();
                raw.texture = a.thumbnail;
                raw.raycastTarget = false;
                UIKit.Stretch(raw.rectTransform);
                var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = (float)a.thumbnail.width / Mathf.Max(1, a.thumbnail.height);
            }

            var info = UIKit.Rect("Info", card.transform);
            UIKit.Stretch(info, 356, 92, 36, 28);
            var col = UIKit.VColumn(info, 8f);
            col.childForceExpandHeight = false;

            AddLine(info, a.displayName.Get(), Theme.Heading, Theme.Ink, FontStyles.Bold, 110);
            if (!string.IsNullOrEmpty(a.localName)) AddLine(info, a.localName, Theme.Small, Theme.AccentText, FontStyles.Italic, 44);
            AddLine(info, a.region.Get(), Theme.Small, Theme.InkMuted, FontStyles.Normal, 84);
            AddLine(info, a.era.Get(), Theme.Small, Theme.InkMuted, FontStyles.Normal, 44);
            if (a.isPlaceholder) AddLine(info, Locale.T("catalog.placeholder"), 26f, Theme.AccentText, FontStyles.Normal, 40);

            var go = UIKit.Surface(card.transform, "Open", SurfaceStyle.Accent, 32, false, false);
            UIKit.Place(go.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(64f, 64f));
            UIKit.IconImage(go.transform, Icon.ChevronRight, 34f, Theme.OnAccent);
        }

        static void AddLine(RectTransform parent, string text, float size, Color color, FontStyles style, float height)
        {
            if (string.IsNullOrEmpty(text)) return;
            var t = UIKit.Text(parent, "Line", text, size, color, TextAlignmentOptions.TopLeft, style);
            t.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Layout(t, height);
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
