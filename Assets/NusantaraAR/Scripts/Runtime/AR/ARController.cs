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
    /// State machine mode AR (PRD §4.1-4.2): cek ketersediaan ARCore -> izin kamera -> scanning (coaching)
    /// -> siap letak -> terpasang (anchored) <-> pindahkan, plus tracking hilang. Semua jalur gagal
    /// mengarah ke 3D Viewer.
    /// </summary>
    public class ARController : MonoBehaviour
    {
        public enum State
        {
            CheckingAvailability, Unsupported, Installing, InstallFailed, NeedsPermission, PermissionDenied,
            Scanning, ReadyToPlace, Placed, Repositioning, TrackingLost
        }

        const float TipsAfterSeconds = 15f;
        const float DegreesPerDp = 0.4f;

        public ARSession session;
        public PlacementController placement;
        public TouchGestures gestures;
        public Camera arCamera;

        State state;
        ArtifactData data;
        float scanStart;
        float sessionStart;

        ArtifactHud hud;
        TopBar topBar;
        CoachCard coach;
        RectTransform tips;
        MessageDialog message;
        GroundGlow glow;
        float glowRadius;
        bool refreshed;

        public State CurrentState => state;

        IEnumerator Start()
        {
            Application.targetFrameRate = 30;
            data = AppSession.SelectedArtifact;
            AudioManager.Instance.PlayMusic(data != null ? data.backgroundMusic : null); // sama dengan di detail: tidak mulai ulang
            BuildUI();
            hud.SetControlsVisible(false);
            session.enabled = false;
            session.matchFrameRateRequested = true;

            gestures.Tapped += OnTap;
            gestures.Dragged += OnDrag;
            gestures.Pinched += OnPinch;
            gestures.TwoFingerPanned += OnPan;
            gestures.TwoFingerPanEnded += OnPanEnded;
            gestures.InteractionStarted += StopAutoRotate;

            SetState(State.CheckingAvailability);
            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                yield return ARSession.CheckAvailability();

            if (ARSession.state == ARSessionState.Unsupported)
            {
                SetState(State.Unsupported);
                yield break;
            }

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                SetState(State.Installing);
                yield return ARSession.Install();
                if (ARSession.state == ARSessionState.NeedsInstall || ARSession.state == ARSessionState.Unsupported)
                {
                    SetState(State.InstallFailed);
                    yield break;
                }
            }

            if (!CameraPermission.IsGranted)
            {
                SetState(State.NeedsPermission);
                yield break; // dilanjutkan dari tombol "Izinkan Kamera"
            }
            StartAR();
        }

        void OnDestroy()
        {
            Locale.Changed -= RefreshTexts;
            if (gestures == null) return;
            gestures.Tapped -= OnTap;
            gestures.Dragged -= OnDrag;
            gestures.Pinched -= OnPinch;
            gestures.TwoFingerPanned -= OnPan;
            gestures.TwoFingerPanEnded -= OnPanEnded;
            gestures.InteractionStarted -= StopAutoRotate;
        }

        void StartAR()
        {
            session.enabled = true;
            placement.SetPlanesVisible(true);
            sessionStart = scanStart = Time.time;
            SetState(State.Scanning);
            Analytics.Log("ar_session_start", ("artifact", data != null ? data.artifactId : ""));
        }

        void RequestPermission()
        {
            CameraPermission.Request(granted =>
            {
                if (granted) StartAR();
                else SetState(State.PermissionDenied);
            });
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            switch (state)
            {
                case State.Scanning:
                case State.ReadyToPlace:
                case State.Repositioning:
                {
                    bool has = placement.UpdateTarget(true);
                    if (state != State.Repositioning) SetState(has ? State.ReadyToPlace : State.Scanning);
                    bool showTips = !has && Time.time - scanStart > TipsAfterSeconds;
                    UIKit.SetVisible(tips, showTips);
                    break;
                }
                case State.Placed:
                    if (ARSession.state != ARSessionState.SessionTracking) SetState(State.TrackingLost);
                    break;
                case State.TrackingLost:
                    if (ARSession.state == ARSessionState.SessionTracking) SetState(State.Placed);
                    break;
            }

            UpdateGlow();

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (!hud.CloseInfo()) Back();
            }
        }

        /// <summary>Cahaya terakota di bawah artefak yang terpasang, mengikuti posisi & skalanya.</summary>
        void UpdateGlow()
        {
            if (glow == null) return;
            var p = placement.Placed;
            bool show = p != null && p.gameObject.activeInHierarchy;
            glow.SetVisible(show);
            if (show) glow.Place(p.transform.position, p.transform.up, glowRadius * p.RelativeScale);
        }

        // ------------------------------------------------------------------ gestur

        void OnTap(Vector2 screen)
        {
            if (state == State.Placed)
            {
                hud.HandleTap(screen);
                return;
            }
            bool canPlace = state == State.ReadyToPlace || (state == State.Repositioning && placement.HasTargetPose);
            if (!canPlace) return;
            bool first = placement.Placed == null;
            var instance = placement.Place(data);
            if (instance == null) return;
            if (first)
            {
                var b = instance.GetWorldBounds();
                glowRadius = Mathf.Max(b.extents.x, b.extents.z) / Mathf.Max(0.01f, instance.RelativeScale) * 1.5f + 0.03f;
                hud.Bind(instance, arCamera, ResetView, Nudge);
                Analytics.Log("ar_placed", ("artifact", data.artifactId), ("seconds", Mathf.RoundToInt(Time.time - sessionStart)));
            }
            placement.SetPlanesVisible(false);
            UIKit.SetVisible(tips, false);
            hud.SetControlsVisible(true);
            SetState(State.Placed);
        }

        /// <summary>Tombol putar HUD (+ = muka artefak bergeser ke kiri; AR Meja tanpa miring).</summary>
        void Nudge(float yawDegrees, float tiltDegrees)
        {
            if (!IsManipulable) return;
            placement.Placed.RotateYaw(yawDegrees);
            if (glow != null) glow.Pulse();
        }

        void OnDrag(Vector2 delta)
        {
            if (!IsManipulable) return;
            placement.Placed.RotateYaw(-delta.x * DegreesPerDp / Mathf.Max(0.01f, TouchGestures.DpToPixels(1f)));
        }

        void OnPinch(float ratio)
        {
            if (IsManipulable) placement.Placed.MultiplyScale(ratio);
        }

        void OnPan(Vector2 center)
        {
            if (IsManipulable) placement.DragTo(center);
        }

        void OnPanEnded() => placement.EndDrag();

        bool IsManipulable => (state == State.Placed || state == State.TrackingLost) && placement.Placed != null;

        void StopAutoRotate()
        {
            var p = placement != null ? placement.Placed : null;
            if (p != null && p.autoRotate != null) p.autoRotate.Active = false;
        }

        void ResetView()
        {
            if (placement.Placed != null) placement.Placed.ResetView();
        }

        void BeginMove()
        {
            placement.BeginReposition();
            placement.SetPlanesVisible(true);
            hud.SetControlsVisible(false);
            scanStart = Time.time;
            SetState(State.Repositioning);
        }

        void Back() => AppSession.OpenViewer();

        // ------------------------------------------------------------------ UI

        void BuildUI()
        {
            var canvas = UIKit.CreateCanvas("UI");
            var full = UIKit.Stretch(UIKit.Rect("Full", canvas.transform));
            var safe = UIKit.Stretch(UIKit.Rect("Safe", full));
            safe.gameObject.AddComponent<SafeArea>();

            const float top = 20f + TopBar.Height + 16f;
            hud = ArtifactHud.Create(safe, full, new HudOptions
            {
                topInset = top,
                tiltButtons = false, // di meja keris selalu tegak
                panel = new ControlPanelConfig
                {
                    getScale = () => placement.Placed != null ? placement.Placed.RelativeScale : 1f,
                    setScale = s =>
                    {
                        if (placement.Placed != null) placement.Placed.SetRelativeScale(s);
                    },
                    toggleKey = "ctrl.planes",
                    getToggle = () => placement.PlanesVisible,
                    setToggle = placement.SetPlanesVisible,
                    buttonKey = "ctrl.move",
                    buttonIcon = Icon.Move,
                    onButton = BeginMove
                }
            });
            topBar = TopBar.Create(safe, Back, true);
            coach = CoachCard.Create(safe, top);

            // Tips setelah 15 detik tanpa bidang (PRD Layar 3)
            var tipsBg = UIKit.Surface(safe, "Tips", SurfaceStyle.Glass, 40);
            tipsBg.GetComponent<Surface>().fill.GetComponent<GlassSurface>().Strength = 0.85f;
            tips = tipsBg.rectTransform;
            UIKit.Place(tips, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1000f, 580f));
            var tt = UIKit.Text(tips, "Title", Locale.T("ar.tipsTitle"), Theme.Heading, Theme.Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            LocalizedLabel.Attach(tt, "ar.tipsTitle");
            UIKit.Stretch(tt.rectTransform, 48, 48, 40, 470);
            var tb = UIKit.Text(tips, "Body", Locale.T("ar.tipsBody"), Theme.Small, Theme.Ink);
            tb.lineSpacing = 10f;
            LocalizedLabel.Attach(tb, "ar.tipsBody");
            UIKit.Stretch(tb.rectTransform, 48, 48, 116, 160);
            var ov = UIKit.Button(tips, "OpenViewer", Locale.T("ar.openViewer"), ButtonStyle.Primary, Back, out var ol);
            LocalizedLabel.Attach(ol, "ar.openViewer");
            UIKit.Place((RectTransform)ov.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(600f, 104f));
            tips.gameObject.SetActive(false);

            // Panel pesan (tidak didukung / instalasi / izin)
            message = MessageDialog.Create(full, "ar.openViewer", Back);
            glow = GroundGlow.Create();
            if (glow != null) glow.SetVisible(false);

            Locale.Changed += RefreshTexts;
        }

        void SetState(State s)
        {
            if (state == s && refreshed) return;
            state = s;
            RefreshTexts();
        }

        void RefreshTexts()
        {
            if (coach == null) return;
            refreshed = true;
            topBar.SetTitle(data != null ? data.displayName.Get() : "");
            bool showMessage = false;
            string title = null, body = null, primary = null;
            UnityEngine.Events.UnityAction primaryAction = null;
            bool showPrimary = true;

            switch (state)
            {
                case State.CheckingAvailability: coach.Show(Icon.Compass, Locale.T("ar.checking")); break;
                case State.Installing: coach.Show(Icon.Compass, Locale.T("ar.installing")); break;
                case State.Scanning: coach.Show(Icon.Compass, Locale.T("ar.detecting"), Locale.T("ar.scanning"), true); break;
                case State.ReadyToPlace: coach.Show(Icon.Target, Locale.T("ar.ready")); break;
                case State.Repositioning: coach.Show(Icon.Move, Locale.T("ar.moving")); break;
                case State.Placed: coach.Show(Icon.Move, Locale.T("ar.placed"), null, false, 5f); break;
                case State.TrackingLost: coach.Show(Icon.Alert, Locale.T("ar.trackingLost")); break;
                case State.Unsupported:
                    showMessage = true;
                    title = Locale.T("ar.unsupportedTitle");
                    body = Locale.T("ar.unsupportedBody");
                    showPrimary = false;
                    break;
                case State.InstallFailed:
                    showMessage = true;
                    title = Locale.T("ar.unsupportedTitle");
                    body = Locale.T("ar.installFailed");
                    showPrimary = false;
                    break;
                case State.NeedsPermission:
                    showMessage = true;
                    title = Locale.T("ar.permTitle");
                    body = Locale.T("ar.permBody");
                    primary = Locale.T("ar.permAllow");
                    primaryAction = RequestPermission;
                    break;
                case State.PermissionDenied:
                    showMessage = true;
                    title = Locale.T("ar.permTitle");
                    body = Locale.T("ar.permDeniedBody");
                    primary = Locale.T("ar.openSettings");
                    primaryAction = CameraPermission.OpenAppSettings;
                    break;
            }

            if (!showMessage)
            {
                message.Hide();
                return;
            }
            coach.Hide();
            message.Show(title, body, primary, showPrimary ? primaryAction : null);
        }

        void OnApplicationFocus(bool focus)
        {
            // Kembali dari Pengaturan aplikasi atau dialog setelah izin diberikan.
            if (focus && (state == State.PermissionDenied || state == State.NeedsPermission) && CameraPermission.IsGranted) StartAR();
        }
    }
}
