using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Memasang musik latar artefak (<see cref="ArtifactData.backgroundMusic"/> + kredit) dari manifest
    /// <c>Tools/musik/musik.json</c>. Berkas audio: <c>Content/&lt;id&gt;/Music/*.mp3</c>, hasil <c>Tools/musik/siapkan_musik.py</c>
    /// dari unduhan Pixabay.
    /// Field musik tidak disentuh builder keris, jadi tetap terpasang saat model dibangun ulang.
    /// Menu: Nusantara AR / Pasang Musik Latar (juga dipanggil dari Setup Everything).
    /// </summary>
    public static class MusicBuilder
    {
        const string ContentRoot = GlbArtifact.Root + "/Content";
        static string ManifestPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tools", "musik", "musik.json");

#pragma warning disable 0649 // diisi JsonUtility
        [Serializable] class Manifest { public Track[] tracks; }
        [Serializable] class Track { public string artifactId, file, title, author, source, url, license; public bool aiGenerated; }
#pragma warning restore 0649

        [MenuItem("Nusantara AR/Pasang Musik Latar")]
        static void BuildMenu()
        {
            Build();
            Debug.Log("[NusantaraAR] Musik latar selesai dipasang.");
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogWarning("[NusantaraAR] Manifest musik tidak ada: " + ManifestPath);
                return;
            }
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            AssetDatabase.Refresh();
            foreach (var t in manifest.tracks)
            {
                var data = AssetDatabase.LoadAssetAtPath<ArtifactData>($"{ContentRoot}/{t.artifactId}/{t.artifactId}.asset");
                if (data == null) throw new InvalidOperationException("Musik untuk artefak yang belum ada: " + t.artifactId);
                var clip = LoadMusic(t.file);
                data.backgroundMusic = clip;
                data.musicCredit = clip != null ? $"\"{t.title}\" - {t.author} ({t.source}, {t.license})" + (t.aiGenerated ? " [AI]" : "") : "";
                EditorUtility.SetDirty(data);
                Debug.Log(clip != null
                    ? $"[NusantaraAR] Musik {t.artifactId}: {t.title} - {t.author} ({clip.length:F0} dtk)"
                    : $"[NusantaraAR] Musik {t.artifactId}: berkas belum ada ({t.file})");
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Musik: stereo, Vorbis, di-stream dari disk (hanya buffer kecil di memori) sehingga tidak ikut dimuat
        /// bersama katalog di Resources.
        /// </summary>
        static AudioClip LoadMusic(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[NusantaraAR] Berkas musik belum ada (jalankan Tools/musik/siapkan_musik.py): " + path);
                return null;
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = imp.defaultSampleSettings;
            bool changed = imp.forceToMono || !imp.loadInBackground || settings.preloadAudioData
                || settings.loadType != AudioClipLoadType.Streaming
                || settings.compressionFormat != AudioCompressionFormat.Vorbis
                || !Mathf.Approximately(settings.quality, 0.4f);
            if (changed)
            {
                imp.forceToMono = false;
                imp.loadInBackground = true;
                settings.preloadAudioData = false;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.4f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                imp.defaultSampleSettings = settings;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
