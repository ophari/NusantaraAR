using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Anotasi bagian artefak langsung di AR (PRD FR-08): titik emas yang menempel di model, garis penunjuk,
    /// dan label nama bagian yang disusun di kiri/kanan objek tanpa saling tumpuk. Mengetuk label/titik/bagian
    /// model membuka <see cref="HotspotCard"/> di samping bagian tersebut; kartu ikut bergerak bersama objek.
    /// Titik yang tertutup geometri diredupkan; label bisa disembunyikan lewat <see cref="LabelsVisible"/>.
    /// </summary>
    public class HotspotOverlay : MonoBehaviour
    {
        const float DotTouch = 96f;       // unit referensi 1080p ~ 36 dp; label di sebelahnya ikut jadi area sentuh
        const float LabelHeight = 68f;
        const float LabelGap = 12f;
        const float Reach = 120f;         // jarak mendatar titik -> label/kartu
        const float SideHysteresis = 40f;
        const float EdgeMargin = 16f;
        const float TopReserve = 150f;    // tombol Kembali / kontrol kanan-atas
        const float BottomReserve = 290f; // dock + label tahap
        const float Smoothing = 14f;

        class Callout
        {
            public HotspotData data;
            public RectTransform dot, pulse, core, line, label;
            public CanvasGroup dotGroup, lineGroup, labelGroup;
            public Image lineImage, pulseImage;
            public bool visible, occluded, right, placed;
            public Vector2 anchor, labelTarget, labelCenter;
            public float appear;
        }

        readonly List<Callout> callouts = new List<Callout>();
        readonly List<Callout> ordered = new List<Callout>();
        readonly List<Callout> placedSide = new List<Callout>();
        readonly RaycastHit[] hits = new RaycastHit[8];

        RectTransform lineLayer, dotLayer, labelLayer;
        HotspotCard card;
        Camera cam;
        ArtifactInstance artifact;
        Vector2 cardCenter;
        bool cardRight = true, cardPlaced;
        bool labelsVisible = true;

        public HotspotCard Card => card;
        public string SelectedId => card.Current?.hotspotId;
        public event Action<HotspotData> HotspotTapped;

        public bool LabelsVisible
        {
            get => labelsVisible;
            set => labelsVisible = value;
        }

        /// <summary>Bagian yang sedang diceritakan mode Kisah: titik & labelnya ditonjolkan, yang lain diredupkan.</summary>
        public string FocusId { get; set; }

        public static HotspotOverlay Create(RectTransform parent)
        {
            var rt = UIKit.Rect("Hotspots", parent);
            UIKit.Stretch(rt);
            var o = rt.gameObject.AddComponent<HotspotOverlay>();
            o.lineLayer = UIKit.Stretch(UIKit.Rect("Lines", rt));
            o.dotLayer = UIKit.Stretch(UIKit.Rect("Dots", rt));
            o.labelLayer = UIKit.Stretch(UIKit.Rect("Labels", rt));
            o.card = HotspotCard.Create(rt);
            return o;
        }

        public void Bind(ArtifactInstance instance, Camera camera)
        {
            artifact = instance;
            cam = camera;
            card.Close();
            foreach (var c in callouts)
            {
                if (c.dot != null) Destroy(c.dot.gameObject);
                if (c.line != null) Destroy(c.line.gameObject);
                if (c.label != null) Destroy(c.label.gameObject);
            }
            callouts.Clear();
            if (artifact == null || artifact.Data == null) return;
            foreach (var h in artifact.Data.hotspots) callouts.Add(CreateCallout(h));
            Locale.Changed -= RefreshTitles;
            Locale.Changed += RefreshTitles;
        }

        void OnDestroy() => Locale.Changed -= RefreshTitles;

        // ------------------------------------------------------------------ pembuatan

        Callout CreateCallout(HotspotData h)
        {
            var c = new Callout { data = h };

            c.line = UIKit.Rect("Line_" + h.hotspotId, lineLayer);
            c.line.anchorMin = c.line.anchorMax = new Vector2(0.5f, 0.5f);
            c.line.pivot = new Vector2(0f, 0.5f);
            c.lineImage = c.line.gameObject.AddComponent<Image>();
            c.lineImage.color = Theme.Gold;
            c.lineImage.raycastTarget = false;
            c.lineGroup = c.line.gameObject.AddComponent<CanvasGroup>();

            c.dot = UIKit.Rect("Dot_" + h.hotspotId, dotLayer);
            c.dot.anchorMin = c.dot.anchorMax = new Vector2(0.5f, 0.5f);
            c.dot.sizeDelta = new Vector2(DotTouch, DotTouch);
            var hit = c.dot.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0); // area sentuh transparan
            var dotButton = c.dot.gameObject.AddComponent<Button>();
            dotButton.transition = Selectable.Transition.None;
            dotButton.onClick.AddListener(() => Tap(h));
            c.dotGroup = c.dot.gameObject.AddComponent<CanvasGroup>();
            c.pulse = MakeCircle(c.dot, "Pulse", 50f, Theme.Gold, true);
            c.pulseImage = c.pulse.GetComponent<Image>();
            MakeCircle(c.dot, "Halo", 42f, Theme.Chrome, false);
            c.core = MakeCircle(c.dot, "Core", 22f, Theme.Gold, false);

            // Label: pil emas "Nama Bagian (i)" seperti papan keterangan museum.
            var pill = UIKit.Panel(labelLayer, "Label_" + h.hotspotId, Theme.Gold, true, true, 34);
            c.label = pill.rectTransform;
            c.label.anchorMin = c.label.anchorMax = c.label.pivot = new Vector2(0.5f, 0.5f);
            var row = UIKit.HRow(c.label, 12f, new RectOffset(24, 12, 0, 0), false);
            row.childForceExpandHeight = false;
            var fitter = c.label.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            c.label.sizeDelta = new Vector2(200f, LabelHeight);
            var title = UIKit.Text(c.label, "Title", h.title.Get(), 28f, Theme.Teak, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            var icon = UIKit.Panel(c.label, "Info", Theme.Teak, false, false);
            icon.sprite = SpriteFactory.Circle();
            UIKit.Layout(icon, 44f, 44f);
            var i = UIKit.Text(icon.transform, "i", "i", 28f, Theme.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Stretch(i.rectTransform);
            var labelButton = pill.gameObject.AddComponent<Button>();
            labelButton.targetGraphic = pill;
            labelButton.onClick.AddListener(() => Tap(h));
            c.labelGroup = pill.gameObject.AddComponent<CanvasGroup>();

            SetShown(c, false);
            return c;
        }

        static RectTransform MakeCircle(RectTransform parent, string name, float size, Color color, bool ring)
        {
            var img = UIKit.Panel(parent, name, color, false, false);
            img.sprite = SpriteFactory.Circle(ring, 0.1f);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        void RefreshTitles()
        {
            foreach (var c in callouts)
                if (c.label != null) c.label.GetComponentInChildren<TextMeshProUGUI>().text = c.data.title.Get();
        }

        void Tap(HotspotData h)
        {
            AudioManager.Instance.Click();
            HotspotTapped?.Invoke(h);
        }

        // ------------------------------------------------------------------ API

        /// <summary>
        /// Hotspot yang sedang tampil di layar, sesuai urutan data (untuk navigasi sebelum/berikutnya).
        /// Bila belum ada frame yang diproses (baru di-Bind), jatuh ke hotspot yang boleh tampil.
        /// </summary>
        public List<HotspotData> ShownHotspots()
        {
            var list = new List<HotspotData>();
            foreach (var c in callouts)
                if (c.visible) list.Add(c.data);
            if (list.Count == 0 && artifact != null && artifact.Data != null)
                foreach (var h in artifact.Data.hotspots)
                    if (artifact.IsHotspotAvailable(h)) list.Add(h);
            return list;
        }

        /// <summary>Hotspot bagian model di bawah titik layar (ketuk langsung pada model), atau null.</summary>
        public HotspotData PickAt(Vector2 screen)
        {
            if (cam == null || artifact == null || artifact.Data == null || !artifact.gameObject.activeInHierarchy) return null;
            var ray = cam.ScreenPointToRay(screen);
            int count = Physics.RaycastNonAlloc(ray, hits, 50f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit? nearest = null;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider == null || hits[i].collider.GetComponentInParent<ArtifactInstance>() != artifact) continue;
                if (nearest == null || hits[i].distance < nearest.Value.distance) nearest = hits[i];
            }
            if (nearest == null) return null;
            var part = nearest.Value.collider.GetComponentInParent<ArtifactPart>();
            var point = nearest.Value.point;

            // Utamakan hotspot milik bagian yang diketuk; bila tidak ada, ambil hotspot terdekat dari titik ketuk.
            HotspotData best = null;
            float bestScore = float.MaxValue;
            foreach (var h in artifact.Data.hotspots)
            {
                if (!artifact.IsHotspotAvailable(h) || !artifact.TryGetHotspotWorldPosition(h, out var p)) continue;
                float score = (p - point).sqrMagnitude;
                if (part == null || h.partName != part.partName) score += 1e6f;
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        // ------------------------------------------------------------------ tata letak per frame

        void LateUpdate()
        {
            if (cam == null || artifact == null) return;
            var container = (RectTransform)transform;
            var size = container.rect.size;
            float halfW = size.x * 0.5f, halfH = size.y * 0.5f;
            float top = halfH - TopReserve, bottom = -halfH + BottomReserve;
            float t = 1f - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
            float pulsePhase = Mathf.Repeat(Time.unscaledTime * 0.9f, 1f);
            string selectedId = SelectedId;

            // 1) Proyeksikan titik; tentukan yang terlihat.
            float sumX = 0f;
            int visibleCount = 0;
            foreach (var c in callouts)
            {
                c.visible = false;
                if (!artifact.gameObject.activeInHierarchy || !artifact.IsHotspotAvailable(c.data)) continue;
                if (!artifact.TryGetHotspotWorldPosition(c.data, out var world) || !Project(world, container, out c.anchor)) continue;
                c.visible = true;
                c.occluded = IsOccluded(world);
                sumX += c.anchor.x;
                visibleCount++;
            }
            float centerX = visibleCount > 0 ? sumX / visibleCount : 0f;

            // 2) Label kiri/kanan dari pusat objek (dengan histeresis agar tidak berkedip saat objek berputar).
            ordered.Clear();
            foreach (var c in callouts)
            {
                if (!c.visible) continue;
                float dx = c.anchor.x - centerX;
                if (!c.placed) c.right = dx >= 0f;
                else if (c.right && dx < -SideHysteresis) c.right = false;
                else if (!c.right && dx > SideHysteresis) c.right = true;
                if (c.data.hotspotId != selectedId) ordered.Add(c);
            }
            ordered.Sort((a, b) => b.anchor.y.CompareTo(a.anchor.y));

            // 3) Susun label dari atas ke bawah: turunkan label yang bertabrakan dengan label yang sudah diletakkan.
            placedSide.Clear();
            foreach (var c in ordered)
            {
                float w = Mathf.Max(c.label.rect.width, 120f);
                float cx = c.right ? c.anchor.x + Reach + w * 0.5f : c.anchor.x - Reach - w * 0.5f;
                cx = Mathf.Clamp(cx, -halfW + EdgeMargin + w * 0.5f, halfW - EdgeMargin - w * 0.5f);
                float cy = Mathf.Min(c.anchor.y, top - LabelHeight * 0.5f);
                for (int guard = 0; guard < placedSide.Count + 1; guard++)
                {
                    bool moved = false;
                    foreach (var p in placedSide)
                    {
                        float pw = Mathf.Max(p.label.rect.width, 120f);
                        bool overlapX = Mathf.Abs(p.labelTarget.x - cx) < (pw + w) * 0.5f + LabelGap;
                        bool overlapY = Mathf.Abs(p.labelTarget.y - cy) < LabelHeight + LabelGap;
                        if (overlapX && overlapY)
                        {
                            cy = p.labelTarget.y - LabelHeight - LabelGap;
                            moved = true;
                        }
                    }
                    if (!moved) break;
                }
                cy = Mathf.Max(cy, bottom + LabelHeight * 0.5f);
                // Tabrakan dihitung dari target (bukan posisi yang sedang dihaluskan) agar susunan stabil.
                c.labelTarget = new Vector2(cx, cy);
                c.labelCenter = c.placed ? Vector2.Lerp(c.labelCenter, c.labelTarget, t) : c.labelTarget;
                c.label.anchoredPosition = c.labelCenter;
                placedSide.Add(c);
            }

            // 4) Terapkan ke UI.
            Callout selected = null;
            foreach (var c in callouts)
                if (c.visible && c.data.hotspotId == selectedId) selected = c;
            bool cardOpen = card.IsOpen && selected != null;
            Callout focused = null;
            if (!cardOpen && !string.IsNullOrEmpty(FocusId))
                foreach (var c in callouts)
                    if (c.visible && c.data.hotspotId == FocusId) focused = c;
            foreach (var c in callouts)
            {
                if (!c.visible)
                {
                    SetShown(c, false);
                    c.placed = false;
                    c.appear = 0f;
                    continue;
                }
                bool isSelected = c == selected;
                bool isFocused = c == focused;
                c.placed = true;
                c.appear = Mathf.MoveTowards(c.appear, 1f, Time.unscaledDeltaTime * 5f);
                float ease = 1f - (1f - c.appear) * (1f - c.appear);

                c.dot.anchoredPosition = c.anchor;
                UIKit.SetVisible(c.dot, true);
                float dim = c.occluded && !isFocused ? 0.45f : 1f;
                if ((cardOpen && !isSelected) || (focused != null && !isFocused)) dim *= 0.4f;
                c.dotGroup.alpha = dim;
                c.core.localScale = Vector3.one * (isSelected || isFocused ? 1.5f : 1f);
                c.pulse.localScale = Vector3.one * (1f + (isFocused ? 0.9f : 0.5f) * pulsePhase);
                c.pulseImage.color =Theme.WithAlpha(Theme.Gold, (1f - pulsePhase) * (isSelected || isFocused ? 1f : 0.8f));

                bool showLabel = (labelsVisible || isFocused) && !isSelected;
                UIKit.SetVisible(c.label, showLabel);
                if (showLabel)
                {
                    c.labelGroup.alpha = dim * ease;
                    c.label.localScale = Vector3.one * (Mathf.Lerp(0.6f, 1f, ease) * (isFocused ? 1.12f : 1f));
                    float w = c.label.rect.width;
                    var edge = new Vector2(c.labelCenter.x + (c.right ? -w * 0.5f : w * 0.5f), c.labelCenter.y);
                    DrawLine(c, c.anchor, edge, dim * ease);
                }
                else if (!isSelected) UIKit.SetVisible(c.line, false);
            }

            // 5) Kartu info menempel di samping bagian yang dipilih.
            card.SetShown(cardOpen);
            if (cardOpen) PlaceCard(selected, halfW, top, bottom, t);
            else cardPlaced = false;
        }

        void PlaceCard(Callout c, float halfW, float top, float bottom, float t)
        {
            float w = HotspotCard.Width;
            float h = card.Rect.rect.height;
            float roomRight = halfW - EdgeMargin - (c.anchor.x + Reach);
            float roomLeft = (c.anchor.x - Reach) - (-halfW + EdgeMargin);
            bool fitsRight = roomRight >= w, fitsLeft = roomLeft >= w;
            if (!cardPlaced) cardRight = fitsRight || (!fitsLeft && roomRight >= roomLeft);
            else if (cardRight && !fitsRight && fitsLeft) cardRight = false;
            else if (!cardRight && !fitsLeft && fitsRight) cardRight = true;

            float cx = cardRight ? c.anchor.x + Reach + w * 0.5f : c.anchor.x - Reach - w * 0.5f;
            cx = Mathf.Clamp(cx, -halfW + EdgeMargin + w * 0.5f, halfW - EdgeMargin - w * 0.5f);
            float cy = h >= top - bottom ? (top + bottom) * 0.5f : Mathf.Clamp(c.anchor.y, bottom + h * 0.5f, top - h * 0.5f);
            var target = new Vector2(cx, cy);
            cardCenter = cardPlaced ? Vector2.Lerp(cardCenter, target, t) : target;
            cardPlaced = true;
            card.Rect.anchoredPosition = cardCenter;

            float edgeX = cardCenter.x + (cardRight ? -w * 0.5f : w * 0.5f);
            float edgeY = Mathf.Clamp(c.anchor.y, cardCenter.y - h * 0.5f + 40f, cardCenter.y + h * 0.5f - 40f);
            DrawLine(c, c.anchor, new Vector2(edgeX, edgeY), 1f);
        }

        static void DrawLine(Callout c, Vector2 from, Vector2 to, float alpha)
        {
            var d = to - from;
            float len = d.magnitude;
            if (len < 1f)
            {
                UIKit.SetVisible(c.line, false);
                return;
            }
            UIKit.SetVisible(c.line, true);
            c.line.anchoredPosition = from;
            c.line.sizeDelta = new Vector2(len, 4f);
            c.line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            c.lineGroup.alpha = alpha;
        }

        static void SetShown(Callout c, bool shown)
        {
            UIKit.SetVisible(c.dot, shown);
            UIKit.SetVisible(c.line, shown);
            UIKit.SetVisible(c.label, shown);
        }

        bool Project(Vector3 world, RectTransform container, out Vector2 local)
        {
            var p = cam.WorldToScreenPoint(world);
            local = default;
            if (p.z <= 0f || p.x < 0f || p.y < 0f || p.x > Screen.width || p.y > Screen.height) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(container, p, null, out local);
            return true;
        }

        bool IsOccluded(Vector3 world)
        {
            var origin = cam.transform.position;
            var dir = world - origin;
            float dist = dir.magnitude;
            if (dist < 1e-4f) return false;
            int count = Physics.RaycastNonAlloc(origin, dir / dist, hits, dist - 0.002f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && hits[i].collider.GetComponentInParent<ArtifactInstance>() == artifact)
                    return true;
            return false;
        }
    }
}
