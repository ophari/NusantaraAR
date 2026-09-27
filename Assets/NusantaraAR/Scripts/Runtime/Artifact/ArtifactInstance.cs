using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR
{
    /// <summary>
    /// Komponen root prefab artefak. Pivot root berada di dasar artefak (sejajar bidang AR) dan
    /// sisi depan artefak menghadap -Z lokal root.
    /// </summary>
    public class ArtifactInstance : MonoBehaviour
    {
        public const float MinScale = 0.5f;
        public const float MaxScale = 3f;

        public Transform modelRoot;
        public ExplodedViewController exploded;
        public AutoRotate autoRotate;

        readonly Dictionary<string, ArtifactPart> parts = new Dictionary<string, ArtifactPart>();
        Quaternion initialRotation;
        Vector3 initialScale = Vector3.one;
        bool initialized;

        public ArtifactData Data { get; private set; }

        /// <summary>Skala relatif terhadap 1:1 (1 = ukuran nyata).</summary>
        public float RelativeScale => initialScale.x > 0f ? transform.localScale.x / initialScale.x : 1f;

        void Awake() => CacheParts();

        public void Init(ArtifactData data)
        {
            Data = data;
            CacheParts();
            if (!initialized)
            {
                initialRotation = transform.localRotation;
                initialScale = transform.localScale;
                initialized = true;
            }
        }

        void CacheParts()
        {
            parts.Clear();
            foreach (var p in GetComponentsInChildren<ArtifactPart>(true))
                if (!string.IsNullOrEmpty(p.partName)) parts[p.partName] = p;
            if (exploded == null) exploded = GetComponent<ExplodedViewController>();
            if (autoRotate == null) autoRotate = GetComponent<AutoRotate>();
        }

        public ArtifactPart GetPart(string partName) =>
            partName != null && parts.TryGetValue(partName, out var p) ? p : null;

        /// <summary>Memutar muka artefak (sisi -Z) ke arah kamera, hanya pada sumbu Y.</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            var dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
            initialRotation = transform.localRotation;
        }

        public void RotateYaw(float degrees) => transform.Rotate(0f, degrees, 0f, Space.World);

        public void SetRelativeScale(float factor)
        {
            factor = Mathf.Clamp(factor, MinScale, MaxScale);
            transform.localScale = initialScale * factor;
        }

        public void MultiplyScale(float ratio) => SetRelativeScale(RelativeScale * ratio);

        /// <summary>"Reset Tampilan": rotasi awal + skala 1:1 (PRD FR-06).</summary>
        public void ResetView()
        {
            transform.localRotation = initialRotation;
            transform.localScale = initialScale;
            if (autoRotate != null) autoRotate.Active = false;
        }

        /// <summary>Apakah hotspot boleh tampil pada keadaan saat ini (PRD FR-08, §6.3).</summary>
        public bool IsHotspotAvailable(HotspotData h)
        {
            var part = GetPart(h.partName);
            if (part == null || !part.IsVisible) return false;
            if (h.visibleFrom == HotspotStage.Bilah)
                return exploded != null && exploded.CurrentStage >= 1 && !exploded.IsAnimating;
            return exploded == null || !exploded.IsAnimating;
        }

        public bool TryGetHotspotWorldPosition(HotspotData h, out Vector3 position)
        {
            var part = GetPart(h.partName);
            if (part == null)
            {
                position = default;
                return false;
            }
            position = part.transform.TransformPoint(h.localPosition);
            return true;
        }

        /// <summary>Bounds gabungan semua renderer yang terlihat (ruang dunia).</summary>
        public Bounds GetWorldBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in renderers)
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }
}
