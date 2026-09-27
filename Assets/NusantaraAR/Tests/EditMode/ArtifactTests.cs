using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>Uji konten & struktur setiap artefak di katalog (kriteria FR-04, FR-07, FR-08, FR-11).</summary>
    [TestFixture("KERIS_BALI_01")]
    [TestFixture("KERIS_SUMATRA_01")]
    public class ArtifactTests
    {
        readonly string id;
        ContentCatalog catalog;
        GameObject go;
        ArtifactInstance instance;

        public ArtifactTests(string id) => this.id = id;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/ContentCatalog tidak ditemukan - jalankan Nusantara AR/Setup Everything");
            var data = catalog.Find(id);
            Assert.IsNotNull(data, id + " belum ada di katalog - jalankan Nusantara AR/Setup Everything");
            go = Object.Instantiate(data.prefab);
            instance = go.GetComponent<ArtifactInstance>();
            instance.Init(data);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void Catalog_HoldsOnlyTheBlenderKerises()
        {
            var ids = catalog.artifacts.Where(a => a != null).Select(a => a.artifactId).ToList();
            CollectionAssert.AreEquivalent(new[] { "KERIS_BALI_01", "KERIS_SUMATRA_01" }, ids);
            var cats = catalog.NonEmptyCategories();
            Assert.IsTrue(cats.Contains(ArtifactCategory.KerisSenjata));
            Assert.IsFalse(cats.Contains(ArtifactCategory.Arca));
        }

        [Test]
        public void Catalog_FindsArtifactByItsOwnMarker()
        {
            Assert.AreNotEqual(0, instance.Data.markerCode);
            Assert.AreEqual(id, catalog.FindByMarker(instance.Data.markerCode)?.artifactId);
            var codes = catalog.artifacts.Where(a => a != null).Select(a => a.markerCode).ToList();
            Assert.AreEqual(codes.Count, codes.Distinct().Count(), "Setiap artefak harus punya kode kartu sendiri");
            Assert.IsNull(catalog.FindByMarker(0xEEC1), "Kartu keris sementara sudah ditarik");
            Assert.IsNull(catalog.FindByMarker(0xDA26), "Kartu Keris Jawa sudah ditarik");
        }

        [Test]
        public void Model_UsesImportedBlenderMeshes()
        {
            var meshes = go.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToList();
            Assert.IsTrue(meshes.All(m => m != null), "Ada mesh yang hilang");
            foreach (var m in meshes)
                StringAssert.EndsWith(".glb", UnityEditor.AssetDatabase.GetAssetPath(m), m.name + " harus berasal dari GLB Blender");
        }

        [Test]
        public void Textures_AreCompressedForMobile()
        {
            var textures = go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null).SelectMany(m => m.GetTexturePropertyNames().Select(m.GetTexture))
                .OfType<Texture2D>().Distinct().ToList();
            Assert.Greater(textures.Count, 5);
            foreach (var t in textures)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(t);
                StringAssert.EndsWith(".png", path, t.name + " tidak boleh memakai tekstur mentah (ARGB32) dari GLB");
                var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
                var android = imp.GetPlatformTextureSettings("Android");
                Assert.IsTrue(android.overridden && android.format.ToString().StartsWith("ASTC"), path + " harus ASTC di Android");
            }
        }

        [Test]
        public void EveryHotspot_PointsToExistingPart()
        {
            Assert.GreaterOrEqual(instance.Data.hotspots.Count, 9);
            foreach (var h in instance.Data.hotspots)
                Assert.IsNotNull(instance.GetPart(h.partName), "Bagian tidak ada: " + h.partName + " (" + h.hotspotId + ")");
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
        public void Hotspots_SitOnTheFrontOfTheirPart()
        {
            instance.exploded.SnapTo(1);
            foreach (var h in instance.Data.hotspots)
            {
                Assert.IsTrue(instance.TryGetHotspotWorldPosition(h, out var p));
                var b = GlbBounds(instance.GetPart(h.partName));
                b.Expand(0.02f);
                Assert.IsTrue(b.Contains(p), h.hotspotId + " jauh dari bagiannya");
                Assert.Less(p.z, b.center.z, h.hotspotId + " harus di muka depan (-Z)");
            }
        }

        [Test]
        public void EveryStage_MovesSomething()
        {
            Assert.GreaterOrEqual(instance.exploded.StageCount, 5);
            var parts = go.GetComponentsInChildren<ArtifactPart>().ToList();
            instance.exploded.SnapTo(0);
            var prev = parts.Select(p => p.transform.localPosition).ToList();
            for (int s = 1; s < instance.exploded.StageCount; s++)
            {
                instance.exploded.SnapTo(s);
                var now = parts.Select(p => p.transform.localPosition).ToList();
                Assert.IsTrue(now.Zip(prev, (a, b) => (a - b).magnitude).Any(d => d > 0.05f), "Tahap " + s + " tidak menggerakkan bagian mana pun");
                prev = now;
            }
        }

        [Test]
        public void Draw_BladeLeavesSheathWithoutPassingThroughIt()
        {
            var ex = instance.exploded;
            Assert.IsTrue(ex.CanDraw, "Tahap 1 harus punya jalur cabut (drawOut)");
            var blade = instance.GetPart("Wilah");
            var sheath = new[] { instance.GetPart("Warangka"), instance.GetPart("Gandar") };
            Vector3 PoseOf(int stage) => ex.stages[stage].poses.First(p => p.part == blade.transform).localPosition;
            var sheathed = PoseOf(0);
            var drawn = PoseOf(1);
            var draw = ex.stages[1].drawOut;
            ex.SnapTo(0);
            foreach (var r in blade.renderers) r.enabled = true;

            for (int i = 0; i <= 200; i++)
            {
                var p = ExplodedViewController.DrawPath(sheathed, drawn, draw, i / 200f);
                blade.transform.localPosition = p;
                // Selama tarikan lurus sepanjang sumbu sarung, bilah memang masih di dalam sarung.
                var d = p - sheathed;
                if ((d - Vector3.Project(d, draw)).magnitude < 1e-4f) continue;
                var bb = GlbBounds(blade);
                foreach (var s in sheath)
                    Assert.IsFalse(bb.Intersects(GlbBounds(s)),
                        $"Bilah menembus {s.partName} pada u = {i / 200f:0.00} (tarikan lurus kurang jauh?)");
            }
            Assert.That((ExplodedViewController.DrawPath(sheathed, drawn, draw, 1f) - drawn).magnitude, Is.LessThan(1e-5f));
        }

        [Test]
        public void Pivot_IsAtBase()
        {
            instance.exploded.SnapTo(0);
            var b = instance.GetWorldBounds();
            Assert.AreEqual(0f, b.min.y, 0.005f, "Dasar artefak harus sejajar bidang (y = 0)");
        }

        [Test]
        public void RelativeScale_IsClamped()
        {
            instance.SetRelativeScale(10f);
            Assert.AreEqual(ArtifactInstance.MaxScale, instance.RelativeScale, 1e-4f);
            instance.SetRelativeScale(0.01f);
            Assert.AreEqual(ArtifactInstance.MinScale, instance.RelativeScale, 1e-4f);
            instance.ResetView();
            Assert.AreEqual(1f, instance.RelativeScale, 1e-4f);
        }

        [Test]
        public void LocalizedString_FallsBackToIndonesian()
        {
            var prev = Locale.Current;
            try
            {
                Locale.Current = Language.EN;
                Assert.AreEqual("halo", new LocalizedString("halo", "").Get());
                Assert.AreEqual("hello", new LocalizedString("halo", "hello").Get());
            }
            finally
            {
                Locale.Current = prev;
            }
        }

        internal static Bounds GlbBounds(ArtifactPart part)
        {
            var b = part.renderers[0].bounds;
            foreach (var r in part.renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
