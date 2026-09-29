using System.Collections;
using NusantaraAR.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;

namespace NusantaraAR
{
    /// <summary>
    /// Scene Main: katalog (Layar 1) dan halaman detail dengan 3D Viewer non-AR (Layar 2, PRD FR-11/FR-12),
    /// dengan bar navigasi bawah (Koleksi · Scan QR · Pengaturan).
    /// </summary>
    public class MainController : MonoBehaviour
    {
        const float TopBarBottom = 20f + TopBar.Height;

        public Camera viewerCamera;
        public OrbitCameraController orbit;
        public TouchGestures gestures;
        public Transform stageRoot;

        RectTransform full;
        CatalogScreen catalog;
        MarkerCardScreen markerCard;
        SettingsScreen settings;
        OnboardingScreen onboarding;
        ArtifactHud hud;
        TopBar topBar;
        DetailSheet sheet;
        BottomNav nav;
        Backdrop backdrop;
        GroundGlow glow;

        ArtifactInstance current;
        bool arSupported = true;
        bool drawFraming;
        Vector3 preDrawTarget;
        float preDrawDistance;

        void Start()
        {
            Application.targetFrameRate = 60;
            viewerCamera.backgroundColor = Theme.Stage;
            BuildUI();

            var selected = AppSession.OpenDetailOnLoad ? AppSession.SelectedArtifact : null;
            AppSession.OpenDetailOnLoad = false;
            if (selected != null) OpenDetail(selected);
            else ShowCatalog();

            if (!AppSettings.OnboardingDone) onboarding.Show();
            if (gestures != null)
            {
                gestures.InteractionStarted += OnInteractionStarted;
                gestures.Tapped += hud.HandleTap;
            }
            Locale.Changed += RefreshDetailTexts;
            StartCoroutine(CheckARSupport());
        }

        void OnDestroy()
        {
            Locale.Changed -= RefreshDetailTexts;
            if (gestures == null) return;
            gestures.InteractionStarted -= OnInteractionStarted;
            gestures.Tapped -= hud.HandleTap;
        }

        void BuildUI()
        {
            var canvas = UIKit.CreateCanvas("UI");
            full = UIKit.Stretch(UIKit.Rect("Full", canvas.transform));
            var safe = UIKit.Stretch(UIKit.Rect("Safe", full));
            safe.gameObject.AddComponent<SafeArea>();

            hud = ArtifactHud.Create(safe, full, new HudOptions
            {
                topInset = TopBarBottom + 16f,
                bottomInset = BottomNav.Height,
                storyBottom = BottomNav.Height + BottomNav.Protrusion + 12f,
                tiltButtons = true
            });
            hud.Story.ActiveChanged += _ => UpdateDetailLayout();
            topBar = TopBar.Create(safe, ShowCatalog, false);
            sheet = DetailSheet.Create(safe, OpenMarker, OpenAR, ShowCard);
            sheet.HeightChanged += UpdateDetailLayout;

            // Urutan: katalog, pengaturan, bar navigasi (tetap tampil di atas keduanya), kartu QR, onboarding.
            catalog = CatalogScreen.Create(full, AppSession.Catalog, OpenDetail);
            settings = SettingsScreen.Create(full, () => onboarding.Show());
            settings.VisibilityChanged += OnSettingsVisibilityChanged;
            var navLayer = UIKit.Stretch(UIKit.Rect("NavLayer", full));
            navLayer.gameObject.AddComponent<SafeArea>();
            nav = BottomNav.Create(navLayer);
            nav.Tapped += OnNavTapped;
            nav.SetActive(BottomNav.Tab.Collection);
            markerCard = MarkerCardScreen.Create(full);
            onboarding = OnboardingScreen.Create(full, null);

            backdrop = Backdrop.Create(viewerCamera, Mathf.Min(25f, viewerCamera.farClipPlane * 0.85f));
            glow = GroundGlow.Create(stageRoot);
            if (glow != null) glow.SetVisible(false);
        }

        void OpenDetail(ArtifactData data)
        {
            if (data == null || data.prefab == null) return;
            ClearArtifact();
            settings.Hide();
            AppSession.SelectedArtifactId = data.artifactId;
            var go = Instantiate(data.prefab, stageRoot);
            go.name = data.prefab.name;
            current = go.GetComponent<ArtifactInstance>();
            current.Init(data);
            hud.Bind(current, viewerCamera, ResetView, Nudge);
            sheet.Bind(data);
            AudioManager.Instance.PlayMusic(data.backgroundMusic);
            catalog.SetVisible(false);
            SetDetailVisible(true);
            backdrop.SetDetail(true);
            RefreshDetailTexts();
            UpdateDetailLayout();
            var bounds = current.GetWorldBounds();
            orbit.Frame(bounds);
            PlaceGlow(bounds);
            Analytics.Log("detail_open", ("artifact", data.artifactId));
        }

        void ShowCatalog()
        {
            ClearArtifact();
            AudioManager.Instance.StopMusic();
            SetDetailVisible(false);
            settings.Hide();
            catalog.SetVisible(true);
            backdrop.SetDetail(false);
            orbit.SetCoveredScreen(0f, 0f);
            nav.SetActive(BottomNav.Tab.Collection);
        }

        void ClearArtifact()
        {
            hud.CloseInfo();
            drawFraming = false;
            if (current != null) Destroy(current.gameObject);
            current = null;
            if (glow != null) glow.SetVisible(false);
        }

        void SetDetailVisible(bool visible)
        {
            UIKit.SetVisible(topBar, visible);
            UIKit.SetVisible(sheet, visible && !hud.Story.IsActive);
            hud.SetControlsVisible(visible);
        }

        void PlaceGlow(Bounds b)
        {
            if (glow == null) return;
            glow.SetVisible(true);
            glow.Place(new Vector3(b.center.x, b.min.y, b.center.z), Vector3.up, Mathf.Max(b.extents.x, b.extents.z) * 1.5f + 0.04f);
        }

        /// <summary>Sheet dibuka/ditutup atau Kisah dimulai: HUD dan framing kamera mengikuti ruang yang tersisa.</summary>
        void UpdateDetailLayout()
        {
            // current/settings bisa sudah dihancurkan saat scene ditutup (OnDisable sheet ikut memicu event ini).
            if (current == null || settings == null || settings.gameObject.activeSelf) return;
            bool storyActive = hud.Story.IsActive;
            UIKit.SetVisible(sheet, !storyActive);
            float sheetHeight = storyActive ? 0f : sheet.VisibleHeight;
            hud.SetBottomInset(BottomNav.Height + sheetHeight);

            // Fraksi layar yang tertutup UI (unit kanvas → fraksi tinggi layar penuh, termasuk inset notch/gesture bar).
            float h = Mathf.Max(1f, full.rect.height);
            var safeArea = Screen.safeArea;
            float ky = Screen.height > 0 ? h / Screen.height : 1f;
            float top = (Screen.height - safeArea.yMax) * ky + TopBarBottom;
            float bottom = safeArea.yMin * ky + BottomNav.Height + (storyActive ? BottomNav.Protrusion + 12f + StoryPanel.Height : sheetHeight);
            // Sisi kiri/kanan tertutup klaster tombol putar & rel (kanvas selalu 1080 unit lebar).
            float w = Mathf.Max(1f, full.rect.width);
            orbit.SetCoveredScreen(top / h, bottom / h, ArtifactHud.LeftReserve / w, ArtifactHud.RightReserve / w);
        }

        void OnNavTapped(BottomNav.Tab tab)
        {
            switch (tab)
            {
                case BottomNav.Tab.Collection:
                    if (current != null || settings.gameObject.activeSelf) ShowCatalog();
                    break;
                case BottomNav.Tab.Scan:
                    AppSession.OpenMarker(current != null ? current.Data : null);
                    break;
                case BottomNav.Tab.Settings:
                    if (settings.gameObject.activeSelf) settings.Hide();
                    else settings.Show();
                    break;
            }
        }

        /// <summary>
        /// Pengaturan berlatar transparan (kaca di atas latar kamera): layar di bawahnya disembunyikan selama terbuka,
        /// lalu dipulihkan saat ditutup.
        /// </summary>
        void OnSettingsVisibilityChanged()
        {
            bool open = settings.gameObject.activeSelf;
            nav.SetActive(open ? BottomNav.Tab.Settings : BottomNav.Tab.Collection);
            if (open)
            {
                hud.CloseInfo();
                catalog.SetVisible(false);
                SetDetailVisible(false);
                if (current != null) current.gameObject.SetActive(false);
                if (glow != null) glow.SetVisible(false);
                backdrop.SetDetail(false);
                orbit.SetCoveredScreen(0f, 0f);
                return;
            }
            if (current == null)
            {
                catalog.SetVisible(true);
                return;
            }
            current.gameObject.SetActive(true);
            SetDetailVisible(true);
            backdrop.SetDetail(true);
            PlaceGlow(current.GetWorldBounds());
            UpdateDetailLayout();
        }

        void RefreshDetailTexts()
        {
            if (current == null || current.Data == null) return;
            topBar.SetTitle(current.Data.displayName.Get());
            // "Letakkan di Meja" butuh ARCore; di HP tanpa ARCore (mis. Galaxy A05) cukup Scan QR.
            // Setiap artefak punya kode QR (diturunkan dari artifactId).
            sheet.SetAvailability(arSupported, !string.IsNullOrEmpty(current.Data.artifactId));
        }

        void ResetView()
        {
            if (current != null) current.ResetView();
            orbit.ResetView();
        }

        /// <summary>Tombol putar/miring: + yaw = muka keris bergeser ke kiri (kamera mengorbit ke arah sebaliknya).</summary>
        void Nudge(float yawDegrees, float tiltDegrees)
        {
            orbit.Nudge(-yawDegrees, -tiltDegrees);
            if (glow != null) glow.Pulse();
        }

        void OnInteractionStarted()
        {
            if (current != null && current.autoRotate != null) current.autoRotate.Active = false;
            if (glow != null && current != null) glow.Pulse();
        }

        void OpenMarker()
        {
            if (current != null) AppSession.OpenMarker(current.Data);
        }

        void ShowCard()
        {
            if (current != null) markerCard.Show(current.Data);
        }

        void OpenAR()
        {
            if (current != null) AppSession.OpenAR(current.Data);
        }

        IEnumerator CheckARSupport()
        {
#if UNITY_EDITOR
            yield break; // Editor: tetap izinkan masuk scene AR (akan menampilkan jalur "AR tidak tersedia").
#else
            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                yield return ARSession.CheckAvailability();
            arSupported = ARSession.state != ARSessionState.Unsupported;
            RefreshDetailTexts();
#endif
        }

        /// <summary>
        /// Selama animasi hunus/sarungkan, kamera mundur halus bila perlu agar bilah yang ditarik tetap di layar,
        /// lalu kembali ke jarak semula setelah selesai.
        /// </summary>
        void FollowDrawAnimation()
        {
            var ex = current != null ? current.exploded : null;
            bool drawing = ex != null && ex.IsDrawAnimating;
            if (drawing && !drawFraming)
            {
                drawFraming = true;
                preDrawTarget = orbit.target;
                preDrawDistance = orbit.distance;
                if (ex.TryGetDrawSweepBounds(out var sweep))
                {
                    float fit = orbit.DistanceToFit(sweep);
                    if (fit > orbit.distance) orbit.EaseTo(sweep.center, fit);
                }
            }
            else if (!drawing && drawFraming)
            {
                drawFraming = false;
                orbit.EaseTo(preDrawTarget, preDrawDistance);
            }
        }

        void Update()
        {
            FollowDrawAnimation();

            // Tombol "back" Android dipetakan ke Escape oleh Input System.
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (hud.CloseInfo()) return;
            if (markerCard.gameObject.activeSelf) markerCard.Hide();
            else if (settings.gameObject.activeSelf) settings.Hide();
            else if (current != null) ShowCatalog();
        }
    }
}
