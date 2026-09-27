using UnityEngine;
using UnityEngine.InputSystem;

namespace NusantaraAR.Marker
{
    /// <summary>
    /// Estimasi pose marker planar dari homografi (metode Zhang) dengan intrinsik kamera pinhole
    /// (fokus f piksel, titik pusat cx, cy). Hasil: posisi 4 sudut marker di ruang lokal kamera Unity
    /// (x kanan, y atas, z maju).
    /// </summary>
    public static class MarkerPose
    {
        /// <param name="cornersPx">TL, TR, BR, BL dalam piksel (y ke bawah).</param>
        /// <param name="size">Panjang sisi kotak hitam marker (meter).</param>
        public static bool TryEstimate(Vector2[] cornersPx, float size, float f, float cx, float cy, out Vector3[] cornersCamera)
        {
            cornersCamera = null;
            float s = size * 0.5f;
            var model = new[] { new Vector2(-s, -s), new Vector2(s, -s), new Vector2(s, s), new Vector2(-s, s) };
            if (!Homography.FromPoints(model, cornersPx, out var H)) return false;

            // B = K^-1 H
            double[] B = new double[9];
            for (int c = 0; c < 3; c++)
            {
                B[0 + c] = (H[0 + c] - cx * H[6 + c]) / f;
                B[3 + c] = (H[3 + c] - cy * H[6 + c]) / f;
                B[6 + c] = H[6 + c];
            }
            var h1 = new Vector3((float)B[0], (float)B[3], (float)B[6]);
            var h2 = new Vector3((float)B[1], (float)B[4], (float)B[7]);
            var h3 = new Vector3((float)B[2], (float)B[5], (float)B[8]);
            float norm = (h1.magnitude + h2.magnitude) * 0.5f;
            if (norm < 1e-9f) return false;
            float lambda = 1f / norm;
            if (h3.z * lambda < 0f) lambda = -lambda; // marker harus di depan kamera
            var r1 = h1 * lambda;
            var r2 = h2 * lambda;
            var t = h3 * lambda;

            cornersCamera = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                var p = r1 * model[i].x + r2 * model[i].y + t; // ruang kamera CV (y ke bawah)
                cornersCamera[i] = new Vector3(p.x, -p.y, p.z);
            }
            return true;
        }

        /// <summary>
        /// Pose artefak di atas marker (ruang kamera Unity): sumbu atas = normal kartu (menghadap kamera),
        /// depan (+Z) = ke arah tepi atas kartu, sehingga muka artefak (-Z) menghadap tepi bawah/pengguna.
        /// </summary>
        public static Pose ArtifactPose(Vector3[] c)
        {
            var center = (c[0] + c[1] + c[2] + c[3]) * 0.25f;
            var right = ((c[1] - c[0]) + (c[2] - c[3])).normalized;
            var top = ((c[0] - c[3]) + (c[1] - c[2])).normalized;
            var normal = Vector3.Cross(top, right).normalized;
            if (Vector3.Dot(normal, -center) < 0f) normal = -normal;
            return new Pose(center, Quaternion.LookRotation(top, normal));
        }

        /// <summary>
        /// Pose artefak yang berdiri tegak menurut gravitasi, apa pun kemiringan QR (ruang kamera Unity).
        /// QR di meja: artefak berdiri di atas QR, muka (-Z) ke tepi bawah QR (sama seperti <see cref="ArtifactPose"/>).
        /// QR di layar/dinding: artefak tetap tegak dan mukanya menghadap keluar dari QR (ke pengguna),
        /// bukan terbaring dengan sisi atasnya ke kamera.
        /// </summary>
        /// <param name="up">Arah atas dunia dalam ruang kamera (dari sensor gravitasi).</param>
        /// <param name="wallWeight">0 = QR mendatar (meja), 1 = QR tegak (layar/dinding); peralihan halus di 30°-65°.</param>
        /// <param name="yawToViewer">Putaran (kelipatan 90°, pada sumbu <paramref name="up"/>) agar muka artefak
        /// menghadap pengguna walau QR di meja diletakkan miring/terbalik; 0 bila QR tegak.</param>
        public static Pose UprightPose(Vector3[] c, Vector3 up, out float wallWeight, out float yawToViewer)
        {
            var qr = ArtifactPose(c);
            var normal = qr.up;
            var top = qr.forward;
            float tilt = Vector3.Angle(normal, up);
            wallWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 65f, tilt));

            // Dua petunjuk arah depan (+Z, menjauhi pengguna) yang saling menguatkan di semua kemiringan:
            // tepi atas QR (dominan saat QR mendatar) dan kebalikan normal QR (dominan saat QR tegak).
            var forward = Vector3.ProjectOnPlane(top, up) + Vector3.ProjectOnPlane(-normal, up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(Vector3.down, up); // kamera tepat tegak lurus ke bawah

            yawToViewer = 0f;
            var view = Vector3.ProjectOnPlane(qr.position, up); // kamera -> QR, mendatar
            if (wallWeight < 0.5f && view.sqrMagnitude > 1e-6f)
                yawToViewer = Mathf.Round(Vector3.SignedAngle(forward, view, up) / 90f) * 90f;

            return new Pose(qr.position, Quaternion.LookRotation(forward.normalized, up));
        }
    }

    /// <summary>
    /// Arah atas dunia dalam ruang kamera belakang, dari sensor gravitasi HP (atau akselerometer yang diredam).
    /// Aplikasi terkunci potret sehingga sumbu X/Y perangkat = sumbu kamera; Z perangkat keluar dari layar,
    /// sedangkan kamera belakang memandang ke arah sebaliknya. Tanpa sensor (Editor), atas layar dianggap atas dunia.
    /// </summary>
    public class DeviceGravity
    {
        const float SmoothingPerSecond = 5f;

        InputDevice gravity, accel;
        bool enabledGravity, enabledAccel;
        Vector3 down;
        bool hasReading;

        /// <summary>Arah atas dunia di ruang lokal kamera (Vector3.up bila tidak ada sensor).</summary>
        public Vector3 UpInCamera => hasReading ? new Vector3(-down.x, -down.y, down.z) : Vector3.up;

        public void Enable()
        {
            gravity = GravitySensor.current;
            accel = Accelerometer.current;
            enabledGravity = EnableIfNeeded(gravity);
            enabledAccel = EnableIfNeeded(accel);
        }

        public void Disable()
        {
            if (enabledGravity && gravity != null && gravity.added) InputSystem.DisableDevice(gravity);
            if (enabledAccel && accel != null && accel.added) InputSystem.DisableDevice(accel);
            enabledGravity = enabledAccel = false;
        }

        public void Update(float dt)
        {
            if (!TryRead(out var raw)) return;
            // Konvensi Unity: vektor = arah gravitasi (ke bawah) dalam g, ruang perangkat.
            if (!hasReading)
            {
                down = raw;
                hasReading = true;
                return;
            }
            // Akselerometer ikut terguncang gerakan tangan; redam agar artefak tidak goyang.
            down = Vector3.Slerp(down, raw, 1f - Mathf.Exp(-SmoothingPerSecond * dt)).normalized;
        }

        bool TryRead(out Vector3 dir)
        {
            dir = default;
            var v = Vector3.zero;
            if (gravity is GravitySensor g && g.enabled) v = g.gravity.ReadValue();
            if (v.sqrMagnitude < 0.25f && accel is Accelerometer a && a.enabled) v = a.acceleration.ReadValue();
            if (v.sqrMagnitude < 0.25f) return false; // belum ada data / jatuh bebas
            dir = v.normalized;
            return true;
        }

        static bool EnableIfNeeded(InputDevice device)
        {
            if (device == null || device.enabled) return false;
            InputSystem.EnableDevice(device);
            return true;
        }
    }

    /// <summary>Filter One Euro untuk mengurangi getaran pose tanpa menambah jeda saat bergerak cepat.</summary>
    public class PoseSmoother
    {
        public float minCutoff = 1.5f;
        public float beta = 8f;
        public float rotationMinCutoff = 1.5f;
        public float rotationBeta = 0.6f;

        bool initialized;
        Vector3 position, velocity;
        Quaternion rotation;

        public void Reset() => initialized = false;

        public Pose Filter(Pose target, float dt)
        {
            dt = Mathf.Max(dt, 1e-3f);
            if (!initialized)
            {
                initialized = true;
                position = target.position;
                rotation = target.rotation;
                velocity = Vector3.zero;
                return target;
            }
            var rawVelocity = (target.position - position) / dt;
            velocity = Vector3.Lerp(velocity, rawVelocity, Alpha(1f, dt));
            float cutoff = minCutoff + beta * velocity.magnitude;
            position = Vector3.Lerp(position, target.position, Alpha(cutoff, dt));

            float angle = Quaternion.Angle(rotation, target.rotation) * Mathf.Deg2Rad / dt;
            float rotCutoff = rotationMinCutoff + rotationBeta * angle;
            rotation = Quaternion.Slerp(rotation, target.rotation, Alpha(rotCutoff, dt));
            return new Pose(position, rotation);
        }

        static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }
    }
}
