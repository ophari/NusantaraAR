using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>Uji musik latar setiap artefak: klip terpasang, berkredit, dan di-stream (tidak dimuat bersama katalog).</summary>
    [TestFixture("KERIS_BALI_01")]
    [TestFixture("KERIS_SUMATRA_01")]
    [TestFixture("BOROBUDUR_01")]
    public class MusicTests
    {
        readonly string id;
        ArtifactData data;

        public MusicTests(string id) => this.id = id;

        [SetUp]
        public void SetUp()
        {
            data = ContentCatalog.Load()?.Find(id);
            Assert.IsNotNull(data, id + " belum ada di katalog - jalankan Nusantara AR/Setup Everything");
        }

        [Test]
        public void HasBackgroundMusicWithCredit()
        {
            Assert.IsNotNull(data.backgroundMusic, id + " belum punya musik latar - jalankan Nusantara AR/Pasang Musik Latar");
            Assert.Greater(data.backgroundMusic.length, 30f, id + ": musik terlalu pendek untuk diulang");
            StringAssert.Contains("Pixabay", data.musicCredit, id + ": kredit musik");
        }

        [Test]
        public void MusicClip_IsStreamedVorbis()
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(data.backgroundMusic));
            Assert.IsNotNull(imp, id + ": importer musik");
            Assert.AreEqual(AudioClipLoadType.Streaming, imp.defaultSampleSettings.loadType, id);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, imp.defaultSampleSettings.compressionFormat, id);
            Assert.IsFalse(imp.defaultSampleSettings.preloadAudioData, id + " tidak boleh dimuat bersama katalog");
        }

        [Test]
        public void LoopEnvelope_FadesAtBothEnds()
        {
            Assert.AreEqual(0f, AudioManager.LoopEnvelope(0f, 60f, 2f), 1e-4f);
            Assert.AreEqual(0.5f, AudioManager.LoopEnvelope(1f, 60f, 2f), 1e-4f);
            Assert.AreEqual(1f, AudioManager.LoopEnvelope(30f, 60f, 2f), 1e-4f);
            Assert.AreEqual(0.5f, AudioManager.LoopEnvelope(59f, 60f, 2f), 1e-4f);
            Assert.AreEqual(0f, AudioManager.LoopEnvelope(60f, 60f, 2f), 1e-4f);
            Assert.AreEqual(1f, AudioManager.LoopEnvelope(0f, 3f, 2f), 1e-4f, "klip lebih pendek dari 2x fade tidak di-fade");
        }
    }
}
