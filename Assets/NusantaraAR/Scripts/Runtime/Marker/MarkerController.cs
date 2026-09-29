using System.Collections;
using System.Collections.Generic;
using NusantaraAR.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NusantaraAR.Marker
{
    /// <summary>
    /// Mode AR "Scan QR": kamera biasa (tanpa ARCore) + deteksi kode QR; artefak yang isinya cocok
    /// (<see cref="ArtifactData.QrText"/>) muncul di atas kode QR. Kartu penanda lama (pola 6x6, dulu "Scan Kartu")
    /// tetap dikenali sebagai cadangan dari analisis frame yang sama. Berjalan di HP tanpa ARCore (mis. Galaxy A05).
    /// Artefak selalu berdiri tegak menurut sensor gravitasi, jadi QR boleh di meja, di layar/monitor, atau miring:
    /// di meja artefak berdiri di atas QR, di layar/dinding artefak menghadap keluar dari QR ke pengguna.
    /// Gestur: geser mendatar = putar, geser tegak = miringkan maju/mundur, cubit = skala.
    /// "Kunci" menahan posisi agar QR boleh dijauhkan.
    /// </summary>
    public class MarkerController : MonoBehaviour
    {
        enum State { NeedsPermission, PermissionDenied, NoCamera, CameraStalled, Starting, Searching, Tracking, Locked }

        const float LostAfterSeconds = 0.6f;
        const float KeepQrWithoutDecodeSeconds = 2f;
        const float DegreesPerDp = 0.4f;
        const float FeedStallSeconds = 3f;
        const int MaxFeedRestarts = 2;

        public Camera cam;
        public CameraFeed feed;
        public RawImage background;
        public RectTransform backgroundCanvas;
        public TouchGestures gestures;
        [Tooltip("Perkiraan sisi kode QR tercetak, tanpa zona putih (meter). Hanya memengaruhi jarak, bukan tampilan relatif.")]
        public float qrSizeMeters = 0.08f;
        [Tooltip("Perkiraan sisi kotak hitam kartu penanda lama (meter).")]
        public float markerSizeMeters = 0.08f;
        [Tooltip("Tetap kenali kartu penanda lama (pola 6x6) bila tidak ada kode QR yang dikenali di frame")]
        public bool detectLegacyCards = true;
        [Tooltip("Lebar artefak relatif terhadap sisi QR/kartu saat pertama muncul")]
        public float fitToMarker = 2.2f;

        readonly DarkRegions regions = new DarkRegions();
        readonly QrDetector qrDetector = new QrDetector();
        readonly MarkerDetector detector = new MarkerDetector();
        readonly PoseSmoother smoother = new PoseSmoother();
        readonly List<int> codes = new List<int>();
        readonly DeviceGravity gravity = new DeviceGravity();

        State state = State.Starting;
        ArtifactInstance current;
        ArtifactData currentData;
        float baseScale = 1f;
        Quaternion userRotation = Quaternion.identity;
        float userScale = 1f;
        float lastSeen = -10f;
        float lastQrRead = -10f;
        Pose markerPose;
        bool hasPose;
        float wallWeight;
        float viewerYaw;
        bool onTable;
        Vector3 boundsCenter, boundsExtents; // ruang lokal prefab, tanpa skala
        bool controlsShown;
        bool cameraStarting;
        float lastFrame;
        int feedRestarts;

        ArtifactHud hud;
        TopBar topBar;
        CoachCard coach;
        MessageDialog message;
        GroundGlow glow;
        bool uiReady, refreshed;

        void Start()
        {
            Application.targetFrameRate = 30;
            foreach (var a in AppSession.Catalog.artifacts)
                if (a != null && a.markerCode != 0 && !codes.Contains(a.markerCode)) codes.Add(a.markerCode);

            // RawImage tanpa tekstur tampil sebagai kotak putih; baru ditampilkan CameraFeed.ConfigureDisplay saat frame pertama.
            background.enabled = false;
            BuildUI();
            hud.SetControlsVisible(false);
            gestures.Dragged += OnDrag;
            gestures.Pinched += OnPinch;
            gestures.InteractionStarted += StopAutoRotate;
            gestures.Tapped += hud.HandleTap;
            Locale.Changed += RefreshTexts;
            gravity.Enable();

            if (!CameraPermission.IsGranted)
            {
                SetState(State.NeedsPermission);
                RequestPermission();
            }
            else
            {
                StartCamera();
            }
        }

        void OnDestroy()
        {
            Locale.Changed -= RefreshTexts;
            gravity.Disable();
            if (gestures != null)
            {
                gestures.Dragged -= OnDrag;
                gestures.Pinched -= OnPinch;
                gestures.InteractionStarted -= StopAutoRotate;
                gestures.Tapped -= hud.HandleTap;
            }
            if (feed != null) feed.StopFeed();
        }

        void RequestPermission()
        {
            CameraPermission.Request(granted =>
            {
                if (granted)
                {
                    StartCamera();
                }
                else
                {
                    SetState(State.PermissionDenied);
                }
            });
        }

        /// <summary>
        /// Callback izin dan OnApplicationFocus terpicu bersamaan saat dialog izin ditutup; tanpa penjaga ini kamera
        /// dibuka dua kali dan tekstur kedua tak pernah mendapat frame (layar hitam). <paramref name="restart"/> = buka ulang
        /// walau kamera sedang berjalan.
        /// </summary>
        void StartCamera(bool restart = false)
        {
            if (cameraStarting || (feed.IsRunning && !restart)) return;
            StartCoroutine(InitAndStartCamera());
        }

        IEnumerator InitAndStartCamera()
        {
            cameraStarting = true;
            background.enabled = false;
            SetState(State.Starting);

            // Beri jeda 1 frame agar Android activity & camera HAL terinisialisasi
            yield return null;

            // Tunggu hingga hardware kamera terdeteksi oleh WebCamTexture (maksimal 1.5 detik)
            int retries = 0;
            while (!CameraFeed.HasCamera && retries < 15)
            {
                retries++;
                yield return new WaitForSeconds(0.1f);
            }

            bool started = CameraFeed.HasCamera && feed.StartFeed();
            cameraStarting = false;
            if (!started)
            {
                SetState(State.NoCamera);
                yield break;
            }

            lastFrame = Time.time;
            SetState(State.Searching);
            Analytics.Log("marker_session_start");
        }

        void RetryCamera()
        {
            feedRestarts = 0;
            StartCamera(restart: true);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && (state == State.PermissionDenied || state == State.NeedsPermission || state == State.NoCamera || state == State.CameraStalled))
            {
                if (CameraPermission.IsGranted)
                {
                    RetryCamera();
                }
            }
        }

        /// <summary>Kamera terbuka tapi tidak mengirim frame (mis. masih dipegang aplikasi lain): buka ulang, lalu tampilkan pesan.</summary>
        void CheckFeedStalled()
        {
            if (cameraStarting || (state != State.Searching && state != State.Tracking && state != State.Locked)) return;
            if (Time.time - lastFrame < FeedStallSeconds) return;
            if (feedRestarts < MaxFeedRestarts)
            {
                feedRestarts++;
                StartCamera(restart: true);
            }
            else
            {
                feed.StopFeed();
                SetState(State.CameraStalled);
            }
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            gravity.Update(Time.deltaTime);
            if (feed.IsRunning && feed.Grab())
            {
                lastFrame = Time.time;
                feedRestarts = 0;
                feed.ConfigureDisplay(background, backgroundCanvas, cam);
                ProcessFrame();
            }
            else CheckFeedStalled();

            if (state == State.Searching || state == State.Tracking)
            {
                bool seen = Time.time - lastSeen < LostAfterSeconds;
                SetState(seen ? State.Tracking : State.Searching);
                if (current != null) current.gameObject.SetActive(seen);
                bool show = seen && current != null;
                if (glow != null) glow.SetVisible(show);
                if (show != controlsShown)
                {
                    controlsShown = show;
                    hud.SetControlsVisible(show);
                }
            }
            if (current != null && current.gameObject.activeSelf && hasPose)
            {
                // AutoRotate memutar transform sendiri, tapi pose ditimpa tiap frame -> putarannya disalurkan ke sini.
                if (current.autoRotate != null && current.autoRotate.Active)
                    userRotation = Quaternion.AngleAxis(current.autoRotate.degreesPerSecond * Time.deltaTime, Vector3.up) * userRotation;
                ApplyPose();
            }

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (!hud.CloseInfo()) Back();
            }
        }

        void ProcessFrame()
        {
            if (state == State.Locked) return;
            regions.Analyze(feed.Gray, feed.Width, feed.Height); // sekali per frame, dipakai detektor QR & kartu
            if (!FindTarget(out var data, out var cornersPx, out float size)) return;
            EnsureArtifact(data, size);
            if (current == null) return;
            if (!MarkerPose.TryEstimate(cornersPx, size, feed.FocalPixels, feed.Width * 0.5f, feed.Height * 0.5f, out var corners)) return;
            var up = gravity.UpInCamera;
            var target = MarkerPose.UprightPose(corners, up, out float wall, out float yawToViewer);
            bool reacquired = Time.time - lastSeen > LostAfterSeconds;
            if (reacquired)
            {
                smoother.Reset();
                wallWeight = wall;
            }
            else
            {
                wallWeight = Mathf.Lerp(wallWeight, wall, 1f - Mathf.Exp(-6f * Time.deltaTime));
            }
            // QR di meja: hadapkan muka artefak ke pengguna sekali saat mulai terlihat (atau saat QR kembali
            // direbahkan), lalu biarkan menempel pada QR. Hysteresis mencegah muka berbalik-balik di ~45°.
            bool table = reacquired ? wall < 0.5f : onTable ? wall < 0.7f : wall < 0.3f;
            if (reacquired || table != onTable) viewerYaw = table ? yawToViewer : 0f;
            onTable = table;
            target.rotation = Quaternion.AngleAxis(viewerYaw, up) * target.rotation;
            markerPose = smoother.Filter(target, Time.deltaTime);
            hasPose = true;
            if (lastSeen < 0f) Analytics.Log("marker_detected", ("artifact", currentData.artifactId));
            lastSeen = Time.time;
        }

        /// <summary>Kode QR lebih dulu; kartu penanda lama hanya bila tidak ada QR yang dikenali.</summary>
        bool FindTarget(out ArtifactData data, out Vector2[] cornersPx, out float size)
        {
            size = qrSizeMeters;
            foreach (var d in qrDetector.Detect(regions))
            {
                data = d.Decoded ? AppSession.Catalog.FindByQr(d.text) : null;
                if (data != null) lastQrRead = Time.time;
                // Frame buram: pola QR terlihat tapi isinya tak terbaca -> tetap pakai artefak dari QR yang baru saja terbaca.
                else if (!d.Decoded && currentData != null && Time.time - lastQrRead < KeepQrWithoutDecodeSeconds) data = currentData;
                if (data == null || data.prefab == null) continue;
                cornersPx = d.corners;
                return true;
            }
            if (detectLegacyCards && codes.Count > 0)
            {
                foreach (var d in detector.Detect(regions, codes))
                {
                    data = AppSession.Catalog.FindByMarker(d.code);
                    if (data == null || data.prefab == null) continue;
                    cornersPx = d.corners;
                    size = markerSizeMeters;
                    return true;
                }
            }
            data = null;
            cornersPx = null;
            return false;
        }

        void EnsureArtifact(ArtifactData data, float size)
        {
            if (currentData == data) return;
            if (current != null) Destroy(current.gameObject);
            currentData = data;
            AppSession.SelectedArtifactId = data.artifactId;
            var go = Instantiate(data.prefab);
            go.name = data.prefab.name;
            current = go.GetComponent<ArtifactInstance>();
            current.Init(data);
            AudioManager.Instance.PlayMusic(data.backgroundMusic);
            var b = current.GetWorldBounds();
            float width = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            baseScale = size * fitToMarker / width;
            float s = Mathf.Max(1e-6f, go.transform.lossyScale.x);
            boundsCenter = go.transform.InverseTransformPoint(b.center);
            boundsExtents = b.extents / s;
            userRotation = Quaternion.identity;
            userScale = 1f;
            hud.Bind(current, cam, ResetView, RotateBy);
            RefreshTexts();
        }

        void ApplyPose()
        {
            var t = current.transform;
            float scale = baseScale * userScale;
            var baseRotation = cam.transform.rotation * markerPose.rotation;
            var rotation = baseRotation * userRotation;
            // Letak pusat artefak terhadap QR: di meja alasnya duduk di QR; di layar/dinding pusatnya tepat di
            // depan QR dengan sisi belakang menempel ke bidang QR. Putar/miring berporos di pusat artefak.
            var pivot = Vector3.Lerp(boundsCenter, new Vector3(0f, 0f, -boundsExtents.z), wallWeight);
            t.position = cam.transform.TransformPoint(markerPose.position)
                         + baseRotation * (pivot * scale) - rotation * (boundsCenter * scale);
            t.rotation = rotation;
            t.localScale = Vector3.one * scale;

            // Cahaya di bawah artefak hanya saat QR rebah di meja (memudar bila QR berdiri di layar/dinding).
            if (glow != null)
            {
                float radius = Mathf.Max(boundsExtents.x, boundsExtents.z) * scale * 1.5f;
                glow.Place(cam.transform.TransformPoint(markerPose.position), baseRotation * Vector3.up, radius,
                    Mathf.Clamp01(1f - wallWeight * 1.6f));
            }
        }

        // ------------------------------------------------------------------ gestur & kontrol

        /// <summary>
        /// Geser mendatar = putar pada sumbu tegak artefak; geser tegak = miringkan pada sumbu kanan layar
        /// (ala trackball), sehingga sisi depan/atas bisa dilihat dari sudut scan mana pun.
        /// </summary>
        void OnDrag(Vector2 delta)
        {
            float perPixel = DegreesPerDp / Mathf.Max(0.01f, TouchGestures.DpToPixels(1f));
            RotateBy(-delta.x * perPixel, delta.y * perPixel);
        }

        /// <summary>Gestur & tombol HUD: + yaw = muka artefak bergeser ke kiri, + tilt = sisi atas menjauh.</summary>
        void RotateBy(float yawDegrees, float tiltDegrees)
        {
            if (current == null || !current.gameObject.activeSelf || !hasPose) return;
            var yaw = Quaternion.AngleAxis(yawDegrees, Vector3.up);
            var screenRight = Quaternion.Inverse(markerPose.rotation) * Vector3.right; // sumbu kanan kamera di ruang pose
            screenRight.y = 0f;
            var tilt = screenRight.sqrMagnitude > 1e-4f
                ? Quaternion.AngleAxis(tiltDegrees, screenRight.normalized)
                : Quaternion.identity;
            userRotation = Quaternion.Normalize(tilt * yaw * userRotation);
            if (glow != null) glow.Pulse();
        }

        void OnPinch(float ratio)
        {
            if (current == null || !current.gameObject.activeSelf) return;
            userScale = Mathf.Clamp(userScale * ratio, ArtifactInstance.MinScale, ArtifactInstance.MaxScale);
        }

        void StopAutoRotate()
        {
            if (current != null && current.autoRotate != null) current.autoRotate.Active = false;
        }

        void ResetView()
        {
            userRotation = Quaternion.identity;
            userScale = 1f;
            if (current != null && current.autoRotate != null) current.autoRotate.Active = false;
        }

        void ToggleLock()
        {
            if (state == State.Locked)
            {
                lastSeen = Time.time; // beri jeda singkat sebelum dianggap hilang
                SetState(State.Searching);
            }
            else if (current != null && current.gameObject.activeSelf)
            {
                SetState(State.Locked);
            }
        }

        void Back()
        {
            if (currentData != null) AppSession.OpenViewer();
            else AppSession.OpenCatalog();
        }

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
                tiltButtons = true,
                panel = new ControlPanelConfig
                {
                    getScale = () => userScale,
                    setScale = s => userScale = Mathf.Clamp(s, ArtifactInstance.MinScale, ArtifactInstance.MaxScale),
                    toggleKey = "marker.lock",
                    getToggle = () => state == State.Locked,
                    setToggle = on =>
                    {
                        if (on != (state == State.Locked)) ToggleLock();
                    },
                    toggleEnabled = () => state == State.Locked || (current != null && current.gameObject.activeSelf)
                }
            });
            topBar = TopBar.Create(safe, Back, true);
            coach = CoachCard.Create(safe, top);
            message = MessageDialog.Create(full, "common.back", Back);
            glow = GroundGlow.Create();
            if (glow != null) glow.SetVisible(false);
            uiReady = true;
        }

        void SetState(State s)
        {
            if (state == s && refreshed) return;
            state = s;
            RefreshTexts();
        }

        void RefreshTexts()
        {
            if (!uiReady) return;
            refreshed = true;
            string title = null, body = null, primary = null;
            UnityEngine.Events.UnityAction action = null;
            topBar.SetTitle(currentData != null ? currentData.displayName.Get() : Locale.T("nav.scan"));
            switch (state)
            {
                case State.Starting:
                case State.Searching: coach.Show(Icon.ScanFrame, Locale.T("marker.searching"), null, true); break;
                case State.Tracking: coach.Show(Icon.Move, Locale.T("marker.tracking"), null, false, 5f); break;
                case State.Locked: coach.Show(Icon.Lock, Locale.T("marker.locked"), null, false, 5f); break;
                case State.NoCamera:
                    title = Locale.T("marker.noCameraTitle");
                    body = Locale.T("marker.noCameraBody");
                    primary = Locale.T("common.retry");
                    action = RetryCamera;
                    break;
                case State.CameraStalled:
                    title = Locale.T("marker.stalledTitle");
                    body = Locale.T("marker.stalledBody");
                    primary = Locale.T("common.retry");
                    action = RetryCamera;
                    break;
                case State.NeedsPermission:
                    title = Locale.T("ar.permTitle");
                    body = Locale.T("ar.permBody");
                    primary = Locale.T("ar.permAllow");
                    action = RequestPermission;
                    break;
                case State.PermissionDenied:
                    title = Locale.T("ar.permTitle");
                    body = Locale.T("ar.permDeniedBody");
                    primary = Locale.T("ar.openSettings");
                    action = CameraPermission.OpenAppSettings;
                    break;
            }
            if (title == null)
            {
                message.Hide();
                return;
            }
            coach.Hide();
            message.Show(title, body, primary, action);
        }
    }
}
