using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>Layar 1: katalog koleksi (PRD §5.2). Kategori tanpa konten tidak ditampilkan.</summary>
    public class CatalogScreen : MonoBehaviour
    {
        ContentCatalog catalog;
        Action<ArtifactData> onSelect;
        RectTransform chips, list;
        ArtifactCategory? filter;

        public static CatalogScreen Create(RectTransform fullRoot, ContentCatalog catalog, Action<ArtifactData> onSelect, Action onSettings, Action onScan)
        {
            var bg = UIKit.Panel(fullRoot, "CatalogScreen", Theme.Teak, false, true);
            UIKit.Stretch(bg.rectTransform);
            var area = UIKit.Rect("Safe", bg.transform);
            area.gameObject.AddComponent<SafeArea>();
            var screen = bg.gameObject.AddComponent<CatalogScreen>();
            screen.catalog = catalog;
            screen.onSelect = onSelect;
            screen.Build(area, onSettings);
            // Tombol mengambang: langsung buka kamera dan pindai kode QR (PRD Layar 1, "Masuk ke Mode AR").
            var scan = UIKit.Button(area, "Scan", Locale.T("marker.scan"), ButtonStyle.Primary, () => onScan?.Invoke(), out var sl, 38f);
            LocalizedLabel.Attach(sl, "marker.scan");
            UIKit.Place((RectTransform)scan.transform, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(640f, 120f));
            return screen;
        }

        void Build(RectTransform area, Action onSettings)
        {
            var title = UIKit.Text(area, "Title", Locale.T("app.title"), Theme.Title, Theme.Gold, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -48f), new Vector2(700f, 80f));
            var tagline = UIKit.Text(area, "Tagline", Locale.T("app.tagline"), Theme.Small, Theme.Stone);
            LocalizedLabel.Attach(tagline, "app.tagline");
            UIKit.Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -130f), new Vector2(900f, 50f));
            var settings = UIKit.Button(area, "Settings", Locale.T("common.settings"), ButtonStyle.Chip, () => onSettings?.Invoke(), out var sl, 30f);
            LocalizedLabel.Attach(sl, "common.settings");
            UIKit.Place((RectTransform)settings.transform, new Vector2(1f, 1f), new Vector2(-40f, -52f), new Vector2(250f, 84f));

            chips = UIKit.Rect("Categories", area);
            UIKit.Place(chips, new Vector2(0f, 1f), new Vector2(48f, -206f), new Vector2(984f, 84f));
            var row = UIKit.HRow(chips, 16f, null, false);
            row.childAlignment = TextAnchor.MiddleLeft;

            var scroll = UIKit.VerticalScroll(area, "List", out list, 28f, new RectOffset(48, 48, 12, 60));
            UIKit.Stretch((RectTransform)scroll.transform, 0, 0, 316, 190);

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
                var empty = UIKit.Text(list, "Empty", Locale.T("catalog.empty"), Theme.Body, Theme.Stone);
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
            UIKit.Layout(b, 84, Mathf.Max(180f, l.GetPreferredValues(label).x + 64f));
        }

        void AddCard(ArtifactData a)
        {
            var card = UIKit.Panel(list, "Card_" + a.artifactId, Theme.TextPanel, true, true, 32);
            UIKit.Layout(card, 420);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.onClick.AddListener(() =>
            {
                AudioManager.Instance.Click();
                onSelect?.Invoke(a);
            });

            var thumbFrame = UIKit.Panel(card.transform, "Thumb", Theme.WithAlpha(Theme.Border, 1f), true, false, 24);
            UIKit.Place(thumbFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(300f, 372f));
            if (a.thumbnail != null)
            {
                var raw = UIKit.Rect("Image", thumbFrame.transform).gameObject.AddComponent<RawImage>();
                raw.texture = a.thumbnail;
                raw.raycastTarget = false;
                UIKit.Stretch(raw.rectTransform, 6, 6, 6, 6);
                var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)a.thumbnail.width / Mathf.Max(1, a.thumbnail.height);
            }

            var info = UIKit.Rect("Info", card.transform);
            UIKit.Stretch(info, 352, 28, 32, 28);
            var col = UIKit.VColumn(info, 8f);
            col.childForceExpandHeight = false;

            AddLine(info, a.displayName.Get(), Theme.Heading, Theme.Parchment, FontStyles.Bold, 110);
            if (!string.IsNullOrEmpty(a.localName)) AddLine(info, a.localName, Theme.Small, Theme.Gold, FontStyles.Italic, 44);
            AddLine(info, a.region.Get(), Theme.Small, Theme.Stone, FontStyles.Normal, 84);
            AddLine(info, a.era.Get(), Theme.Small, Theme.Stone, FontStyles.Normal, 44);
            if (a.isPlaceholder) AddLine(info, Locale.T("catalog.placeholder"), 26f, Theme.Gold, FontStyles.Normal, 40);
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
