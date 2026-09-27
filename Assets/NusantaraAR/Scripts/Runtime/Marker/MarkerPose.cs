using UnityEngine;

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
