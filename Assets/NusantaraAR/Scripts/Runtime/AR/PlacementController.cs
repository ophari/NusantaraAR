using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace NusantaraAR
{
    /// <summary>
    /// Raycast dari tengah layar ke bidang AR, menggerakkan reticle, meletakkan artefak, dan
    /// mengaitkannya ke ARAnchor (PRD FR-03, FR-06). Geser 2 jari memindahkan objek di atas bidang.
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        public ARRaycastManager raycastManager;
        public ARPlaneManager planeManager;
        public Camera arCamera;
        public ReticleView reticle;

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        ARAnchor anchor;
        bool planesVisible = true;
        bool dragging;

        public ArtifactInstance Placed { get; private set; }
        public bool HasTargetPose { get; private set; }
        public Pose TargetPose { get; private set; }

        /// <summary>Raycast dari tengah layar; true bila ada bidang yang bisa ditempati.</summary>
        public bool UpdateTarget(bool showReticle)
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            HasTargetPose = Raycast(center, out var pose);
            if (HasTargetPose) TargetPose = pose;
            if (reticle != null)
            {
                if (HasTargetPose && showReticle) reticle.Show(pose);
                else reticle.Hide();
            }
            return HasTargetPose;
        }

        bool Raycast(Vector2 screenPoint, out Pose pose)
        {
            pose = default;
            if (raycastManager == null) return false;
            if (!raycastManager.Raycast(screenPoint, Hits, TrackableType.PlaneWithinPolygon)) return false;
            pose = Hits[0].pose;
            return true;
        }

        /// <summary>Meletakkan (atau memindahkan) artefak pada pose target, menghadap kamera.</summary>
        public ArtifactInstance Place(ArtifactData data)
        {
            if (!HasTargetPose || data == null || data.prefab == null) return null;
            if (Placed == null)
            {
                var go = Instantiate(data.prefab);
                go.name = data.prefab.name;
                Placed = go.GetComponent<ArtifactInstance>();
                Placed.Init(data);
            }
            Placed.gameObject.SetActive(true);
            Detach();
            Placed.transform.position = TargetPose.position;
            Placed.FaceTowards(arCamera.transform.position);
            AttachAnchor();
            if (reticle != null) reticle.Hide();
            return Placed;
        }

        /// <summary>"Pindahkan": lepas anchor lama, sembunyikan objek, kembali ke mode penempatan.</summary>
        public void BeginReposition()
        {
            if (Placed == null) return;
            Detach();
            Placed.gameObject.SetActive(false);
        }

        /// <summary>Geser 2 jari: ikuti titik tengah jari di atas bidang.</summary>
        public void DragTo(Vector2 screenPoint)
        {
            if (Placed == null || !Raycast(screenPoint, out var pose)) return;
            if (!dragging)
            {
                dragging = true;
                Detach();
            }
            Placed.transform.position = pose.position;
        }

        public void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            AttachAnchor();
        }

        void AttachAnchor()
        {
            if (Placed == null) return;
            var go = new GameObject("ArtifactAnchor");
            go.transform.SetPositionAndRotation(Placed.transform.position, Quaternion.identity);
            // Menambahkan komponen ARAnchor membuat anchor baru di subsistem AR.
            anchor = go.AddComponent<ARAnchor>();
            Placed.transform.SetParent(go.transform, true);
        }

        void Detach()
        {
            if (Placed != null) Placed.transform.SetParent(null, true);
            if (anchor != null) Destroy(anchor.gameObject);
            anchor = null;
        }

        public void SetPlanesVisible(bool visible)
        {
            planesVisible = visible;
            ApplyPlaneVisibility();
        }

        void LateUpdate() => ApplyPlaneVisibility();

        void ApplyPlaneVisibility()
        {
            if (planeManager == null) return;
            foreach (var plane in planeManager.trackables)
                if (plane.gameObject.activeSelf != planesVisible) plane.gameObject.SetActive(planesVisible);
        }
    }
}
