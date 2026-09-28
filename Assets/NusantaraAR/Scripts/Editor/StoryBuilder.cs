using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Membangun aset mode Kisah (<see cref="ArtifactStory"/>) dari naskah <c>Tools/narasi/kisah.json</c>,
    /// audio hasil <c>Tools/narasi/kisah_tts.py</c> (<c>Content/&lt;id&gt;/Story/*.mp3</c>) dan waktu kalimatnya
    /// (<c>Tools/narasi/kisah_cues.json</c>), lalu memasangnya ke <see cref="ArtifactData.story"/>.
    /// Aset kisah terpisah dari ArtifactData agar tidak tertimpa builder keris.
    /// Menu: Nusantara AR / Bangun Kisah (juga dipanggil dari Setup Everything).
    /// </summary>
    public static class StoryBuilder
    {
        const string ContentRoot = GlbArtifact.Root + "/Content";
        static string ToolsDir => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tools", "narasi");

#pragma warning disable 0649 // diisi JsonUtility
        [Serializable] class ScriptFile { public string voiceID, voiceEN; public ArtifactScript[] artifacts; }
        [Serializable] class ArtifactScript { public string artifactId; public ChapterScript[] chapters; }
        [Serializable] class ChapterScript { public string key, focus, titleID, titleEN, id, en; public int stage = -1; }
        [Serializable] class CueFile { public ClipCues[] clips; }
        [Serializable] class ClipCues { public string file; public StoryCue[] cues; }
#pragma warning restore 0649

        [MenuItem("Nusantara AR/Bangun Kisah")]
        static void BuildMenu()
        {
            Build();
            Debug.Log("[NusantaraAR] Kisah selesai dibangun.");
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
            string scriptPath = Path.Combine(ToolsDir, "kisah.json");
            if (!File.Exists(scriptPath))
            {
                Debug.LogWarning("[NusantaraAR] Naskah kisah tidak ada: " + scriptPath);
                return;
            }
            var script = JsonUtility.FromJson<ScriptFile>(File.ReadAllText(scriptPath));
            string cuesPath = Path.Combine(ToolsDir, "kisah_cues.json");
            var cues = File.Exists(cuesPath)
                ? JsonUtility.FromJson<CueFile>(File.ReadAllText(cuesPath)).clips.ToDictionary(c => c.file, c => c.cues)
                : new Dictionary<string, StoryCue[]>();

            AssetDatabase.Refresh();
            foreach (var art in script.artifacts)
            {
                string dir = ContentRoot + "/" + art.artifactId;
                var data = AssetDatabase.LoadAssetAtPath<ArtifactData>(dir + "/" + art.artifactId + ".asset");
                if (data == null) throw new InvalidOperationException("Kisah untuk artefak yang belum ada: " + art.artifactId);
                var exploded = data.prefab != null ? data.prefab.GetComponent<ExplodedViewController>() : null;
                int stageCount = exploded != null ? exploded.StageCount : 1;

                string storyDir = dir + "/Story";
                Directory.CreateDirectory(storyDir);
                string storyPath = storyDir + "/" + art.artifactId + "_Story.asset";
                var story = AssetDatabase.LoadAssetAtPath<ArtifactStory>(storyPath);
                if (story == null)
                {
                    story = ScriptableObject.CreateInstance<ArtifactStory>();
                    AssetDatabase.CreateAsset(story, storyPath);
                }
                story.voiceCredit = "Microsoft Edge TTS: " + script.voiceID + " / " + script.voiceEN;
                story.curatorValidated = false;
                story.chapters = new List<StoryChapter>();

                for (int i = 0; i < art.chapters.Length; i++)
                {
                    var c = art.chapters[i];
                    if (c.stage >= stageCount)
                        throw new InvalidOperationException($"{art.artifactId} bab '{c.key}': tahap {c.stage} tidak ada (jumlah tahap {stageCount}).");
                    if (!string.IsNullOrEmpty(c.focus) && data.FindHotspot(c.focus) == null)
                        throw new InvalidOperationException($"{art.artifactId} bab '{c.key}': hotspot '{c.focus}' tidak ada.");
                    string clipBase = $"{storyDir}/{art.artifactId}_{i + 1:00}_{c.key}_";
                    story.chapters.Add(new StoryChapter
                    {
                        key = c.key,
                        title = new LocalizedString(c.titleID, c.titleEN),
                        text = new LocalizedString(c.id, c.en),
                        stage = c.stage,
                        focusHotspot = c.focus ?? "",
                        voiceID = LoadVoice(clipBase + "id.mp3"),
                        voiceEN = LoadVoice(clipBase + "en.mp3"),
                        cuesID = CuesFor(cues, clipBase + "id.mp3"),
                        cuesEN = CuesFor(cues, clipBase + "en.mp3")
                    });
                }
                EditorUtility.SetDirty(story);
                data.story = story;
                EditorUtility.SetDirty(data);
                Debug.Log($"[NusantaraAR] Kisah {art.artifactId}: {story.chapters.Count} bab, " +
                          $"{story.chapters.Count(ch => ch.voiceID != null)} suara ID, {story.chapters.Count(ch => ch.voiceEN != null)} suara EN");
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Klip narasi: mono, Vorbis terkompresi di memori, tidak dimuat bersama katalog (StoryPanel memuatnya per bab)
        /// dan dimuat di latar agar tidak menyendat UI.
        /// </summary>
        static AudioClip LoadVoice(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[NusantaraAR] Audio kisah belum dibuat (jalankan Tools/narasi/kisah_tts.py): " + path);
                return null;
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = imp.defaultSampleSettings;
            bool changed = !imp.forceToMono || !imp.loadInBackground || settings.preloadAudioData
                || settings.loadType != AudioClipLoadType.CompressedInMemory
                || settings.compressionFormat != AudioCompressionFormat.Vorbis
                || !Mathf.Approximately(settings.quality, 0.5f);
            if (changed)
            {
                imp.forceToMono = true;
                imp.loadInBackground = true;
                settings.preloadAudioData = false;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.5f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                imp.defaultSampleSettings = settings;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static List<StoryCue> CuesFor(Dictionary<string, StoryCue[]> cues, string path) =>
            cues.TryGetValue(path, out var list) ? list.ToList() : new List<StoryCue>();
    }
}
