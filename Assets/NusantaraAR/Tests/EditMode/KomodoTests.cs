using System.Linq;
using NUnit.Framework;
using NusantaraAR.Marker;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>
    /// Uji khusus Komodo (diorama model Blender 1:10): bagian, ukuran, lidah tersimpan di mulut, rahang berputar di engsel,
    /// telur keluar dari ceruk sarang, dan komodo terangkat dari alas.
    /// </summary>
    public class KomodoTests
    {
        const string Id = "KOMODO_01";
        const float Scale = 0.1f;              // KomodoBuilder.Scale
        const float JawOpenDegrees = 24f;      // KomodoBuilder.JawOpenDegrees (= JAW_OPEN di komodo.py)
        const float HingeX = -0.9611f;         // ENGSEL rahang di komodo.py (x Blender = x ruang Model)
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

        /// <summary>Bounds bagian di ruang Model (meter asli, sebelum diperkecil 1:10).</summary>
        Bounds Model(string part)
        {
            var w = Of(part);
            var b = new Bounds(i.modelRoot.InverseTransformPoint(w.center), Vector3.zero);
            b.Encapsulate(i.modelRoot.InverseTransformPoint(w.min));
            b.Encapsulate(i.modelRoot.InverseTransformPoint(w.max));
            return b;
        }

        [Test]
        public void HasSevenParts_TenHotspots_FiveStages_AndNoDrawButton()
        {
            Assert.AreEqual(ArtifactCategory.Satwa, i.Data.category);
            Assert.AreEqual(MarkerPattern.KomodoCode, i.Data.markerCode);
            foreach (var p in new[] { "Alas", "Sarang", "Telur", "Tubuh", "Kepala", "Rahang", "Lidah" }) Assert.IsNotNull(i.GetPart(p), p);
            Assert.AreEqual(10, i.Data.hotspots.Count);
            Assert.AreEqual(5, i.exploded.StageCount);
            Assert.IsFalse(i.exploded.CanDraw);
        }

        [Test]
        public void IsAOneToTenDiorama_WithA23mKomodo()
        {
            i.exploded.SnapTo(0);
            Assert.AreEqual(Scale, i.modelRoot.localScale.x, 1e-5f, "Model diperkecil 1:10");
            Assert.AreEqual(0f, Of("Alas").min.y, 0.002f, "Alas diorama di y = 0");
            Assert.That(Of("Alas").size.x, Is.InRange(0.30f, 0.36f), "Alas ±33 cm");
            var body = Model("Tubuh");
            body.Encapsulate(Model("Kepala"));
            Assert.That(body.size.x, Is.InRange(1.5f, 1.9f), "Moncong ke ujung ekor yang melengkung (ruang Model, m)");
            Assert.Less(Model("Kepala").center.x, Model("Sarang").center.x, "Kepala ke -X, sarang di +X");
            Assert.Less(Model("Tubuh").min.y, 0.01f, "Cakar menapak di tanah");
        }

        [Test]
        public void Tongue_IsHiddenInTheMouth_ThenFlicksOutPastTheSnout()
        {
            var ex = i.exploded;
            ex.SnapTo(0);
            Assert.IsFalse(i.GetPart("Lidah").IsVisible, "Lidah tersimpan di dalam mulut saat utuh");
            var head = Model("Kepala");
            ex.SnapTo(1);
            Assert.IsTrue(i.GetPart("Lidah").IsVisible);
            var tongue = Model("Lidah");
            Assert.Less(tongue.min.x, head.min.x - 0.15f, "Lidah terjulur >15 cm melewati moncong");
        }

        [Test]
        public void Jaw_OpensAboutItsHinge()
        {
            var ex = i.exploded;
            var jaw = i.GetPart("Rahang").transform;
            ex.SnapTo(0);
            var pose = ex.stages[1].poses.First(p => p.part == jaw);
            Assert.AreEqual(jaw.localPosition, pose.localPosition, "Tahap 1 hanya memutar rahang pada engselnya");
            Assert.AreEqual(JawOpenDegrees, Quaternion.Angle(Quaternion.identity, pose.localRotation), 0.1f);
            var closed = Model("Rahang");
            ex.SnapTo(1);
            var open = Model("Rahang");
            Assert.Less(open.min.y, closed.min.y - 0.05f, "Ujung rahang turun saat mulut dibuka");
            Assert.AreEqual(HingeX, jaw.localPosition.x, 0.002f, "Pivot rahang di engsel (komodo.py ENGSEL)");
        }

        [Test]
        public void Eggs_LeaveTheNest_ThenTheKomodoIsLifted_ThenTheJawSeparates()
        {
            var ex = i.exploded;
            ex.SnapTo(1);
            var nest = Model("Sarang");
            var eggs = Model("Telur");
            Assert.Greater(eggs.max.z, nest.min.z, "Telur berada di dalam ceruk sarang");
            ex.SnapTo(2);
            Assert.Less(Model("Telur").max.z, nest.min.z, "Telur keluar sepenuhnya ke depan gundukan sarang");
            Assert.GreaterOrEqual(Model("Telur").min.y, -0.001f, "Telur tetap di atas alas");
            var groundTop = Model("Alas").max.y;
            ex.SnapTo(3);
            Assert.Greater(Model("Tubuh").min.y, 0.25f, "Komodo terangkat dari alas");
            Assert.Greater(Model("Tubuh").min.y, Model("Telur").min.y);
            Assert.Less(groundTop, 0.25f, "Alas tetap di tempat (rumput & batu < 25 cm)");
            var headBottom = Model("Kepala").min.y;
            ex.SnapTo(4);
            Assert.Less(Model("Rahang").max.y, headBottom, "Rahang bawah lepas di bawah kepala");
        }
    }
}
