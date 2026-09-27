using System.Linq;
using NUnit.Framework;
using NusantaraAR.Marker;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>Uji Keris Bali prosedural: terdaftar di katalog + kartu, struktur bagian, tahap exploded view, skala.</summary>
    public class KerisBaliTests
    {
        const string Id = "KERIS_BALI_01";
        ContentCatalog catalog;
        GameObject go;
        ArtifactInstance instance;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/ContentCatalog tidak ditemukan - jalankan Nusantara AR/Setup Everything");
            var data = catalog.Find(Id);
            Assert.IsNotNull(data, Id + " belum ada di katalog - jalankan Nusantara AR/Build Keris Bali");
            go = Object.Instantiate(data.prefab);
            instance = go.GetComponent<ArtifactInstance>();
            instance.Init(data);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void Catalog_FindsBaliByItsMarker()
        {
            Assert.AreEqual(Id, catalog.FindByMarker(MarkerPattern.KerisBaliCode)?.artifactId);
            var codes = catalog.artifacts.Where(a => a != null).Select(a => a.markerCode).ToList();
            Assert.AreEqual(codes.Count, codes.Distinct().Count(), "Setiap artefak harus punya kode kartu sendiri");
        }

        [Test]
        public void EveryHotspot_PointsToExistingPart()
        {
            Assert.AreEqual(9, instance.Data.hotspots.Count);
            foreach (var h in instance.Data.hotspots)
                Assert.IsNotNull(instance.GetPart(h.partName), "Bagian tidak ada: " + h.partName + " (" + h.hotspotId + ")");
        }

        [Test]
        public void Exploded_HasAssembledPlusFiveStages()
        {
            Assert.AreEqual(6, instance.exploded.StageCount);
        }

        [Test]
        public void Assembled_HidesBlade_AndBladeHotspots()
        {
            instance.exploded.SnapTo(0);
            Assert.IsFalse(instance.GetPart("Wilah").IsVisible, "Bilah harus tersembunyi di dalam sarung");
            foreach (var h in instance.Data.hotspots.Where(h => h.visibleFrom == HotspotStage.Bilah))
                Assert.IsFalse(instance.IsHotspotAvailable(h), h.hotspotId + " tidak boleh tampil saat keris utuh");
            foreach (var h in instance.Data.hotspots.Where(h => h.visibleFrom == HotspotStage.Utuh))
                Assert.IsTrue(instance.IsHotspotAvailable(h), h.hotspotId + " harus tampil saat keris utuh");
        }

        [Test]
        public void Drawn_ShowsAllHotspots()
        {
            instance.exploded.SnapTo(1);
            Assert.IsTrue(instance.GetPart("Wilah").IsVisible);
            foreach (var h in instance.Data.hotspots)
                Assert.IsTrue(instance.IsHotspotAvailable(h), h.hotspotId + " harus tampil setelah dihunus");
        }

        [Test]
        public void Pivot_IsAtBase()
        {
            instance.exploded.SnapTo(0);
            Assert.AreEqual(0f, instance.GetWorldBounds().min.y, 0.005f, "Dasar artefak harus sejajar bidang (y = 0)");
        }

        [Test]
        public void Blade_MatchesAssumedLength()
        {
            instance.exploded.SnapTo(1);
            var b = instance.GetPart("Wilah").renderers[0].bounds;
            // Bilah 42 cm + pesi 7 cm.
            Assert.That(b.size.y, Is.InRange(0.47f, 0.51f));
        }

        [Test]
        public void LastStage_SlidesPendokOffTheGandar()
        {
            var pendok = instance.GetPart("Pendok").transform;
            instance.exploded.SnapTo(4);
            var before = pendok.localPosition;
            instance.exploded.SnapTo(5);
            Assert.Greater((pendok.localPosition - before).magnitude, 0.1f);
        }
    }
}
