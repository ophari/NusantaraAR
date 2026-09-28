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
        Image statusPill;
        TextMeshProUGUI statusText;
        RectTransform tips;
        RectTransform message;
        TextMeshProUGUI messageTitle, messageBody, primaryLabel;
        Button primaryButton, secondaryButton;

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

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (!hud.CloseInfo()) Back();
            }
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
                hud.Bind(instance, arCamera, ResetView, BeginMove);
                Analytics.Log("ar_placed", ("artifact", data.artifactId), ("seconds", Mathf.RoundToInt(Time.time - sessionStart)));
            }
            placement.SetPlanesVisible(false);
            UIKit.SetVisible(tips, false);
            hud.SetControlsVisible(true);
            SetState(State.Placed);
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

            hud = ArtifactHud.Create(safe, full, 160f, true);

            var back = UIKit.Button(safe, "Back", Locale.T("common.back"), ButtonStyle.Secondary, Back, out var bl, 30f);
            LocalizedLabel.Attach(bl, "common.back");
            UIKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(220f, 96f));

            statusPill = UIKit.Panel(safe, "StatusPill", Theme.TextPanel, true, false, 44);
            UIKit.Place(statusPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(110f, -34f), new Vector2(760f, 84f));
            statusText = UIKit.Text(statusPill.transform, "Text", "", 28f, Theme.Parchment, TextAlignmentOptions.Center);
            UIKit.Stretch(statusText.rectTransform, 24, 24, 4, 4);

            // Tips setelah 15 detik tanpa bidang (PRD Layar 3)
            var tipsBg = UIKit.Panel(safe, "Tips", Theme.TextPanel, true, true, 36);
            tips = tipsBg.rectTransform;
            UIKit.Place(tips, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(980f, 560f));
            var tt = UIKit.Text(tips, "Title", Locale.T("ar.tipsTitle"), Theme.Heading, Theme.Gold, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            LocalizedLabel.Attach(tt, "ar.tipsTitle");
            UIKit.Stretch(tt.rectTransform, 44, 44, 36, 460);
            var tb = UIKit.Text(tips, "Body", Locale.T("ar.tipsBody"), Theme.Small, Theme.Parchment);
            tb.lineSpacing = 10f;
            LocalizedLabel.Attach(tb, "ar.tipsBody");
            UIKit.Stretch(tb.rectTransform, 44, 44, 110, 150);
            var ov = UIKit.Button(tips, "OpenViewer", Locale.T("ar.openViewer"), ButtonStyle.Primary, Back, out var ol);
            LocalizedLabel.Attach(ol, "ar.openViewer");
            UIKit.Place((RectTransform)ov.transform, new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(560f, 100f));
            tips.gameObject.SetActive(false);

            // Panel pesan (tidak didukung / instalasi / izin)
            var scrim = UIKit.Panel(full, "Message", Theme.Scrim, false, true);
            message = UIKit.Stretch(scrim.rectTransform);
            var card = UIKit.Panel(message, "Card", Theme.TextPanel, true, true, 40);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 760f));
            messageTitle = UIKit.Text(card.transform, "Title", "", Theme.Heading, Theme.Gold, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIKit.Stretch(messageTitle.rectTransform, 52, 52, 52, 620);
            messageBody = UIKit.Text(card.transform, "Body", "", Theme.Body, Theme.Parchment);
            messageBody.lineSpacing = 8f;
            UIKit.Stretch(messageBody.rectTransform, 52, 52, 140, 290);
            primaryButton = UIKit.Button(card.transform, "Primary", "", ButtonStyle.Primary, null, out primaryLabel);
            UIKit.Place((RectTransform)primaryButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 156f), new Vector2(620f, 104f));
            secondaryButton = UIKit.Button(card.transform, "Secondary", Locale.T("ar.openViewer"), ButtonStyle.Chip, Back, out var sl);
            LocalizedLabel.Attach(sl, "ar.openViewer");
            UIKit.Place((RectTransform)secondaryButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(620f, 100f));
            message.gameObject.SetActive(false);

            Locale.Changed += RefreshTexts;
        }

        void SetState(State s)
        {
            if (state == s && statusText != null && !string.IsNullOrEmpty(statusText.text)) return;
            state = s;
            RefreshTexts();
        }

        void RefreshTexts()
        {
            string status = null;
            bool showMessage = false;
            string title = null, body = null, primary = null;
            UnityEngine.Events.UnityAction primaryAction = null;
            bool showPrimary = true, showSecondary = true;

            switch (state)
            {
                case State.CheckingAvailability: status = Locale.T("ar.checking"); break;
                case State.Installing: status = Locale.T("ar.installing"); break;
                case State.Scanning: status = Locale.T("ar.scanning"); break;
                case State.ReadyToPlace: status = Locale.T("ar.ready"); break;
                case State.Repositioning: status = Locale.T("ar.moving"); break;
                case State.Placed: status = Locale.T("ar.placed"); break;
                case State.TrackingLost: status = Locale.T("ar.trackingLost"); break;
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

            UIKit.SetVisible(statusPill, status != null);
            if (status != null) statusText.text = status;

            UIKit.SetVisible(message, showMessage);
            if (!showMessage) return;
            messageTitle.text = title;
            messageBody.text = body;
            UIKit.SetVisible(primaryButton, showPrimary);
            UIKit.SetVisible(secondaryButton, showSecondary);
            primaryButton.onClick.RemoveAllListeners();
            primaryButton.onClick.AddListener(() => AudioManager.Instance.Click());
            if (primaryAction != null) primaryButton.onClick.AddListener(primaryAction);
            primaryLabel.text = primary ?? string.Empty;
        }

        void OnApplicationFocus(bool focus)
        {
            // Kembali dari Pengaturan aplikasi atau dialog setelah izin diberikan.
            if (focus && (state == State.PermissionDenied || state == State.NeedsPermission) && CameraPermission.IsGranted) StartAR();
        }
    }
}
