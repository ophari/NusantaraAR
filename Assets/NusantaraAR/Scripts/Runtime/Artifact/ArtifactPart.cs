using UnityEngine;

namespace NusantaraAR
{
    /// <summary>
    /// Satu bagian modular artefak (GameObject terpisah, bukan sub-mesh) dengan pivot di titik sambungnya.
    /// Nama bagian dipakai hotspot (<see cref="HotspotData.partName"/>).
    /// </summary>
    public class ArtifactPart : MonoBehaviour
    {
        public string partName;
        public Renderer[] renderers;

        /// <summary>True bila setidaknya satu renderer bagian ini sedang terlihat.</summary>
        public bool IsVisible
        {
            get
            {
                if (renderers == null || renderers.Length == 0) return true;
                foreach (var r in renderers)
                    if (r != null && r.enabled) return true;
                return false;
            }
        }
    }
}
