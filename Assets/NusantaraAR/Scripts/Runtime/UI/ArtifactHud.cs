using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>Tata letak <see cref="ArtifactHud"/> per layar.</summary>
    public class HudOptions
    {
        /// <summary>Jarak rel & area label dari tepi atas area aman (di bawah top bar).</summary>
        public float topInset = 150f;
        /// <summary>Tinggi UI milik layar di bawah (sheet + nav di 3D Viewer). Mode kamera: 0 (panel kontrol milik HUD).</summary>
        public float bottomInset;
        /// <summary>Posisi panel Kisah dari dasar area aman.</summary>
        public float storyBottom = 28f;
        /// <summary>Tombol miring atas/bawah (3D Viewer & Scan QR; AR Meja tidak).</summary>
        public bool tiltButtons = true;
        /// <summary>Panel skala + saklar di mode kamera; null di 3D Viewer.</summary>
        public ControlPanelConfig panel;
    }

    /// <summary>
    /// Kontrol artefak yang sama di 3D Viewer, Scan QR, dan AR Meja (PRD Layar 2 & 4): rel kaca kanan (Kisah,
    /// Bongkar/Gabung, Hunus/Sarungkan, Label, Putar 360°, Reset), klaster tahan-tekan kiri (putar & miring),
    /// panel skala di mode kamera, pil tahap exploded view, label bagian + kartu info yang menempel di objek, dan panel
    /// mode Kisah (menggantikan slot bawah selama narasi bercerita).
    /// </summary>
    public class ArtifactHud : MonoBehaviour
    {
        const float RotateSpeed = 90f, TiltSpeed = 60f;
        const float Margin = 24f, RailWidth = 136f, NudgeSize = 96f;

        /// <summary>Lebar (unit kanvas) yang tertutup klaster kiri / rel kanan, termasuk margin.</summary>
        public const float LeftReserve = Margin + NudgeSize + 12f, RightReserve = Margin + RailWidth + 12f;

        class RailItem
        {
            public Button button;
            public Image fill, icon;
            public TextMeshProUGUI label;
        }

        HudOptions options;
        RectTransform rail, nudge;
        RailItem story, explode, draw, labels, rotate;
        readonly List<RailItem> railItems = new List<RailItem>();
        Image stagePill;
        TextMeshProUGUI stageText;
        HotspotOverlay overlay;
        StoryPanel storyPanel;
        ControlPanel panel;

        ArtifactInstance artifact;
        Action onReset;
        Action<float, float> onNudge;
        bool controlsVisible = true;

        public HotspotOverlay Overlay => overlay;
        public StoryPanel Story => storyPanel;
        public ControlPanel Panel => panel;

        /// <param name="safeRoot">Area aman (tombol).</param>
        /// <param name="fullRoot">Layar penuh (label bagian & kartu info).</param>
        public static ArtifactHud Create(RectTransform safeRoot, RectTransform fullRoot, HudOptions options)
        {
            var hud = safeRoot.gameObject.AddComponent<ArtifactHud>();
            hud.options = options ?? new HudOptions();
            hud.overlay = HotspotOverlay.Create(fullRoot);
            hud.overlay.transform.SetAsFirstSibling();
            hud.Build(safeRoot);
            hud.overlay.HotspotTapped += hud.OnHotspotTapped;
            hud.overlay.Card.Stepped += hud.Step;
            hud.UpdateLayout();
            return hud;
        }

        void Build(RectTransform safeRoot)
        {
            rail = UIKit.Rect("Rail", safeRoot);
            UIKit.Place(rail, new Vector2(1f, 1f), new Vector2(-Margin, -options.topInset), new Vector2(RailWidth, 900f));
            var col = UIKit.VColumn(rail, 14f);
            col.childAlignment = TextAnchor.UpperRight;
            story = AddRail("Story", Icon.Book, "dock.story", PlayStory);
            explode = AddRail("Explode", Icon.Layers, "dock.explode", ToggleExplode);
            draw = AddRail("Draw", Icon.Blade, "dock.draw", ToggleDraw);
            labels = AddRail("Labels", Icon.Tag, "dock.labels", ToggleLabels);
            rotate = AddRail("AutoRotate", Icon.Spin360, "rail.autorotate", ToggleAutoRotate);
            AddRail("Reset", Icon.Reset, "rail.reset", () => onReset?.Invoke());

            BuildNudge(safeRoot);

            if (options.panel != null) panel = ControlPanel.Create(safeRoot, options.panel);

            stagePill = UIKit.Surface(safeRoot, "StagePill", SurfaceStyle.Glass, 32, false);
            UIKit.Place(stagePill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(760f, 64f));
            stageText = UIKit.Text(stagePill.transform, "Text", "", 28f, Theme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Stretch(stageText.rectTransform, 20, 20, 0, 0);

            storyPanel = StoryPanel.Create(safeRoot, overlay);
            storyPanel.ActiveChanged += OnStoryActiveChanged;

            Locale.Changed += RefreshLabels;
        }

        RailItem AddRail(string name, Icon icon, string key, UnityEngine.Events.UnityAction onClick)
        {
            var item = new RailItem();
            item.button = UIKit.RailButton(rail, name, icon, Locale.T(key), onClick, out item.icon, out item.label);
            item.fill = item.button.GetComponent<Surface>().fill;
            UIKit.Layout(item.button, 128f, RailWidth);
            LocalizedLabel.Attach(item.label, key);
            railItems.Add(item);
            return item;
        }

        /// <summary>Klaster kiri: putar kiri/kanan (tahan-tekan) dan pil miring atas/bawah.</summary>
        void BuildNudge(RectTransform safeRoot)
        {
            nudge = UIKit.Rect("Nudge", safeRoot);
            // 2 tombol + jarak; dengan miring: jarak + celah 8 + jarak + pil 200.
            float height = NudgeSize * 2f + 16f + (options.tiltButtons ? 16f + 8f + 16f + 200f : 0f);
            UIKit.Place(nudge, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(NudgeSize, height));
            var col = UIKit.VColumn(nudge, 16f);
            col.childAlignment = TextAnchor.UpperCenter;

            AddHold(nudge, "RotateLeft", Icon.RotateLeft, NudgeSize, dt => Nudge(RotateSpeed * dt, 0f));
            AddHold(nudge, "RotateRight", Icon.RotateRight, NudgeSize, dt => Nudge(-RotateSpeed * dt, 0f));
            if (!options.tiltButtons) return;

            var gap = UIKit.Rect("Gap", nudge);
            UIKit.Layout(gap, 8f);
            var pill = UIKit.Surface(nudge, "Tilt", SurfaceStyle.GlassChrome, 48, false);
            UIKit.Layout(pill, 200f, NudgeSize);
            var fill = pill.GetComponent<Surface>().fill;
            foreach (var (name, icon, sign, anchor) in new[] { ("TiltUp", Icon.ArrowUp, 1f, 1f), ("TiltDown", Icon.ArrowDown, -1f, 0f) })
            {
                var half = UIKit.Panel(pill.transform, name, new Color(1f, 1f, 1f, 0f), false, true);
                half.canvasRenderer.cullTransparentMesh = true;
                UIKit.Place(half.rectTransform, new Vector2(0.5f, anchor), Vector2.zero, new Vector2(NudgeSize, 100f));
                var img = UIKit.IconImage(half.transform, icon, 42f, Theme.Ink);
                UIKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 42f));
                float s = sign;
                var hold = HoldButton.Attach(half, fill);
                hold.Held += dt => Nudge(0f, s * TiltSpeed * dt);
                hold.Pressed += StopAutoRotate;
            }
        }

        void AddHold(RectTransform parent, string name, Icon icon, float size, Action<float> held)
        {
            var root = UIKit.Surface(parent, name, SurfaceStyle.GlassChrome, Mathf.RoundToInt(size * 0.5f));
            UIKit.Layout(root, size, size);
            UIKit.IconImage(root.transform, icon, size * 0.46f, Theme.Ink);
            var hold = HoldButton.Attach(root, root.GetComponent<Surface>().fill);
            hold.Held += held;
            hold.Pressed += StopAutoRotate;
        }

        void OnDestroy() => Locale.Changed -= RefreshLabels;

        /// <param name="nudgeHandler">Putar (derajat, + = muka objek bergeser ke kiri layar) dan miring (+ = sisi atas menjauh).</param>
        public void Bind(ArtifactInstance instance, Camera cam, Action reset, Action<float, float> nudgeHandler)
        {
            if (artifact != null && artifact.exploded != null) artifact.exploded.StageChanged -= OnStageChanged;
            if (artifact != null && artifact.autoRotate != null) artifact.autoRotate.ActiveChanged -= OnAutoRotateChanged;
            artifact = instance;
            onReset = reset;
            onNudge = nudgeHandler;
            storyPanel.Bind(instance);
            overlay.Bind(instance, cam);
            if (panel != null) panel.SetTitle(instance != null && instance.Data != null ? instance.Data.displayName.Get() : "");
            if (artifact != null && artifact.exploded != null) artifact.exploded.StageChanged += OnStageChanged;
            if (artifact != null && artifact.autoRotate != null) artifact.autoRotate.ActiveChanged += OnAutoRotateChanged;
            RefreshLabels();
        }

        public void SetControlsVisible(bool visible)
        {
            controlsVisible = visible;
            UIKit.SetVisible(rail, visible);
            UIKit.SetVisible(nudge, visible);
            UIKit.SetVisible(overlay, visible);
            if (panel != null) UIKit.SetVisible(panel, visible && !storyPanel.IsActive);
            if (!visible) overlay.Card.Close();
            // Kisah dijeda (bukan dihentikan) selama kontrol tersembunyi, mis. kode QR sesaat hilang dari kamera.
            storyPanel.SetSuspended(!visible);
            UpdateLayout();
        }

        /// <summary>Tinggi UI layar di bawah HUD berubah (mis. sheet detail dibuka/ditutup).</summary>
        public void SetBottomInset(float value)
        {
            options.bottomInset = value;
            UpdateLayout();
        }

        void UpdateLayout()
        {
            float slotTop = storyPanel.IsActive ? options.storyBottom + StoryPanel.Height
                : panel != null && panel.gameObject.activeSelf ? 28f + panel.Height
                : options.bottomInset;
            storyPanel.SetBottom(options.storyBottom);
            stagePill.rectTransform.anchoredPosition = new Vector2(0f, slotTop + 16f);

            // Klaster kiri di tengah ruang antara top bar dan slot bawah.
            var safe = (RectTransform)transform;
            float free = safe.rect.height - options.topInset - slotTop;
            nudge.anchoredPosition = new Vector2(Margin, (slotTop - options.topInset) * 0.5f);
            UIKit.SetVisible(nudge, controlsVisible && free > nudge.sizeDelta.y + 40f);

            overlay.SetReserves(options.topInset, slotTop + 96f, LeftReserve, RightReserve);
        }

        void OnRectTransformDimensionsChange()
        {
            if (storyPanel != null) UpdateLayout();
        }

        /// <summary>Menutup kartu info atau menghentikan mode Kisah (tombol Kembali). True bila ada yang ditutup.</summary>
        public bool CloseInfo()
        {
            if (overlay.Card.IsOpen)
            {
                overlay.Card.Close();
                return true;
            }
            if (!storyPanel.IsActive) return false;
            storyPanel.Stop();
            return true;
        }

        /// <summary>Memulai mode Kisah dari bab pertama (dipakai juga oleh build QA).</summary>
        public void PlayStory()
        {
            if (!controlsVisible || !storyPanel.HasStory) return;
            storyPanel.Play();
        }

        /// <summary>Membuka kartu info bagian tertentu (dipakai juga oleh build QA).</summary>
        public void OpenHotspot(string hotspotId)
        {
            var h = artifact != null && artifact.Data != null ? artifact.Data.FindHotspot(hotspotId) : null;
            if (h != null) Open(h);
        }

        /// <summary>Ketuk di luar UI: ketuk bagian model membuka info-nya, ketuk tempat kosong menutup kartu.</summary>
        public void HandleTap(Vector2 screen)
        {
            // Selama Kisah berjalan, ketukan di model diabaikan agar cerita tidak terputus tanpa sengaja.
            if (!controlsVisible || artifact == null || storyPanel.IsActive) return;
            var h = overlay.PickAt(screen);
            if (h != null) Open(h);
            else CloseInfo();
        }

        void Update()
        {
            string text = null;
            if (controlsVisible && !storyPanel.IsActive && artifact != null && artifact.exploded != null
                && (artifact.exploded.CurrentStage > 0 || artifact.exploded.IsAnimating))
                text = artifact.exploded.CurrentStageLabel;
            bool show = !string.IsNullOrEmpty(text);
            UIKit.SetVisible(stagePill, show);
            if (show && stageText.text != text)
            {
                stageText.text = text;
                float w = stageText.GetPreferredValues(text, 10000f, 0f).x + 64f;
                stagePill.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(w, 240f, 900f), 64f);
            }
        }

        void RefreshLabels()
        {
            var ex = artifact != null ? artifact.exploded : null;
            UIKit.SetVisible(story.button, storyPanel.HasStory);
            SetRailLook(story, true); // selalu terakota: pintu masuk mode Kisah
            bool exploded = ex != null && ex.IsExplodedOrExploding;
            explode.label.text = Locale.T(exploded ? "dock.assemble" : "dock.explode");
            SetRailLook(explode, exploded);
            UIKit.SetVisible(draw.button, ex != null && ex.CanDraw);
            bool drawn = ex != null && ex.TargetStage == 1;
            draw.label.text = Locale.T(drawn ? "dock.sheathe" : "dock.draw");
            SetRailLook(draw, drawn);
            SetRailLook(labels, overlay.LabelsVisible);
            SetRailLook(rotate, artifact != null && artifact.autoRotate != null && artifact.autoRotate.Active);
        }

        /// <summary>Tombol rel yang sedang aktif: ikon & teks terakota di atas kaca bernuansa terakota.</summary>
        static void SetRailLook(RailItem item, bool on)
        {
            item.icon.color = on ? Theme.AccentText : Theme.Ink;
            item.label.color = on ? Theme.AccentText : Theme.Ink;
            item.label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            item.fill.color = on ? Color.Lerp(Theme.Surface, Theme.AccentSoft, 0.28f) : Theme.Surface;
        }

        void OnStoryActiveChanged(bool active)
        {
            if (panel != null) UIKit.SetVisible(panel, controlsVisible && !active);
            RefreshLabels();
            UpdateLayout();
        }

        void Nudge(float yawDeg, float tiltDeg)
        {
            if (!controlsVisible || artifact == null) return;
            onNudge?.Invoke(yawDeg, tiltDeg);
        }

        void StopAutoRotate()
        {
            if (artifact != null && artifact.autoRotate != null) artifact.autoRotate.Active = false;
        }

        void ToggleExplode()
        {
            if (artifact == null || artifact.exploded == null) return;
            artifact.exploded.Toggle();
            Analytics.Log("explode_toggle", ("artifact", artifact.Data != null ? artifact.Data.artifactId : ""),
                ("exploded", artifact.exploded.IsExplodedOrExploding));
            RefreshLabels();
        }

        /// <summary>Hunus / Sarungkan: animasi bilah dicabut dari sarung tanpa membongkar bagian lain.</summary>
        void ToggleDraw()
        {
            if (artifact == null || artifact.exploded == null) return;
            artifact.exploded.ToggleDraw();
            Analytics.Log("draw_toggle", ("artifact", artifact.Data != null ? artifact.Data.artifactId : ""),
                ("drawn", artifact.exploded.TargetStage == 1));
            RefreshLabels();
        }

        void ToggleAutoRotate()
        {
            if (artifact == null || artifact.autoRotate == null) return;
            artifact.autoRotate.Active = !artifact.autoRotate.Active;
        }

        void ToggleLabels()
        {
            overlay.LabelsVisible = !overlay.LabelsVisible;
            RefreshLabels();
        }

        void OnAutoRotateChanged(bool _) => RefreshLabels();

        void OnStageChanged(int stage)
        {
            RefreshLabels();
            // Info bagian bilah tidak berlaku lagi setelah keris digabung.
            var shown = overlay.Card.Current;
            if (stage == 0 && shown != null && shown.visibleFrom == HotspotStage.Bilah) CloseInfo();
        }

        void OnHotspotTapped(HotspotData h)
        {
            // Ketuk label yang sedang terbuka = tutup (toggle).
            if (overlay.Card.Current == h) CloseInfo();
            else Open(h);
        }

        /// <summary>Tombol sebelum/berikutnya di kartu: berpindah ke bagian lain yang sedang terlihat.</summary>
        void Step(int direction)
        {
            var list = overlay.ShownHotspots();
            if (list.Count == 0) return;
            int i = list.IndexOf(overlay.Card.Current);
            i = i < 0 ? 0 : (i + direction + list.Count) % list.Count;
            Open(list[i]);
        }

        void Open(HotspotData h)
        {
            if (artifact == null || artifact.Data == null) return;
            storyPanel.Stop(); // mengetuk label saat Kisah berjalan = beralih ke kartu info bagian itu
            if (artifact.autoRotate != null) artifact.autoRotate.Active = false;
            var list = overlay.ShownHotspots();
            overlay.Card.Show(artifact.Data, h, list.IndexOf(h), list.Count);
        }
    }
}
