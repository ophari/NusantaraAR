using System.Linq;
using NUnit.Framework;
using NusantaraAR.Marker;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>Uji Keris Jawa (model Blender): terdaftar di katalog + kartu, struktur bagian, tahap exploded view, sumbu & skala FBX.</summary>
    public class KerisJawaTests
    {
        const string Id = "KERIS_JAWA_01";
        ContentCatalog catalog;
        GameObject go;
        ArtifactInstance instance;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/ContentCatalog tidak ditemukan - jalankan Nusantara AR/Setup Everything");
            var data = catalog.Find(Id);
            Assert.IsNotNull(data, Id + " belum ada di katalog - jalankan Nusantara AR/Build Keris Jawa");
            go = Object.Instantiate(data.prefab);
            instance = go.GetComponent<ArtifactInstance>();
            instance.Init(data);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void Catalog_FindsJawaByItsMarker()
        {
            Assert.AreEqual(Id, catalog.FindByMarker(MarkerPattern.KerisJawaCode)?.artifactId);
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
        public void Exploded_HasAssembledPlusFourStages()
        {
            Assert.AreEqual(5, instance.exploded.StageCount);
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
        public void Blade_MatchesBlueprintLength()
        {
            instance.exploded.SnapTo(1);
            var b = instance.GetPart("Wilah").renderers[0].bounds;
            // Bilah 34,5 cm + peksi 7,5 cm.
            Assert.That(b.size.y, Is.InRange(0.40f, 0.44f));
        }

        [Test]
        public void FbxAxes_KeepBlueprintOrientation()
        {
            instance.exploded.SnapTo(0);
            var w = instance.GetPart("Warangka").renderers[0].bounds;
            Assert.Greater(w.center.x, 0f, "Tanduk tinggi warangka harus di sisi kanan (+X)");
            Assert.That(w.size.x, Is.InRange(0.17f, 0.19f), "Lebar warangka 18 cm menurut cetak biru");
            Assert.That(instance.GetWorldBounds().size.y, Is.InRange(0.55f, 0.60f));
            var hulu = instance.GetPart("Hulu").renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            Assert.That(hulu.size.y, Is.InRange(0.10f, 0.12f), "Hulu + selut 11 cm menurut cetak biru");
        }
    }
}
