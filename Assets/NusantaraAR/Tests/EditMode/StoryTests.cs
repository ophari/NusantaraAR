using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>Uji mode Kisah setiap artefak: suara ID/EN, subtitle per kalimat, tahap & sorotan yang valid.</summary>
    [TestFixture("KERIS_BALI_01")]
    [TestFixture("KERIS_SUMATRA_01")]
    [TestFixture("BOROBUDUR_01")]
    public class StoryTests
    {
        readonly string id;
        GameObject go;
        ArtifactInstance instance;
        ArtifactStory story;

        public StoryTests(string id) => this.id = id;

        [SetUp]
        public void SetUp()
        {
            var data = ContentCatalog.Load()?.Find(id);
            Assert.IsNotNull(data, id + " belum ada di katalog - jalankan Nusantara AR/Setup Everything");
            story = data.story;
            Assert.IsNotNull(story, id + " belum punya Kisah - jalankan Nusantara AR/Bangun Kisah");
            go = Object.Instantiate(data.prefab);
            instance = go.GetComponent<ArtifactInstance>();
            instance.Init(data);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void EveryChapter_HasVoiceAndSubtitlesInBothLanguages()
        {
            Assert.GreaterOrEqual(story.chapters.Count, 5);
            foreach (var ch in story.chapters)
            {
                Assert.IsFalse(string.IsNullOrEmpty(ch.title.id) || string.IsNullOrEmpty(ch.title.en), ch.key + ": judul");
                foreach (var (clip, cues, lang) in new[] { (ch.voiceID, ch.cuesID, "ID"), (ch.voiceEN, ch.cuesEN, "EN") })
                {
                    Assert.IsNotNull(clip, $"{ch.key}: suara {lang} belum ada (Tools/narasi/kisah_tts.py)");
                    Assert.Greater(clip.length, 3f, $"{ch.key} {lang}");
                    Assert.IsNotEmpty(cues, $"{ch.key}: subtitle {lang}");
                    for (int i = 1; i < cues.Count; i++)
                        Assert.Greater(cues[i].time, cues[i - 1].time, $"{ch.key} {lang}: urutan waktu kalimat");
                    Assert.Less(cues.Last().time, clip.length, $"{ch.key} {lang}: kalimat terakhir melewati durasi suara");
                    Assert.IsTrue(cues.All(c => !string.IsNullOrWhiteSpace(c.text)), $"{ch.key} {lang}: kalimat kosong");
                }
            }
        }

        [Test]
        public void VoiceClips_AreCompressedMonoAndLoadedOnDemand()
        {
            foreach (var clip in story.chapters.SelectMany(c => new[] { c.voiceID, c.voiceEN }).Where(c => c != null))
            {
                var imp = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Assert.IsTrue(imp.forceToMono, clip.name);
                Assert.AreEqual(AudioCompressionFormat.Vorbis, imp.defaultSampleSettings.compressionFormat, clip.name);
                Assert.IsFalse(imp.defaultSampleSettings.preloadAudioData, clip.name + " tidak boleh dimuat bersama katalog");
            }
        }

        [Test]
        public void Chapters_DriveValidStages_AndFocusVisibleParts()
        {
            var ex = instance.exploded;
            ex.SnapTo(0);
            bool drew = false, exploded = false;
            foreach (var ch in story.chapters)
            {
                Assert.Less(ch.stage, ex.StageCount, ch.key + ": tahap tidak ada");
                if (ch.stage >= 0) ex.SnapTo(ch.stage);
                drew |= ex.CurrentStage == 1;
                exploded |= ex.CurrentStage > 0;
                if (string.IsNullOrEmpty(ch.focusHotspot)) continue;
                var h = instance.Data.FindHotspot(ch.focusHotspot);
                Assert.IsNotNull(h, ch.key + ": hotspot " + ch.focusHotspot + " tidak ada");
                Assert.IsTrue(instance.IsHotspotAvailable(h), ch.key + ": " + ch.focusHotspot + " tidak terlihat pada tahap " + ex.CurrentStage);
            }
            if (ex.CanDraw)
                Assert.IsTrue(drew, "Kisah keris harus menghunus bilah (cara pembuatan & pamor)");
            Assert.IsTrue(exploded, "Kisah harus memperlihatkan bongkar (tahap > 0)");
            Assert.AreEqual(0, story.chapters.Last().stage, "Kisah berakhir dengan artefak dirakit kembali");
        }

        [Test]
        public void SplitSentences_SpreadsTimeOverTheText()
        {
            var cues = UI.StoryPanel.SplitSentences("Halo! Ini keris. Bagus, kan?", 10f);
            CollectionAssert.AreEqual(new[] { "Halo!", "Ini keris.", "Bagus, kan?" }, cues.Select(c => c.text).ToArray());
            Assert.AreEqual(0f, cues[0].time, 1e-4f);
            Assert.That(cues[1].time, Is.GreaterThan(0f).And.LessThan(cues[2].time));
            Assert.Less(cues[2].time, 10f);
        }
    }
}
