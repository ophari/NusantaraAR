using System.Linq;
using NUnit.Framework;
using NusantaraAR.Marker;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>Uji khusus Keris Bali dan Keris Sumatra (model Blender): kartu, bagian, ukuran, dan arah exploded view.</summary>
    public class KerisBlenderTests
    {
        GameObject go;

        ArtifactInstance Spawn(string id)
        {
            var data = ContentCatalog.Load().Find(id);
            Assert.IsNotNull(data, id + " belum ada di katalog - jalankan Nusantara AR/Setup Everything");
            go = Object.Instantiate(data.prefab);
            var inst = go.GetComponent<ArtifactInstance>();
            inst.Init(data);
            return inst;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        static Bounds Of(ArtifactInstance i, string part) => ArtifactTests.GlbBounds(i.GetPart(part));

        [Test]
        public void Bali_StandsOnItsJagrak_WithKerisLyingAlongX()
        {
            var i = Spawn("KERIS_BALI_01");
            Assert.AreEqual(MarkerPattern.KerisBaliCode, i.Data.markerCode);
            Assert.AreEqual(11, i.Data.hotspots.Count);
            Assert.AreEqual(6, i.exploded.StageCount);
            i.exploded.SnapTo(0);
            var jagrak = Of(i, "Jagrak");
            Assert.AreEqual(0f, jagrak.min.y, 0.003f, "Jagrak harus berdiri di y = 0");
            Assert.That(jagrak.size.x, Is.InRange(0.43f, 0.47f), "Jagrak 45 cm");
            var gandar = Of(i, "Gandar");
            Assert.Greater(gandar.min.y, jagrak.min.y + 0.10f, "Keris bersandar di atas jagrak");
            Assert.Greater(Of(i, "Hulu").center.x, gandar.center.x, "Hulu di sisi +X");
            Assert.Less(Of(i, "Pendok").center.x, gandar.center.x, "Pendok di sisi -X");
        }

        [Test]
        public void Bali_Blade_Is40cm_AndDrawnAboveTheSheath()
        {
            var i = Spawn("KERIS_BALI_01");
            i.exploded.SnapTo(1);
            var blade = Of(i, "Wilah");
            Assert.That(blade.size.x, Is.InRange(0.47f, 0.49f), "Bilah 40 cm + pesi 8 cm, mendatar");
            Assert.Greater(blade.min.y, Of(i, "Warangka").max.y - 0.02f, "Bilah terhunus berada di atas sarung");
            var pendok = i.GetPart("Pendok").transform;
            i.exploded.SnapTo(4);
            var before = pendok.localPosition;
            i.exploded.SnapTo(5);
            Assert.Greater((pendok.localPosition - before).magnitude, 0.1f, "Tahap terakhir melepas pendok");
        }

        [Test]
        public void Sumatra_StandsUpright_OnItsDudukan()
        {
            var i = Spawn("KERIS_SUMATRA_01");
            Assert.AreEqual(MarkerPattern.KerisSumatraCode, i.Data.markerCode);
            Assert.AreEqual(9, i.Data.hotspots.Count);
            Assert.AreEqual(5, i.exploded.StageCount);
            i.exploded.SnapTo(0);
            var all = i.GetWorldBounds();
            Assert.That(all.size.y, Is.InRange(0.50f, 0.55f), "Tinggi keris tersarung + dudukan ~52 cm");
            Assert.AreEqual(0f, Of(i, "Dudukan").min.y, 0.003f);
            Assert.Greater(Of(i, "Warangka").center.x, 0.005f, "Tanduk panjang sampir di sisi +X");
        }

        [Test]
        public void Sumatra_Blade_Is36cm_AndDrawnBesideTheSheath()
        {
            var i = Spawn("KERIS_SUMATRA_01");
            i.exploded.SnapTo(1);
            var blade = Of(i, "Wilah");
            Assert.That(blade.size.y, Is.InRange(0.425f, 0.44f), "Bilah 36 cm + pesi 7,2 cm, tegak");
            Assert.Greater(blade.min.x, Of(i, "Warangka").max.x, "Bilah terhunus berada di samping sarung");
        }
    }
}
