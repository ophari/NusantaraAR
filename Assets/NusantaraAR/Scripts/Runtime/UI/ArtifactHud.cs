using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Kontrol artefak yang sama di 3D Viewer dan AR (PRD Layar 2 & 4):
    /// floating controls (Reset Tampilan, Pindahkan), dock (Bongkar/Gabung, Putar Otomatis, Tampilkan/Sembunyikan Label),
    /// label tahap exploded view, serta label bagian + kartu info yang menempel langsung di objek.
    /// </summary>
    public class ArtifactHud : MonoBehaviour
    {
        RectTransform controls, dock;
        TextMeshProUGUI explodeLabel, drawLabel, rotateLabel, labelsLabel, stageText;
        Button drawButton;
        Image stagePill;
        HotspotOverlay overlay;

        ArtifactInstance artifact;
        Action onReset;
        Action onMove;
        bool controlsVisible = true;

        public HotspotOverlay Overlay => overlay;

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
            UIKit.Button(dock, "Explode", Locale.T("dock.explode"), ButtonStyle.Primary, ToggleExplode, out explodeLabel);
            drawButton = UIKit.Button(dock, "Draw", Locale.T("dock.draw"), ButtonStyle.Chip, ToggleDraw, out drawLabel, 28f);
            UIKit.Button(dock, "AutoRotate", Locale.T("dock.autorotate"), ButtonStyle.Chip, ToggleAutoRotate, out rotateLabel, 28f);
            UIKit.Button(dock, "Labels", Locale.T("dock.labels"), ButtonStyle.Chip, ToggleLabels, out labelsLabel, 28f);

            stagePill = UIKit.Panel(safeRoot, "StagePill", Theme.TextPanel, true, false, 30);
            UIKit.Place(stagePill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 186f), new Vector2(760f, 64f));
            stageText = UIKit.Text(stagePill.transform, "Text", "", 28f, Theme.Parchment, TextAlignmentOptions.Center);
            UIKit.Stretch(stageText.rectTransform, 20, 20, 0, 0);

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
            overlay.Bind(instance, cam);
            if (artifact != null && artifact.exploded != null) artifact.exploded.StageChanged += OnStageChanged;
            if (artifact != null && artifact.autoRotate != null) artifact.autoRotate.ActiveChanged += OnAutoRotateChanged;
            RefreshLabels();
        }

        public void SetControlsVisible(bool visible)
        {
            controlsVisible = visible;
            UIKit.SetVisible(controls, visible);
            UIKit.SetVisible(dock, visible);
            UIKit.SetVisible(overlay, visible);
            if (!visible) CloseInfo();
        }

        /// <summary>Menutup kartu info bila terbuka (tombol Kembali). True bila ada yang ditutup.</summary>
        public bool CloseInfo()
        {
            if (!overlay.Card.IsOpen) return false;
            overlay.Card.Close();
            return true;
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
            if (!controlsVisible || artifact == null) return;
            var h = overlay.PickAt(screen);
            if (h != null) Open(h);
            else CloseInfo();
        }

        void Update()
        {
            string text = null;
            if (controlsVisible && artifact != null && artifact.exploded != null
                && (artifact.exploded.CurrentStage > 0 || artifact.exploded.IsAnimating))
                text = artifact.exploded.CurrentStageLabel;
            bool show = !string.IsNullOrEmpty(text);
            UIKit.SetVisible(stagePill, show);
            if (show) stageText.text = text;
        }

        void RefreshLabels()
        {
            var ex = artifact != null ? artifact.exploded : null;
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
        }

        /// <summary>Tombol dock yang sedang aktif: teks emas tebal.</summary>
        static void SetToggleLook(TextMeshProUGUI label, bool on)
        {
            label.color = on ? Theme.Gold : Theme.Parchment;
            label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
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
            if (artifact.autoRotate != null) artifact.autoRotate.Active = false;
            var list = overlay.ShownHotspots();
            overlay.Card.Show(artifact.Data, h, list.IndexOf(h), list.Count);
        }
    }
}
