using System.Linq;
using NUnit.Framework;
using NusantaraAR.Marker;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>
    /// Uji khusus Golok Betawi (model Blender): bagian, ukuran skala 1:1, orientasi sangkep di depan (-Z),
    /// bilah tersembunyi saat tersarung, dan jalur animasi hunus tanpa menembus sarung.
    /// </summary>
    public class GolokBetawiTests
    {
        const string Id = "GOLOK_BETAWI_01";
        GameObject go;
        ArtifactInstance i;

        [SetUp]
        public void SetUp()
        {
            var data = ContentCatalog.Load().Find(Id);
            Assert.IsNotNull(data, Id + " belum ada di katalog - jalankan Nusantara AR/Build Golok Betawi");
            go = Object.Instantiate(data.prefab);
            i = go.GetComponent<ArtifactInstance>();
            i.Init(data);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        Bounds Of(string part) => ArtifactFixture.GlbBounds(i.GetPart(part));

        [Test]
        public void HasEightParts_TenHotspots_FiveStages_AndDrawAnimation()
        {
            Assert.AreEqual(ArtifactCategory.KerisSenjata, i.Data.category);
            Assert.AreEqual(MarkerPattern.GolokBetawiCode, i.Data.markerCode);
            foreach (var p in new[] { "Bilah", "Salut", "Hulu", "Paku", "Sarung", "KepalaSarung", "Simpay", "Sangkep" })
                Assert.IsNotNull(i.GetPart(p), p);
            Assert.AreEqual(10, i.Data.hotspots.Count);
            Assert.AreEqual(5, i.exploded.StageCount);
            Assert.IsTrue(i.exploded.CanDraw, "Golok Betawi harus punya animasi Hunus / Sarungkan");
        }

        [Test]
        public void ProportionsAndOrientation_MatchBetawiSorenan()
        {
            i.exploded.SnapTo(0);
            Assert.IsFalse(i.GetPart("Bilah").IsVisible, "Bilah harus tersembunyi di dalam sarung saat utuh");
            var sarung = Of("Sarung");
            var sangkep = Of("Sangkep");
            var hulu = Of("Hulu");
            Assert.AreEqual(0f, sarung.min.y, 0.005f, "Buntut sarung di y = 0");
            Assert.Less(sangkep.center.z, sarung.center.z, "Sangkep (cantelan pinggang) harus menghadap depan (-Z)");
            Assert.Greater(hulu.max.y, sarung.max.y, "Hulu di atas sarung");

            i.exploded.SnapTo(1);
            Assert.IsTrue(i.GetPart("Bilah").IsVisible, "Bilah harus tampil saat dihunus");
            var bilah = Of("Bilah");
            Assert.That(bilah.size.y, Is.InRange(0.340f, 0.365f), "Panjang bilah + puting ±35,4 cm");
            Assert.That(bilah.size.x, Is.InRange(0.036f, 0.046f), "Lebar bilah ±4,1 cm");
            Assert.That(bilah.size.z, Is.InRange(0.003f, 0.006f), "Tebal punggung bilah ±4,4 mm");
        }

        [Test]
        public void Draw_BladeLeavesSheathWithoutPassingThroughIt()
        {
            var ex = i.exploded;
            var blade = i.GetPart("Bilah");
            var sheath = new[] { i.GetPart("Sarung"), i.GetPart("KepalaSarung") };
            Vector3 PoseOf(int stage) => ex.stages[stage].poses.First(p => p.part == blade.transform).localPosition;
            var sheathed = PoseOf(0);
            var drawn = PoseOf(1);
            var draw = ex.stages[1].drawOut;
            ex.SnapTo(0);
            foreach (var r in blade.renderers) r.enabled = true;

            for (int k = 0; k <= 200; k++)
            {
                var p = ExplodedViewController.DrawPath(sheathed, drawn, draw, k / 200f);
                blade.transform.localPosition = p;
                var d = p - sheathed;
                if ((d - Vector3.Project(d, draw)).magnitude < 1e-4f) continue;
                var bb = Of("Bilah");
                foreach (var s in sheath)
                    Assert.IsFalse(bb.Intersects(ArtifactFixture.GlbBounds(s)),
                        $"Bilah menembus {s.partName} pada u = {k / 200f:0.00}");
            }
        }
    }
}
