using UnityEngine;
using UnityEngine.SceneManagement;

namespace NusantaraAR
{
    /// <summary>Status lintas-scene: artefak terpilih dan navigasi antara katalog/3D Viewer dan AR.</summary>
    public static class AppSession
    {
        public const string MainScene = "Main";
        public const string ARScene = "AR";
        public const string MarkerScene = "Marker";

        static ContentCatalog catalog;

        public static ContentCatalog Catalog
        {
            get
            {
                if (catalog == null) catalog = ContentCatalog.Load();
                return catalog;
            }
        }

        /// <summary>Artefak yang sedang dibuka (null = katalog).</summary>
        public static string SelectedArtifactId { get; set; }

        /// <summary>Saat kembali ke scene Main, langsung buka halaman detail artefak terpilih.</summary>
        public static bool OpenDetailOnLoad { get; set; }

        public static ArtifactData SelectedArtifact
        {
            get
            {
                var c = Catalog;
                if (c == null) return null;
                var a = c.Find(SelectedArtifactId);
                return a != null ? a : c.First;
            }
        }

        public static void OpenAR(ArtifactData artifact)
        {
            SelectedArtifactId = artifact != null ? artifact.artifactId : null;
            SceneManager.LoadScene(ARScene);
        }

        /// <summary>Mode "Scan QR" (kamera biasa, tanpa ARCore). artifact boleh null: artefak ditentukan oleh kode QR yang dipindai.</summary>
        public static void OpenMarker(ArtifactData artifact)
        {
            SelectedArtifactId = artifact != null ? artifact.artifactId : null;
            SceneManager.LoadScene(MarkerScene);
        }

        public static void OpenCatalog()
        {
            OpenDetailOnLoad = false;
            SceneManager.LoadScene(MainScene);
        }

        /// <summary>Kembali ke scene Main, membuka halaman detail (3D Viewer) artefak terpilih.</summary>
        public static void OpenViewer()
        {
            OpenDetailOnLoad = true;
            SceneManager.LoadScene(MainScene);
        }
    }

    /// <summary>Preferensi pengguna (PlayerPrefs).</summary>
    public static class AppSettings
    {
        const string NarrationKey = "nusantaraar.vol.narration";
        const string SfxKey = "nusantaraar.vol.sfx";
        const string MusicKey = "nusantaraar.vol.music";
        const string OnboardingKey = "nusantaraar.onboarding.done";

        static float? narrationVolume;
        static float? sfxVolume;
        static float? musicVolume;

        static float ClampSafe01(float v, float fallback) =>
            float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp01(v);

        /// <summary>Segarkan cache volume dari PlayerPrefs (mis. setelah PlayerPrefs.DeleteAll).</summary>
        public static void Reload() => narrationVolume = sfxVolume = musicVolume = null;

        // Di-cache: dibaca setiap frame oleh AudioManager (PlayerPrefs di Android lewat JNI).
        public static float NarrationVolume
        {
            get => narrationVolume ??= ClampSafe01(PlayerPrefs.GetFloat(NarrationKey, 1f), 1f);
            set { narrationVolume = ClampSafe01(value, 1f); PlayerPrefs.SetFloat(NarrationKey, narrationVolume.Value); }
        }

        public static float SfxVolume
        {
            get => sfxVolume ??= ClampSafe01(PlayerPrefs.GetFloat(SfxKey, 0.7f), 0.7f);
            set { sfxVolume = ClampSafe01(value, 0.7f); PlayerPrefs.SetFloat(SfxKey, sfxVolume.Value); }
        }

        /// <summary>Musik latar artefak; 0 = mati.</summary>
        public static float MusicVolume
        {
            get => musicVolume ??= ClampSafe01(PlayerPrefs.GetFloat(MusicKey, 0.5f), 0.5f);
            set { musicVolume = ClampSafe01(value, 0.5f); PlayerPrefs.SetFloat(MusicKey, musicVolume.Value); }
        }

        public static bool OnboardingDone
        {
            get => PlayerPrefs.GetInt(OnboardingKey, 0) == 1;
            set { PlayerPrefs.SetInt(OnboardingKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
