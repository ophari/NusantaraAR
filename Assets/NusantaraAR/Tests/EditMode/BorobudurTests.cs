using System.Linq;
using NUnit.Framework;
using NusantaraAR.Marker;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>Uji khusus Candi Borobudur (model Blender): skala 1:200, sepuluh tingkat, dan bongkar per tingkat.</summary>
    public class BorobudurTests
    {
        const string Id = "BOROBUDUR_01";
        static readonly string[] Levels =
            { "Kaki", "Teras1", "Teras2", "Teras3", "Teras4", "Teras5", "Lingkar1", "Lingkar2", "Lingkar3", "StupaInduk" };

        GameObject go;
        ArtifactInstance i;

        [SetUp]
        public void SetUp()
        {
            var data = ContentCatalog.Load().Find(Id);
            Assert.IsNotNull(data, Id + " belum ada di katalog - jalankan Nusantara AR/Setup Everything");
            go = Object.Instantiate(data.prefab);
            i = go.GetComponent<ArtifactInstance>();
            i.Init(data);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void IsScaled1To200_OnTheModelChild()
        {
            Assert.AreEqual(Vector3.one, go.transform.localScale, "Root tetap skala 1 (AR, Scan QR, slider skala)");
            Assert.AreEqual(1f / 200f, i.modelRoot.localScale.x, 1e-6f);
            i.exploded.SnapTo(0);
            var b = i.GetWorldBounds();
            Assert.That(b.size.x, Is.InRange(0.61f, 0.625f), "Tapak 123 m (+ tangga) -> ±61,5 cm");
            Assert.That(b.size.z, Is.InRange(0.61f, 0.625f));
            Assert.AreEqual(0.175f, b.size.y, 0.002f, "Tinggi 35 m -> 17,5 cm");
            Assert.AreEqual(0f, b.center.x, 0.002f, "Pusat kaki di titik letak");
            Assert.AreEqual(0f, b.center.z, 0.002f);
        }

        [Test]
        public void HasTenLevels_TenHotspots_AndNoDrawButton()
        {
            Assert.AreEqual(ArtifactCategory.Candi, i.Data.category);
            Assert.AreEqual(MarkerPattern.BorobudurCode, i.Data.markerCode);
            foreach (var l in Levels) Assert.IsNotNull(i.GetPart(l), l);
            Assert.AreEqual(10, i.Data.hotspots.Count);
            Assert.IsTrue(i.Data.hotspots.All(h => h.visibleFrom == HotspotStage.Utuh));
            Assert.AreEqual(5, i.exploded.StageCount);
            Assert.IsFalse(i.exploded.CanDraw, "Candi tidak punya tombol Hunus");
        }

        [Test]
        public void Exploded_SeparatesEveryLevel_BottomToTop()
        {
            i.exploded.SnapTo(i.exploded.StageCount - 1);
            for (int k = 1; k < Levels.Length; k++)
            {
                var below = ArtifactFixture.GlbBounds(i.GetPart(Levels[k - 1]));
                var above = ArtifactFixture.GlbBounds(i.GetPart(Levels[k]));
                Assert.Greater(above.min.y, below.max.y, Levels[k] + " harus melayang di atas " + Levels[k - 1]);
            }
            i.exploded.SnapTo(0);
            var kaki = ArtifactFixture.GlbBounds(i.GetPart("Kaki"));
            Assert.AreEqual(0f, kaki.min.y, 0.002f, "Kaki candi tetap di bidang saat utuh");
        }
    }
}
