using System;
using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR
{
    public enum ArtifactCategory { KerisSenjata, Arca, KriaLogam }

    /// <summary>Kapan hotspot boleh tampil (PRD §6.3).</summary>
    public enum HotspotStage
    {
        /// <summary>Terlihat sejak keris utuh (hulu, mendak, warangka, gandar/pendok).</summary>
        Utuh,
        /// <summary>Hanya setelah bilah dihunus/dibongkar (ganja, gandik, luk, pamor, ...).</summary>
        Bilah
    }

    [Serializable]
    public class HotspotData
    {
        public string hotspotId;
        [Tooltip("Nama ArtifactPart tempat hotspot menempel")] public string partName;
        [Tooltip("Posisi lokal terhadap transform bagian tersebut")] public Vector3 localPosition;
        public HotspotStage visibleFrom = HotspotStage.Utuh;

        public LocalizedString title;
        [Tooltip("Istilah daerah, mis. 'Jawa: ukiran - Bali: danganan'")] public string regionalTerm;
        public LocalizedString material;
        public LocalizedString philosophy;
        public LocalizedString craft;
        public LocalizedString history;

        public AudioClip narrationID;
        public AudioClip narrationEN;
        public AudioClip pronunciation;
        public LocalizedString transcript;

        public List<string> sources = new List<string>();

        [Header("Validasi (gerbang rilis PRD §6.4)")]
        public bool curatorValidated;
        public string curatorNote;

        public AudioClip Narration =>
            Locale.Current == Language.EN && narrationEN != null ? narrationEN : narrationID;
    }

    /// <summary>Satu artefak di katalog (PRD §6.2).</summary>
    [CreateAssetMenu(menuName = "Nusantara AR/Artifact", fileName = "Artifact")]
    public class ArtifactData : ScriptableObject
    {
        public string artifactId;
        public LocalizedString displayName;
        public string localName;
        public ArtifactCategory category;
        public LocalizedString region;
        public LocalizedString era;
        public LocalizedString summary;
        public Texture2D thumbnail;
        [Tooltip("Prefab berskala nyata (1 unit = 1 m) dengan komponen ArtifactInstance")] public GameObject prefab;

        [Header("Spesimen")]
        public string specimenOwner;
        public string collectionNumber;
        public string license;
        public LocalizedString measurementNote;

        [Header("Kartu penanda lama (cadangan Scan QR, tanpa ARCore)")]
        [Tooltip("Kode 16-bit pola kartu penanda (lihat MarkerPattern). 0 = tidak punya kartu.")]
        public int markerCode;

        /// <summary>Isi kode QR artefak ini, mis. "NUSANTARA:KERIS_BALI_01". Diturunkan dari artifactId (tidak diisi manual).</summary>
        public string QrText => ContentCatalog.QrPrefix + artifactId;

        [Header("Model sementara")]
        public bool isPlaceholder;
        public LocalizedString placeholderNote;

        public List<HotspotData> hotspots = new List<HotspotData>();

        [Tooltip("Mode Kisah (narasi bercerita). Kosong = tombol Kisah disembunyikan.")]
        public ArtifactStory story;

        [Header("Musik latar")]
        [Tooltip("Diputar berulang selama artefak dibuka (3D Viewer, Scan QR, AR). Kosong = tanpa musik.")]
        public AudioClip backgroundMusic;
        [Tooltip("Kredit musik: judul - pembuat (sumber, lisensi)")] public string musicCredit;

        public HotspotData FindHotspot(string id) => hotspots.Find(h => h.hotspotId == id);
    }
}
