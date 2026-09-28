using System.Collections;
using NusantaraAR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace NusantaraAR
{
    /// <summary>
    /// Scene Main: katalog (Layar 1) dan halaman detail dengan 3D Viewer non-AR (Layar 2, PRD FR-11/FR-12).
    /// </summary>
    public class MainController : MonoBehaviour
    {
        public Camera viewerCamera;
        public OrbitCameraController orbit;
        public TouchGestures gestures;
        public Transform stageRoot;

        CatalogScreen catalog;
        MarkerCardScreen markerCard;
        Button tableButton, cardButton;
        RectTransform secondaryRow;
        SettingsScreen settings;
        OnboardingScreen onboarding;
        ArtifactHud hud;
        RectTransform detailTop;
        TextMeshProUGUI detailTitle, arLabel;
        Button arButton;

        ArtifactInstance current;
        bool arSupported = true;
        bool drawFraming;
        Vector3 preDrawTarget;
        float preDrawDistance;

        void Start()
        {
            Application.targetFrameRate = 60;
            BuildUI();

            var selected = AppSession.OpenDetailOnLoad ? AppSession.SelectedArtifact : null;
            AppSession.OpenDetailOnLoad = false;
            if (selected != null) OpenDetail(selected);
            else ShowCatalog();

            if (!AppSettings.OnboardingDone) onboarding.Show();
            if (gestures != null)
            {
                gestures.InteractionStarted += StopAutoRotate;
                gestures.Tapped += hud.HandleTap;
            }
            Locale.Changed += RefreshDetailTexts;
            StartCoroutine(CheckARSupport());
        }

        void OnDestroy()
        {
            Locale.Changed -= RefreshDetailTexts;
            if (gestures == null) return;
            gestures.InteractionStarted -= StopAutoRotate;
            gestures.Tapped -= hud.HandleTap;
        }

        void BuildUI()
        {
            var canvas = UIKit.CreateCanvas("UI");
            var full = UIKit.Stretch(UIKit.Rect("Full", canvas.transform));
            var safe = UIKit.Stretch(UIKit.Rect("Safe", full));
            safe.gameObject.AddComponent<SafeArea>();

            hud = ArtifactHud.Create(safe, full, 170f, false);

            detailTop = UIKit.Rect("DetailTop", safe);
            UIKit.Place(detailTop, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1032f, 120f));
            var back = UIKit.Button(detailTop, "Back", Locale.T("common.back"), ButtonStyle.Secondary, ShowCatalog, out var bl, 30f);
            LocalizedLabel.Attach(bl, "common.back");
            UIKit.Place((RectTransform)back.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(220f, 96f));
            detailTitle = UIKit.Text(detailTop, "Title", "", Theme.Heading, Theme.Parchment, TextAlignmentOptions.Right, FontStyles.Bold);
            detailTitle.overflowMode = TextOverflowModes.Ellipsis;
            detailTitle.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Stretch(detailTitle.rectTransform, 250, 0, 0, 0);

            // Utama: Scan QR (jalan di semua HP berkamera). Sekunder: Letakkan di Meja (hanya HP ber-ARCore) & Tampilkan QR.
            arButton = UIKit.Button(safe, "ScanCard", Locale.T("marker.scan"), ButtonStyle.Primary, OpenMarker, out arLabel, 38f);
            LocalizedLabel.Attach(arLabel, "marker.scan");
            UIKit.Place((RectTransform)arButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 272f), new Vector2(640f, 116f));
            secondaryRow = UIKit.Rect("SecondaryAR", safe);
            UIKit.Place(secondaryRow, new Vector2(0.5f, 0f), new Vector2(0f, 404f), new Vector2(880f, 90f));
            UIKit.HRow(secondaryRow, 20f);
            tableButton = UIKit.Button(secondaryRow, "PlaceOnTable", Locale.T("marker.placeOnTable"), ButtonStyle.Chip, OpenAR, out var tl, 30f);
            LocalizedLabel.Attach(tl, "marker.placeOnTable");
            cardButton = UIKit.Button(secondaryRow, "ShowCard", Locale.T("marker.showCard"), ButtonStyle.Chip, ShowCard, out var cl, 30f);
            LocalizedLabel.Attach(cl, "marker.showCard");

            catalog = CatalogScreen.Create(full, AppSession.Catalog, OpenDetail, () => settings.Show(), () => AppSession.OpenMarker(null));
            markerCard = MarkerCardScreen.Create(full);
            settings = SettingsScreen.Create(full, () => onboarding.Show());
            onboarding = OnboardingScreen.Create(full, null);
        }

        void OpenDetail(ArtifactData data)
        {
            if (data == null || data.prefab == null) return;
            ClearArtifact();
            AppSession.SelectedArtifactId = data.artifactId;
            var go = Instantiate(data.prefab, stageRoot);
            go.name = data.prefab.name;
            current = go.GetComponent<ArtifactInstance>();
            current.Init(data);
            orbit.Frame(current.GetWorldBounds());
            hud.Bind(current, viewerCamera, ResetView, null);
            AudioManager.Instance.PlayMusic(data.backgroundMusic);
            catalog.SetVisible(false);
            SetDetailVisible(true);
            RefreshDetailTexts();
            Analytics.Log("detail_open", ("artifact", data.artifactId));
        }

        void ShowCatalog()
        {
            ClearArtifact();
            AudioManager.Instance.StopMusic();
            SetDetailVisible(false);
            catalog.SetVisible(true);
        }

        void ClearArtifact()
        {
            hud.CloseInfo();
            drawFraming = false;
            if (current != null) Destroy(current.gameObject);
            current = null;
        }

        void SetDetailVisible(bool visible)
        {
            UIKit.SetVisible(detailTop, visible);
            UIKit.SetVisible(arButton, visible);
            UIKit.SetVisible(secondaryRow, visible);
            hud.SetControlsVisible(visible);
        }

        void RefreshDetailTexts()
        {
            if (current != null && current.Data != null) detailTitle.text = current.Data.displayName.Get();
            // "Letakkan di Meja" butuh ARCore; di HP tanpa ARCore (mis. Galaxy A05) cukup Scan QR.
            UIKit.SetVisible(tableButton, arSupported);
            // Setiap artefak punya kode QR (diturunkan dari artifactId).
            bool hasQr = current != null && current.Data != null && !string.IsNullOrEmpty(current.Data.artifactId);
            UIKit.SetVisible(cardButton, hasQr);
            arButton.interactable = hasQr;
        }

        void ResetView()
        {
            if (current != null) current.ResetView();
            orbit.ResetView();
        }

        void StopAutoRotate()
        {
            if (current != null && current.autoRotate != null) current.autoRotate.Active = false;
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
