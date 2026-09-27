using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR
{
    /// <summary>Daftar seluruh artefak. Disimpan di Resources/ContentCatalog agar bisa dimuat dari scene mana pun.</summary>
    [CreateAssetMenu(menuName = "Nusantara AR/Content Catalog", fileName = "ContentCatalog")]
    public class ContentCatalog : ScriptableObject
    {
        public const string ResourcePath = "ContentCatalog";

        public List<ArtifactData> artifacts = new List<ArtifactData>();

        public static ContentCatalog Load() => Resources.Load<ContentCatalog>(ResourcePath);

        public ArtifactData First => artifacts.Count > 0 ? artifacts[0] : null;

        public ArtifactData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return artifacts.Find(a => a != null && a.artifactId == id);
        }

        public ArtifactData FindByMarker(int code)
        {
            if (code == 0) return null;
            return artifacts.Find(a => a != null && a.markerCode == code);
        }

        /// <summary>Kategori yang punya konten; kategori kosong tidak ditampilkan (PRD FR-11).</summary>
        public List<ArtifactCategory> NonEmptyCategories()
        {
            var result = new List<ArtifactCategory>();
            foreach (var a in artifacts)
                if (a != null && !result.Contains(a.category)) result.Add(a.category);
            result.Sort();
            return result;
        }
    }
}
