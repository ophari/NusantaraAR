using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NusantaraAR.Marker;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>
    /// Uji khusus Karambit (model Blender): bagian, ukuran, berdiri di dudukan, dan sarung yang dilepas menyusuri lengkung
    /// bilah (diputar pada pusat busur punggung) tanpa ditembus bilah.
    /// </summary>
    public class KarambitTests
    {
        const string Id = "KARAMBIT_01";
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

        Bounds Of(string part) => ArtifactFixture.GlbBounds(i.GetPart(part));

        [Test]
        public void HasFiveParts_NineHotspots_FiveStages_AndNoDrawButton()
        {
            Assert.AreEqual(ArtifactCategory.KerisSenjata, i.Data.category);
            Assert.AreEqual(MarkerPattern.KarambitCode, i.Data.markerCode);
            foreach (var p in new[] { "Mata", "Cincin", "Hulu", "Sarung", "Dudukan" }) Assert.IsNotNull(i.GetPart(p), p);
            Assert.AreEqual(9, i.Data.hotspots.Count);
            Assert.AreEqual(5, i.exploded.StageCount);
            Assert.IsFalse(i.exploded.CanDraw, "Bilah cakar tidak dicabut lurus: tanpa tombol Hunus");
        }

        [Test]
        public void StandsOnItsStand_HiltUp_ClawCurvingForward()
        {
            i.exploded.SnapTo(0);
            var stand = Of("Dudukan");
            Assert.AreEqual(0f, stand.min.y, 0.003f, "Dudukan di y = 0");
            var hilt = Of("Hulu");
            var sheath = Of("Sarung");
            Assert.AreEqual(i.GetWorldBounds().max.y, hilt.max.y, 0.002f, "Lubang hulu di puncak");
            Assert.Greater(hilt.min.y, sheath.max.y, "Hulu di atas sarung");
            Assert.Greater(sheath.center.x, hilt.center.x, "Lengkung cakar ke sisi +X, hulu condong ke -X");
            Assert.Greater(sheath.min.y, stand.min.y + 0.014f, "Sarung melayang di atas alas dudukan (tebal 1,4 cm)");
        }

        [Test]
        public void Knife_IsAbout16cmTall_WithA22mmFingerRing()
        {
            i.exploded.SnapTo(0);
            var knife = Of("Mata");
            knife.Encapsulate(Of("Hulu"));
            Assert.That(knife.size.y, Is.InRange(0.150f, 0.168f), "Tinggi karambit tanpa sarung ±15,9 cm");
            Assert.That(knife.size.x, Is.InRange(0.095f, 0.115f), "Lebar ±10,5 cm");
            Assert.That(Of("Mata").size.z, Is.InRange(0.003f, 0.006f), "Tebal bilah ±4-5 mm");
        }

        [Test]
        public void SheathPivot_IsTheCentreOfTheBladeSpine()
        {
            // Semua verteks bilah (kecuali puting di atas pangkal) berada di dalam lingkaran punggung berjari-jari 5,2 cm.
            i.exploded.SnapTo(0);
            var pivot = i.GetPart("Sarung").transform;
            float rMax = 0f;
            foreach (var v in Vertices("Mata"))
            {
                var l = pivot.InverseTransformPoint(v);
                if (Angle(l) < 178f) continue;      // puting & bagian dalam cincin
                rMax = Mathf.Max(rMax, new Vector2(l.x, l.y).magnitude);
            }
            Assert.AreEqual(0.052f, rMax, 0.0008f, "Punggung bilah harus busur berpusat di pivot sarung");
        }

        [Test]
        public void SheathSlidesOff_AlongTheCurve_WithoutTheBladeCuttingThroughIt()
        {
            var ex = i.exploded;
            var sheath = i.GetPart("Sarung").transform;
            ex.SnapTo(0);
            var r0 = sheath.localRotation;
            var r1 = ex.stages[1].poses.First(p => p.part == sheath).localRotation;
            Assert.AreEqual(sheath.localPosition, ex.stages[1].poses.First(p => p.part == sheath).localPosition,
                "Tahap 1 hanya memutar sarung pada pivotnya");

            // Selubung sarung per pita sudut 3° di ruang pivot sarung: jari-jari min/maks dan setengah tebal.
            var env = new Dictionary<int, (float rMin, float rMax, float h)>();
            foreach (var v in Vertices("Sarung"))
            {
                var l = sheath.InverseTransformPoint(v);
                int bin = Mathf.FloorToInt(Angle(l) / 3f);
                float r = new Vector2(l.x, l.y).magnitude;
                env[bin] = env.TryGetValue(bin, out var e)
                    ? (Mathf.Min(e.rMin, r), Mathf.Max(e.rMax, r), Mathf.Max(e.h, Mathf.Abs(l.z)))
                    : (r, r, Mathf.Abs(l.z));
            }
            int lo = env.Keys.Min() + 1, hi = env.Keys.Max() - 1;     // pita mulut & ekor diabaikan
            var blade = Vertices("Mata").ToList();
            const float eps = 0.0006f;
            for (int k = 0; k <= 40; k++)
            {
                sheath.localRotation = Quaternion.Slerp(r0, r1, k / 40f);
                foreach (var v in blade)
                {
                    var l = sheath.InverseTransformPoint(v);
                    int bin = Mathf.FloorToInt(Angle(l) / 3f);
                    if (bin < lo || bin > hi || !env.TryGetValue(bin, out var e)) continue;
                    // Selama meluncur, bagian bilah yang berada di rentang sudut sarung harus utuh di dalam selubungnya;
                    // setelah lepas, tidak ada lagi bagian bilah di rentang itu.
                    Assert.Less(k, 40, "Sarung yang sudah lepas masih menumpuk bilah");
                    float r = new Vector2(l.x, l.y).magnitude;
                    Assert.IsTrue(r > e.rMin - eps && r < e.rMax + eps && Mathf.Abs(l.z) < e.h + eps,
                        $"Bilah menembus dinding sarung pada u = {k / 40f:0.00} (sudut {Angle(l):0}°, r {r * 1000:0.0} mm, tebal {l.z * 1000:0.0} mm)");
                }
            }
        }

        [Test]
        public void Exploded_LiftsKnife_ThenHilt_ThenFerrule()
        {
            var ex = i.exploded;
            ex.SnapTo(1);
            var standTop = Of("Dudukan").max.y;
            ex.SnapTo(2);
            Assert.Greater(Of("Cincin").min.y, standTop, "Pisau terangkat lepas dari lengan penjepit");
            ex.SnapTo(3);
            Assert.Greater(Of("Hulu").min.y, Of("Mata").max.y, "Hulu lepas dari ujung puting");
            ex.SnapTo(4);
            Assert.Greater(Of("Cincin").min.y, Of("Mata").max.y, "Cincin lepas dari puting");
            Assert.Greater(Of("Hulu").min.y, Of("Cincin").max.y, "Cincin di bawah hulu");
        }

        IEnumerable<Vector3> Vertices(string part)
        {
            foreach (var r in i.GetPart(part).renderers)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                var m = r.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices) yield return m.MultiplyPoint3x4(v);
            }
        }

        /// <summary>Sudut 0..360° di bidang x-y lokal (bidang profil cakar, = bidang XZ Blender).</summary>
        static float Angle(Vector3 l) => (Mathf.Atan2(l.y, l.x) * Mathf.Rad2Deg + 360f) % 360f;
    }
}
