using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Kontrol artefak yang sama di 3D Viewer dan AR (PRD Layar 2 & 4):
    /// floating controls (Reset Tampilan, Pindahkan), dock (Kisah, Bongkar/Gabung, Hunus, Putar Otomatis, Label),
    /// label tahap exploded view, label bagian + kartu info yang menempel langsung di objek, dan panel mode Kisah
    /// (menggantikan dock selama narasi bercerita).
    /// </summary>
    public class ArtifactHud : MonoBehaviour
    {
        RectTransform controls, dock;
        TextMeshProUGUI storyLabel, explodeLabel, drawLabel, rotateLabel, labelsLabel, stageText;
        Button storyButton, drawButton;
        Image stagePill;
        HotspotOverlay overlay;
        StoryPanel story;

        ArtifactInstance artifact;
        Action onReset;
        Action onMove;
        bool controlsVisible = true;

        public HotspotOverlay Overlay => overlay;
        public StoryPanel Story => story;

        /// <param name="safeRoot">Area aman (tombol).</param>
        /// <param name="fullRoot">Layar penuh (label bagian & kartu info).</param>
        /// <param name="topInset">Jarak kontrol kanan-atas dari tepi atas area aman.</param>
        /// <param name="includeMove">Tampilkan tombol "Pindahkan" (mode AR).</param>
        public static ArtifactHud Create(RectTransform safeRoot, RectTransform fullRoot, float topInset, bool includeMove)
        {
            var hud = safeRoot.gameObject.AddComponent<ArtifactHud>();
            hud.overlay = HotspotOverlay.Create(fullRoot);
            hud.overlay.transform.SetAsFirstSibling();
            hud.Build(safeRoot, topInset, includeMove);
            hud.overlay.HotspotTapped += hud.OnHotspotTapped;
            hud.overlay.Card.Stepped += hud.Step;
            return hud;
        }

        void Build(RectTransform safeRoot, float topInset, bool includeMove)
        {
            controls = UIKit.Rect("FloatingControls", safeRoot);
            UIKit.Place(controls, new Vector2(1f, 1f), new Vector2(-28f, -topInset), new Vector2(300f, includeMove ? 208f : 96f));
            UIKit.VColumn(controls, 16f);
            var reset = UIKit.Button(controls, "Reset", Locale.T("ctrl.reset"), ButtonStyle.Secondary, () => onReset?.Invoke(), out var rl, 30f);
            LocalizedLabel.Attach(rl, "ctrl.reset");
            UIKit.Layout(reset, 96);
            if (includeMove)
            {
                var move = UIKit.Button(controls, "Move", Locale.T("ctrl.move"), ButtonStyle.Secondary, () => onMove?.Invoke(), out var ml, 30f);
                LocalizedLabel.Attach(ml, "ctrl.move");
                UIKit.Layout(move, 96);
            }

            dock = UIKit.Rect("Dock", safeRoot);
            UIKit.Place(dock, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1020f, 132f));
            var dockBg = dock.gameObject.AddComponent<Image>();
            dockBg.sprite = SpriteFactory.RoundedRect(40);
            dockBg.type = Image.Type.Sliced;
            dockBg.color = Theme.TextPanel;
            UIKit.HRow(dock, 12f, new RectOffset(14, 14, 14, 14));
            storyButton = UIKit.Button(dock, "Story", Locale.T("dock.story"), ButtonStyle.Chip, PlayStory, out storyLabel, 28f);
            SetToggleLook(storyLabel, true); // selalu emas: pintu masuk mode Kisah
            UIKit.Button(dock, "Explode", Locale.T("dock.explode"), ButtonStyle.Primary, ToggleExplode, out explodeLabel);
            drawButton = UIKit.Button(dock, "Draw", Locale.T("dock.draw"), ButtonStyle.Chip, ToggleDraw, out drawLabel, 28f);
            UIKit.Button(dock, "AutoRotate", Locale.T("dock.autorotate"), ButtonStyle.Chip, ToggleAutoRotate, out rotateLabel, 28f);
            UIKit.Button(dock, "Labels", Locale.T("dock.labels"), ButtonStyle.Chip, ToggleLabels, out labelsLabel, 28f);

            stagePill = UIKit.Panel(safeRoot, "StagePill", Theme.TextPanel, true, false, 30);
            UIKit.Place(stagePill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 186f), new Vector2(760f, 64f));
            stageText = UIKit.Text(stagePill.transform, "Text", "", 28f, Theme.Parchment, TextAlignmentOptions.Center);
            UIKit.Stretch(stageText.rectTransform, 20, 20, 0, 0);

            story = StoryPanel.Create(safeRoot, overlay);
            story.ActiveChanged += OnStoryActiveChanged;

            Locale.Changed += RefreshLabels;
        }

        void OnDestroy() => Locale.Changed -= RefreshLabels;

        public void Bind(ArtifactInstance instance, Camera cam, Action reset, Action move)
        {
            if (artifact != null && artifact.exploded != null) artifact.exploded.StageChanged -= OnStageChanged;
            if (artifact != null && artifact.autoRotate != null) artifact.autoRotate.ActiveChanged -= OnAutoRotateChanged;
            artifact = instance;
            onReset = reset;
            onMove = move;
            story.Bind(instance);
            overlay.Bind(instance, cam);
            if (artifact != null && artifact.exploded != null) artifact.exploded.StageChanged += OnStageChanged;
            if (artifact != null && artifact.autoRotate != null) artifact.autoRotate.ActiveChanged += OnAutoRotateChanged;
            RefreshLabels();
        }

        public void SetControlsVisible(bool visible)
        {
            controlsVisible = visible;
            UIKit.SetVisible(controls, visible);
            UIKit.SetVisible(dock, visible && !story.IsActive);
            UIKit.SetVisible(overlay, visible);
            if (!visible) overlay.Card.Close();
            // Kisah dijeda (bukan dihentikan) selama kontrol tersembunyi, mis. kode QR sesaat hilang dari kamera.
            story.SetSuspended(!visible);
        }

        /// <summary>Menutup kartu info atau menghentikan mode Kisah (tombol Kembali). True bila ada yang ditutup.</summary>
        public bool CloseInfo()
        {
            if (overlay.Card.IsOpen)
            {
                overlay.Card.Close();
                return true;
            }
            if (!story.IsActive) return false;
            story.Stop();
            return true;
        }

        /// <summary>Memulai mode Kisah dari bab pertama (dipakai juga oleh build QA).</summary>
        public void PlayStory()
        {
            if (!controlsVisible || !story.HasStory) return;
            story.Play();
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
            if (!controlsVisible || artifact == null || story.IsActive) return;
            var h = overlay.PickAt(screen);
            if (h != null) Open(h);
            else CloseInfo();
        }

        void Update()
        {
            string text = null;
            if (controlsVisible && !story.IsActive && artifact != null && artifact.exploded != null
                && (artifact.exploded.CurrentStage > 0 || artifact.exploded.IsAnimating))
                text = artifact.exploded.CurrentStageLabel;
            bool show = !string.IsNullOrEmpty(text);
            UIKit.SetVisible(stagePill, show);
            if (show) stageText.text = text;
        }

        void RefreshLabels()
        {
            var ex = artifact != null ? artifact.exploded : null;
            UIKit.SetVisible(storyButton, story.HasStory);
            storyLabel.text = Locale.T("dock.story");
            explodeLabel.text = Locale.T(ex != null && ex.IsExplodedOrExploding ? "dock.assemble" : "dock.explode");
            UIKit.SetVisible(drawButton, ex != null && ex.CanDraw);
            bool drawn = ex != null && ex.TargetStage == 1;
            drawLabel.text = Locale.T(drawn ? "dock.sheathe" : "dock.draw");
            SetToggleLook(drawLabel, drawn);
            bool rotating = artifact != null && artifact.autoRotate != null && artifact.autoRotate.Active;
            rotateLabel.text = Locale.T("dock.autorotate");
            SetToggleLook(rotateLabel, rotating);
            labelsLabel.text = Locale.T("dock.labels");
            SetToggleLook(labelsLabel, overlay.LabelsVisible);
            foreach (var l in new[] { storyLabel, explodeLabel, drawLabel, rotateLabel, labelsLabel }) FitToLabel(l);
        }

        /// <summary>Tombol dock yang sedang aktif: teks emas tebal.</summary>
        static void SetToggleLook(TextMeshProUGUI label, bool on)
        {
            label.color = on ? Theme.Gold : Theme.Parchment;
            label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
        }

        /// <summary>Lebar tombol dock mengikuti labelnya (lima tombol sama lebar memotong "Putar Otomatis").</summary>
        static void FitToLabel(TextMeshProUGUI label)
        {
            float w = label.GetPreferredValues(label.text, 1000f, 0f).x + 40f;
            UIKit.Layout(label.transform.parent.GetComponent<Button>(), -1, w);
        }

        void OnStoryActiveChanged(bool active)
        {
            UIKit.SetVisible(dock, controlsVisible && !active);
            RefreshLabels();
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
            story.Stop(); // mengetuk label saat Kisah berjalan = beralih ke kartu info bagian itu
            if (artifact.autoRotate != null) artifact.autoRotate.Active = false;
            var list = overlay.ShownHotspots();
            overlay.Card.Show(artifact.Data, h, list.IndexOf(h), list.Count);
        }
    }
}
