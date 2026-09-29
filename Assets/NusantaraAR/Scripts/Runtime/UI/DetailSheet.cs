using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Lembar detail kaca di 3D Viewer (di atas <see cref="BottomNav"/>): nama, nama lokal, Asal & Era, ringkasan
    /// (saat dibuka lewat pegangan), CTA "Scan QR (AR)", serta "Letakkan di Meja" (HP ber-ARCore) & "Tampilkan QR".
    /// </summary>
    public class DetailSheet : MonoBehaviour
    {
        /// <summary>Bagian bawah lembar yang terselip di bawah bar navigasi.</summary>
        public const float Tuck = 48f;

        RectTransform rt;
        TextMeshProUGUI title, localName, originValue, eraValue, summary, placeholder;
        RectTransform secondaryRow;
        Button scanButton, tableButton, qrButton;
        ArtifactData data;
        bool expanded;
        float lastHeight = -1f;

        public event Action HeightChanged;

        /// <summary>Tinggi yang terlihat di atas bar navigasi.</summary>
        public float VisibleHeight => gameObject.activeSelf ? Mathf.Max(0f, rt.rect.height - Tuck) : 0f;

        public static DetailSheet Create(RectTransform safeRoot, Action onScan, Action onTable, Action onShowQr)
        {
            var root = UIKit.Surface(safeRoot, "DetailSheet", SurfaceStyle.Glass, 48);
            var sheet = root.gameObject.AddComponent<DetailSheet>();
            sheet.rt = root.rectTransform;
            sheet.Build(onScan, onTable, onShowQr);
            return sheet;
        }

        void Build(Action onScan, Action onTable, Action onShowQr)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, BottomNav.Height - Tuck);
            rt.sizeDelta = new Vector2(0f, 400f);
            var col = UIKit.VColumn(rt, 10f, new RectOffset(48, 48, 14, (int)(Tuck + BottomNav.Protrusion + 14f)));
            col.childForceExpandHeight = false;
            rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Pegangan: ketuk (atau ketuk judul) = buka/tutup ringkasan.
            var handleHit = UIKit.Panel(rt, "Handle", new Color(1f, 1f, 1f, 0f), false, true);
            handleHit.canvasRenderer.cullTransparentMesh = true;
            UIKit.Layout(handleHit, 26f);
            var bar = UIKit.Panel(handleHit.transform, "Bar", Theme.WithAlpha(Theme.InkMuted, 0.35f), true, false, 5);
            UIKit.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 10f));
            AddToggle(handleHit);

            title = UIKit.Text(rt, "Title", "", Theme.Heading, Theme.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.raycastTarget = true;
            UIKit.Layout(title, 60f);
            AddToggle(title);
            localName = UIKit.Text(rt, "LocalName", "", 28f, Theme.AccentText, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
            UIKit.Layout(localName, 38f);

            originValue = InfoRow("Origin", "sheet.region");
            eraValue = InfoRow("Era", "detail.era");
            placeholder = UIKit.Text(rt, "Placeholder", "", 24f, Theme.AccentText);
            LocalizedLabel.Attach(placeholder, "catalog.placeholder");
            UIKit.Layout(placeholder, 32f);

            summary = UIKit.Text(rt, "Summary", "", 28f, Theme.Ink);
            summary.lineSpacing = 6f;
            UIKit.Layout(summary, 10f);

            var gap = UIKit.Rect("Gap", rt);
            UIKit.Layout(gap, 6f);
            scanButton = UIKit.IconTextButton(rt, "ScanCard", Icon.ScanFrame, Locale.T("marker.scan"), ButtonStyle.Primary,
                () => onScan?.Invoke(), out var sl, 36f);
            LocalizedLabel.Attach(sl, "marker.scan");
            UIKit.Layout(scanButton, 108f);

            secondaryRow = UIKit.Rect("SecondaryAR", rt);
            UIKit.Layout(secondaryRow, 84f);
            UIKit.HRow(secondaryRow, 18f);
            tableButton = UIKit.IconTextButton(secondaryRow, "PlaceOnTable", Icon.Cube, Locale.T("marker.placeOnTable"),
                ButtonStyle.Chip, () => onTable?.Invoke(), out var tl, 28f);
            LocalizedLabel.Attach(tl, "marker.placeOnTable");
            qrButton = UIKit.IconTextButton(secondaryRow, "ShowCard", Icon.Qr, Locale.T("marker.showCard"),
                ButtonStyle.Chip, () => onShowQr?.Invoke(), out var cl, 28f);
            LocalizedLabel.Attach(cl, "marker.showCard");

            Locale.Changed += Refresh;
        }

        void OnDestroy() => Locale.Changed -= Refresh;

        TextMeshProUGUI InfoRow(string name, string labelKey)
        {
            var row = UIKit.Rect(name, rt);
            UIKit.Layout(row, 38f);
            var h = UIKit.HRow(row, 16f, null, false);
            h.childAlignment = TextAnchor.MiddleLeft;
            var label = UIKit.Text(row, "Label", Locale.T(labelKey), 28f, Theme.InkMuted, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Attach(label, labelKey);
            UIKit.Layout(label, -1, 110f);
            var value = UIKit.Text(row, "Value", "", 28f, Theme.Ink, TextAlignmentOptions.MidlineLeft);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Layout(value, -1, -1, 1f);
            return value;
        }

        void AddToggle(Graphic g)
        {
            var b = g.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                AudioManager.Instance.Click();
                expanded = !expanded;
                Refresh();
            });
        }

        public void Bind(ArtifactData artifact)
        {
            data = artifact;
            expanded = false;
            Refresh();
        }

        /// <param name="table">"Letakkan di Meja" (butuh ARCore).</param>
        /// <param name="qr">Artefak punya kode QR.</param>
        public void SetAvailability(bool table, bool qr)
        {
            UIKit.SetVisible(tableButton, table);
            UIKit.SetVisible(qrButton, qr);
            UIKit.SetVisible(secondaryRow, table || qr);
            scanButton.interactable = qr;
        }

        void Refresh()
        {
            if (data == null) return;
            title.text = data.displayName.Get();
            SetOptional(localName, data.localName);
            SetOptional(originValue, data.region.Get(), originValue.transform.parent);
            SetOptional(eraValue, data.era.Get(), eraValue.transform.parent);
            UIKit.SetVisible(placeholder, data.isPlaceholder);

            string s = data.summary.Get();
            bool showSummary = expanded && !string.IsNullOrEmpty(s);
            UIKit.SetVisible(summary, showSummary);
            if (showSummary)
            {
                summary.text = s;
                float width = Mathf.Max(200f, rt.rect.width - 96f);
                UIKit.Layout(summary, Mathf.Min(summary.GetPreferredValues(s, width, 0f).y + 8f, 420f));
            }
            // Tinggi langsung benar (bukan di frame berikutnya) agar framing kamera 3D Viewer memakai nilai akhir.
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        static void SetOptional(TextMeshProUGUI t, string value, Transform container = null)
        {
            bool has = !string.IsNullOrEmpty(value);
            UIKit.SetVisible(container != null ? container : t.transform, has);
            if (has) t.text = value;
        }

        void LateUpdate()
        {
            float h = rt.rect.height;
            if (Mathf.Abs(h - lastHeight) < 0.5f) return;
            lastHeight = h;
            HeightChanged?.Invoke();
        }

        void OnDisable()
        {
            lastHeight = -1f;
            HeightChanged?.Invoke();
        }
    }
}
