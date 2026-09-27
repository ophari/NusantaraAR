using UnityEngine;

namespace NusantaraAR
{
    /// <summary>Kamera orbit untuk mode 3D Viewer (PRD FR-12): geser = putar, pinch/scroll = zoom.</summary>
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

        Vector3 homeTarget;
        float homeDistance, homeYaw, homePitch;
        bool easing;
        Vector3 easeTarget;
        float easeDistance;

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

        /// <summary>Jarak kamera agar seluruh bounds (dunia) masuk layar dari sudut mana pun.</summary>
        public float DistanceToFit(Bounds bounds)
        {
            var cam = GetComponent<Camera>();
            float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            float fov = Mathf.Deg2Rad * cam.fieldOfView * 0.5f;
            if (cam.aspect < 1f) fov = Mathf.Atan(Mathf.Tan(fov) * cam.aspect); // potret: batasi oleh lebar
            return radius / Mathf.Sin(fov) * 1.05f;
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
        }
    }
}
