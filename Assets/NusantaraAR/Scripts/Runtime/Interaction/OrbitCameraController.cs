using UnityEngine;

namespace NusantaraAR
{
    /// <summary>
    /// Kamera orbit untuk mode 3D Viewer (PRD FR-12): geser = putar, pinch/scroll = zoom, tombol = putar/miring.
    /// Bila sebagian layar tertutup UI (top bar, sheet + bar navigasi), pusat proyeksi digeser agar objek berada di
    /// tengah area yang terlihat; ScreenPointToRay/WorldToScreenPoint ikut matriks ini sehingga ketuk & label tetap tepat.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitCameraController : MonoBehaviour
    {
        public TouchGestures gestures;
        public Vector3 target;
        public float distance = 1f;
        public float minDistance = 0.2f;
        public float maxDistance = 3f;
        public float yaw;
        public float pitch = 12f;
        public float minPitch = -10f;
        public float maxPitch = 80f;
        [Tooltip("Derajat per dp geser")] public float degreesPerDp = 0.35f;

        [Tooltip("Kecepatan kamera menuju posisi otomatis (EaseTo)")] public float easeSpeed = 4f;

        Camera cam;
        Vector3 homeTarget;
        float homeDistance, homeYaw, homePitch;
        bool easing;
        Vector3 easeTarget;
        float easeDistance;
        float coveredTop, coveredBottom, coveredLeft, coveredRight; // fraksi layar yang tertutup UI

        Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();

        void OnEnable()
        {
            if (gestures == null) return;
            gestures.Dragged += OnDrag;
            gestures.Pinched += OnPinch;
        }

        void OnDisable()
        {
            if (gestures == null) return;
            gestures.Dragged -= OnDrag;
            gestures.Pinched -= OnPinch;
        }

        /// <summary>Fraksi tinggi (atas/bawah) dan lebar (kiri/kanan) layar, 0..1, yang tertutup UI.</summary>
        public void SetCoveredScreen(float top, float bottom, float left = 0f, float right = 0f)
        {
            coveredTop = Mathf.Clamp(top, 0f, 0.45f);
            coveredBottom = Mathf.Clamp(bottom, 0f, 0.6f);
            coveredLeft = Mathf.Clamp(left, 0f, 0.35f);
            coveredRight = Mathf.Clamp(right, 0f, 0.35f);
            ApplyProjection();
        }

        /// <summary>Membingkai bounds (dunia) dan menjadikannya posisi "reset".</summary>
        public void Frame(Bounds bounds)
        {
            easing = false;
            target = bounds.center;
            float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            distance = DistanceToFit(bounds);
            minDistance = radius * 0.35f;
            maxDistance = distance * 3f;
            yaw = 0f;
            pitch = 12f;
            homeTarget = target;
            homeDistance = distance;
            homeYaw = yaw;
            homePitch = pitch;
            Apply();
        }

        /// <summary>Jarak kamera agar seluruh bounds (dunia) masuk area terlihat dari sudut mana pun.</summary>
        public float DistanceToFit(Bounds bounds)
        {
            float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            float tanV = Mathf.Tan(Mathf.Deg2Rad * Cam.fieldOfView * 0.5f);
            float visibleH = Mathf.Max(0.3f, 1f - coveredTop - coveredBottom);
            float visibleW = Mathf.Max(0.3f, 1f - coveredLeft - coveredRight);
            float half = Mathf.Min(Mathf.Atan(tanV * visibleH), Mathf.Atan(tanV * Cam.aspect * visibleW)); // potret: dibatasi lebar
            return radius / Mathf.Sin(half) * 1.05f;
        }

        /// <summary>Menggeser titik pandang & jarak secara halus (mis. mundur selama animasi hunus). Pinch membatalkan.</summary>
        public void EaseTo(Vector3 center, float dist)
        {
            easeTarget = center;
            easeDistance = dist;
            easing = true;
        }

        public void ResetView()
        {
            easing = false;
            target = homeTarget;
            distance = homeDistance;
            yaw = homeYaw;
            pitch = homePitch;
            Apply();
        }

        /// <summary>Putar/miring dari tombol (derajat orbit kamera).</summary>
        public void Nudge(float yawDegrees, float pitchDegrees)
        {
            yaw += yawDegrees;
            pitch = Mathf.Clamp(pitch + pitchDegrees, minPitch, maxPitch);
            Apply();
        }

        void OnDrag(Vector2 deltaPixels)
        {
            float perPixel = degreesPerDp / Mathf.Max(0.01f, TouchGestures.DpToPixels(1f));
            yaw += deltaPixels.x * perPixel;
            pitch = Mathf.Clamp(pitch - deltaPixels.y * perPixel, minPitch, maxPitch);
            Apply();
        }

        void OnPinch(float ratio)
        {
            if (ratio <= 0f) return;
            easing = false;
            distance = Mathf.Clamp(distance / ratio, minDistance, maxDistance);
            Apply();
        }

        void LateUpdate()
        {
            if (easing)
            {
                float k = 1f - Mathf.Exp(-easeSpeed * Time.deltaTime);
                target = Vector3.Lerp(target, easeTarget, k);
                distance = Mathf.Lerp(distance, easeDistance, k);
                if ((target - easeTarget).sqrMagnitude < 1e-8f && Mathf.Abs(distance - easeDistance) < 1e-4f) easing = false;
            }
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = target + rot * new Vector3(0f, 0f, -distance);
            transform.rotation = rot;
            ApplyProjection();
        }

        /// <summary>
        /// Proyeksi off-center: pusat gambar pindah ke tengah area yang tidak tertutup UI
        /// (NDC x = kiri − kanan, NDC y = bawah − atas; NDC' = NDC − m02/m12).
        /// </summary>
        void ApplyProjection()
        {
            var c = Cam;
            c.ResetProjectionMatrix();
            if (coveredTop <= 0f && coveredBottom <= 0f && coveredLeft <= 0f && coveredRight <= 0f) return;
            var p = c.projectionMatrix;
            p.m02 = coveredRight - coveredLeft;
            p.m12 = coveredTop - coveredBottom;
            c.projectionMatrix = p;
        }
    }
}
